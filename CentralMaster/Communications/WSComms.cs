using CentralMaster.Communications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.Communications
{
    public class WSComms : ITransmit
    {
        #region vars
        public Action<string> writeToDisplay = null;
        public Action<byte[]> receivedData = null;
        public Action<string> errorOccured = null;

        private string _host_addr = "162.243.185.22";
        private int _host_port = 8181;
        private string _channel_comm = "channelOne";

        #endregion // vars

        public WSComms()
        {
        }



        #region properties
        public string HostAddress
        {
            get
            {
                return _host_addr;
            }
            set
            {
                _host_addr = value;
            }
        }

        public int HostPort
        {
            get
            {
                return _host_port;
            }
            set
            {
                _host_port = value;
            }
        }

        public string ChannelName
        {
            get
            {
                return _channel_comm;
            }
            set
            {
                _channel_comm = value;
            }
        }


        #endregion // properties

        #region utility_funcs
        private void WriteMessage(string s)
        {
            if (writeToDisplay != null)
                writeToDisplay(s);
            else
                Console.WriteLine(s);
        }

        private void raiseAppDataEvent(byte[] received)
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
                WriteMessage(BitConverter.ToString(received));
                receivedData(received);
            }
            else
            {
                string disp_str = BitConverter.ToString(received).Replace("-", " ");
                WriteMessage(disp_str);
            }
        }

        private void raiseErrorOccurred(string error_str)
        {
            errorOccured?.Invoke(error_str);
        }


        public void transmit(byte[] tosendb)
        {
            Console.WriteLine("in transmit function ws comms ");
            Console.WriteLine(BitConverter.ToString(tosendb).Replace("-", " "));
        }

        public void transmit(byte[] tosendb, int startidx, int length)
        {
            byte[] tosend_data = new byte[length];
            System.Array.Copy(tosendb, startidx, tosend_data, 0, length);
            transmit(tosend_data);
        }

        public void transmit(string tosend_str)
        {
        }
        #endregion //utility_funcs

        #region init_close_funcs
        public void Start()
        {
        }

        public bool IsConnected()
        {
            // TODO:
            return false;
        }

        public void Stop()
        {
        }
        #endregion // init_close_funcs
    }
}
