using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace CentralMaster.Communications
{
    public enum SerialErrorCodes : ushort
    {
        IOException = 0x0001 // possible comm lost; restart the serial interface
    }

    public class SerialComm
    {
        #region variables
        public Action<string> writeToDisplay = null;
        public Action<byte[]> receivedData = null;
        public Action<ushort> errorOccured = null;
        private SerialPort port = null;
        private Thread rxThread = null;
        //byte[] recvBuf = new byte[4096];
        private bool stopReading = false;
        private CancellationTokenSource cancelRead = null;
        private bool serialInterfaceOpen = false;
        private bool commInterfaceRunning = false;

        private string _portName = string.Empty;
        private int _baudRate = 115200;
        private int _databits = 8;
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;
        private System.Timers.Timer restart_timer = null;

        #endregion //variables

        #region property
        public string PortName
        {
            get
            {
                return _portName;
            }
            set
            {
                _portName = value;
            }
        }

        public bool IsPortOpen
        {
            get
            {
                return serialInterfaceOpen;
            }
        }

        public bool IsRxThreadRunning
        {
            get
            {
                return commInterfaceRunning;
            }
        }

        public int BaudRate
        {
            get { return _baudRate; }
            set { _baudRate = value; }
        }

        public int DataBits
        {
            get { return _databits; }
            set { _databits = value; }
        }

        public StopBits StopBitValue
        {
            get { return _stopBits; }
            set { _stopBits = value; }
        }

        public Parity ParityValue
        {
            get { return _parity; }
            set { _parity = value; }
        }
        #endregion // property

        public SerialComm(string portName)
        {
            _portName = portName;
        }

        #region utility_funcs
        private void WriteMessage(string s)
        {
            if (writeToDisplay != null)
                writeToDisplay(s);
            else
                Console.WriteLine(s);
        }

        private void raiseAppSerialDataEvent(byte[] received)
        {
            if (received == null)
            {
                return;
            }
            if (received.Length == 0)
            {
                return;
            }
            if (receivedData != null)
            {
                //WriteMessage(BitConverter.ToString(received));
                receivedData(received);
            }
            else
            {
                string disp_str = BitConverter.ToString(received).Replace("-", " ");
                WriteMessage(disp_str);
            }
        }

        private void raiseErrorOccurred(ushort error_code)
        {
            errorOccured?.Invoke(error_code);
        }

        public async void transmit(byte[] tosendb)
        {
            await port.BaseStream.WriteAsync(tosendb, 0, tosendb.Length);
        }

        public async void transmit(byte[] tosendb, int startidx, int length)
        {
            await port.BaseStream.WriteAsync(tosendb, startidx, length);
        }
        #endregion //utility_funcs

        #region init_funcs

        public bool Open()
        {
            bool status = openSerialPort();
            if (serialInterfaceOpen)
            {
                startThreads();
            }
            return status;
        }

        private bool openSerialPort()
        {
            if (serialInterfaceOpen)
            {
                return true;
            }
            string comportName = PortName;
            //var portExists = SerialPort.GetPortNames().Any(x => comportName.Equals(x));
            var portExists = Directory.GetFiles("/dev/", "tty*").Any(x => comportName.Equals(x));
            if (!portExists)
            {
                WriteMessage(string.Format("COM port {0} does not exist.", comportName));
                return false;
            }
            port = new SerialPort(comportName, _baudRate, _parity, _databits, _stopBits);
            port.ReadTimeout = 2000;
            try
            {
                port.Open();
                WriteMessage("Successfully Opened " + comportName);
                serialInterfaceOpen = true;
                return true;
            }
            catch (Exception e)
            {
                WriteMessage(string.Format("Exception occured opening {0} port. {1}", comportName, e.Message));
                return false;
            }

        }

        private bool startThreads()
        {
            if (commInterfaceRunning == true)
                return true;
            rxThread = new Thread(new ThreadStart(this.serialRxThread));
            rxThread.Name = "";
            rxThread.Priority = ThreadPriority.AboveNormal;
            rxThread.Start();
            WriteMessage("Started Serial Receive Thread");
            commInterfaceRunning = true;
            return true;

        }

        private void restartComms()
        {
            if (commInterfaceRunning == false)
            {
                // if comm interface running is false user has stopped it, so do not restart it
                return;
            }
            // error or exception occured that closed the port so restart comms
            restart_timer.Close();
            restart_timer = null;
            closeThreads();
            closePorts();
            openSerialPort();
            startThreads();
        }

        private int ReadData(byte[] buf, int start_index, int length, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return 0;

            int bytes_available;
            int bytes_read;
            bytes_available = port.BytesToRead;
            if (bytes_available <= 0)
            {
                return 0;
            }
            if ((length - start_index) < bytes_available)
            {
                bytes_available = length - start_index;
            }
            bytes_read = port.Read(buf, start_index, bytes_available);
            return bytes_read;
        }

        private int ReadDataOld(byte[] buf, int start_index, int length, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return 0;

            byte tmpByte;
            int idx = start_index;

            tmpByte = (byte)port.ReadByte();

            while (tmpByte != 255)
            {
                buf[idx] = tmpByte;
                idx++;
                if (idx >= length)
                {
                    break;
                }
                tmpByte = (byte)port.ReadByte();
            }
            //Console.WriteLine(string.Format("tmpbyte read last {0:x2}", tmpByte)); 
            return idx - start_index;
        }

        //private async void serialRxThread()
        private void serialRxThread()
        {
            byte[] buf = new byte[4096];
            int bytesRead;
            //startParsingData();
            cancelRead = new CancellationTokenSource();
            //Console.WriteLine("starting the recv loop");
            while (stopReading == false)
            {
                try
                {
                    //Console.WriteLine("before read async rxthread");
                    //bytesRead = await port.BaseStream.ReadAsync(buf, 0, buf.Length, cancelRead.Token);
                    bytesRead = ReadData(buf, 0, buf.Length, cancelRead.Token);
                    if (bytesRead > 0)
                    {
                        //Console.WriteLine(string.Format("read {0} bytes ", bytesRead));
                        byte[] received = new byte[bytesRead];
                        Buffer.BlockCopy(buf, 0, received, 0, bytesRead);
                        raiseAppSerialDataEvent(received);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }
                catch (IOException ioe)
                {
                    // thread exit or application request to exit
                    WriteMessage(string.Format("Exception receiving data. Stopped receiving data. {0}", ioe.Message));
                    stopReading = true;
                    if (!cancelRead.Token.IsCancellationRequested)
                    {
                        restart_timer = new System.Timers.Timer(2000);
                        restart_timer.AutoReset = false;
                        restart_timer.Elapsed += Restart_timer_Elapsed;
                        restart_timer.Start();
                    }
                }
                catch (Exception e)
                {
                    WriteMessage(e.StackTrace);
                    WriteMessage(e.Message);
                    stopReading = true;
                }
            }
            //Console.WriteLine("Finished the recv loop");

        }

        private void Restart_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //restartComms();
            raiseErrorOccurred((ushort)SerialErrorCodes.IOException);
        }
        #endregion //init_funcs

        #region shutdown_funcs
        public void Close()
        {
            closeThreads();
            closePorts();
        }

        private void closePorts()
        {
            if (port != null)
            {
                if (port.IsOpen)
                {
                    //port.BaseStream.Flush();
                    //GC.ReRegisterForFinalize(port.BaseStream);
                    port.Close();
                }
                port.Dispose();
                port = null;
                serialInterfaceOpen = false;
            }
        }

        private void closeThreads()
        {
            if (rxThread != null)
            {
                try
                {
                    stopReading = true;
                    if (cancelRead != null)
                        cancelRead.Cancel();
                    //rxThread.Abort();
                    rxThread.Join(500);
                }
                catch (Exception e_ignore)
                {

                }
                finally
                {
                    rxThread = null;
                }
            }
            commInterfaceRunning = false;
        }
        #endregion //shutdown_funcs


    }
}
