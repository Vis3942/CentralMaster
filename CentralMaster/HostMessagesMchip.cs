using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CentralMaster.Communications;
using LogUtils;

namespace CentralMaster
{
    public class HostMessagesMchip
    {
        // set CheckResponse on this class if resp required
        // then send messages
        // then await for checkReceivedMessageAsync


        #region vars

        private UdpMchipG3Comm _udp_conn = null;
        private bool UDPConnectionReady = false;

        // vars supporting property
        //private bool _checkResponse;

        private byte[] tmpQueueBuf = new byte[4096];
        private int tmpQueueBufUsedLength = 0;
        //private int remainingMessageLength = 0;
        private CancellationTokenSource cancelParsingMessages = new CancellationTokenSource();
        private BlockingCollection<byte[]> receiveQueue = new BlockingCollection<byte[]>();
        private BlockingCollection<EndPoint> receiveEndPoint = new BlockingCollection<EndPoint>();
        //private BlockingCollection<MessageHeader> recvMsgs = new BlockingCollection<MessageHeader>();

        public Action<string> writeToDisplay = null;
        public Action<NodeAggregateMessage> receivedDataTransferIndication = null;
        public Action<string> receivedErrorNotification = null;
        private BlockingCollection<byte[]> transmitQueue = new BlockingCollection<byte[]>();
        private BlockingCollection<byte[]> transmitIPV6Queue = new BlockingCollection<byte[]>();
        private CancellationTokenSource cancelTransmitQueue = new CancellationTokenSource();
        //private BackgroundWorker transmitWorker = null;
        private Task transmitTask = null;

        private const int RECEIVE_MESSAGE_CHECK_TIMEOUT_MS = 25;

        #endregion vars

        #region property
        //public bool CheckResponse
        //{
        //    get
        //    {
        //        return _checkResponse;
        //    }
        //    set
        //    {
        //        _checkResponse = value;
        //    }
        //}

        public UdpMchipG3Comm DeviceUDPConnection
        {
            get
            {
                return _udp_conn;
            }
            set
            {
                if (_udp_conn != null)
                {
                    UDPConnectionClose();
                }
                _udp_conn = value;
                UDPInterfaceOpen();
            }
        }

        #endregion property

        private void udpDataReceivedEvent(byte[] received, int recv_length, EndPoint remoteEP)
        {
            DCLogger.Verbose(string.Format("recv -> {0}", recv_length));
            if (received == null)
            {
                return;
            }
            if (recv_length == 0)
            {
                return;
            }

            // copy received data to buffer
            // if buf is insufficient reallocate it; something wrong ... going on

            Array.Copy(received, 0, tmpQueueBuf, tmpQueueBufUsedLength, recv_length);
            tmpQueueBufUsedLength += recv_length;
            // now process the data
            // the first 2 bytes is length, then it could contain multiple commands / responses
            bool done = false;
            int msglen;
            while (!done)
            {
                // is valid message length data available if yes copy it and delete it from tmpQuueuBuf
                if (tmpQueueBufUsedLength <= 4)
                {
                    done = true;
                    break;
                }
                msglen = (int)BitConverter.ToUInt16(tmpQueueBuf, 0);
                // may not be necessary for Microchip case
                //if ((msglen % 2) != 0)
                //{
                //    msglen = msglen + 1;
                //}
                if (tmpQueueBufUsedLength >= msglen)
                {
                    // valid packat available
                    byte[] forMsgQ = new byte[msglen];
                    Array.Copy(tmpQueueBuf, 0, forMsgQ, 0, msglen);
                    addToReceivedMessagesQueue(forMsgQ, remoteEP);
                    // delete data from tmpQueueBuf
                    int tocopylen = tmpQueueBufUsedLength - msglen; // - 4;
                    if (tocopylen > 0)
                    {
                        byte[] tmpBuf = new byte[tocopylen];
                        Array.Copy(tmpQueueBuf, msglen, tmpBuf, 0, tocopylen);
                        Array.Copy(tmpBuf, 0, tmpQueueBuf, 0, tocopylen);
                    }
                    tmpQueueBufUsedLength = tocopylen;
                }
                else
                {
                    // discard value
                    tmpQueueBufUsedLength = 0;
                    done = true;
                }
            }
        }


        private void UDPInterfaceOpen()
        {
            if (UDPConnectionReady)
                return;

            if (_udp_conn == null)
                return;

            _udp_conn.receivedData += udpDataReceivedEvent;
            _udp_conn.errorOccured += NotifyErrorOccured;
            startParsingData();
            //StartTransmitBackgroundProcess();
            StartTransmitTask();
            UDPConnectionReady = true;
        }

        private void UDPConnectionClose()
        {
            if (!UDPConnectionReady)
                return;

            _udp_conn.receivedData -= udpDataReceivedEvent;
            _udp_conn.errorOccured -= NotifyErrorOccured;
            //StopTransmitBackgroundProcess();
            StopTransmitTask();
            UDPConnectionReady = false;
        }

        private bool addToReceivedMessagesQueue(byte[] msgRecvd, EndPoint remoteEP)
        {
            try
            {
                // debug only; delete after that
                int tmplen;
                if (msgRecvd == null)
                {
                    tmplen = -1;
                }
                else
                {
                    tmplen = msgRecvd.Length;
                }
                //DCLogger.Debug($"add to recvd msgQ len= {tmplen} from {((IPEndPoint)remoteEP).Address}");
                receiveQueue.Add(msgRecvd, cancelParsingMessages.Token);
                receiveEndPoint.Add(remoteEP, cancelParsingMessages.Token);
            }
            catch (Exception e)
            {
                DCLogger.Error(e, "adding recvd message to Queue");
                return false;
            }
            return true;
        }

        private void startParsingData()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] processDatab = receiveQueue.Take(cancelParsingMessages.Token);
                        EndPoint processDataEndPoint = receiveEndPoint.Take(cancelParsingMessages.Token);
                        //RollingFileLogger.Verbose(string.Format("rx Queue command = 0x{0:x2}, ", processDatab[0]));
                        DCLogger.Information($"data recv len {processDatab.Length} from addr {((IPEndPoint)processDataEndPoint).Address}");
                        ProcessReceivedMessage(processDatab, processDataEndPoint);
                    }
                    catch (Exception e)
                    {
                        //WriteMessage(e.Message);
                        DCLogger.Error(e, "processing data: ");
                        DCLogger.Error(e.StackTrace);
                        // restart
                    }
                }
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent);
        }

        private void ProcessReceivedMessage(byte[] message_bytes, EndPoint remoteEP)
        {
            DCLogger.Information("start of process Recvd Message");
            if ( (message_bytes == null) || (message_bytes.Length == 0) )
            {
                DCLogger.Information(string.Format("recv message is null or zero length at ProcessReceivedMessage (HostMessageMchip.cs) at {0}", message_bytes[0], message_bytes.Length, DateTime.Now.TimeOfDay.ToString()));
                return;
            }
            DCLogger.Information(string.Format("recv message type => 0x{0:x} of length {1}, at {2}", message_bytes[0], message_bytes.Length, DateTime.Now.TimeOfDay.ToString()));
            NodeAggregateMessage nam = new NodeAggregateMessage(message_bytes, remoteEP);
            NotifyDataTransferIndication(nam);
        }




        #region send_messages

        public int TransmitQueueLength()
        {
            return transmitQueue.Count;
        }

        private void SafeSendMessageOnG3TUN(byte[] tosendb, byte[] to_ipv6_addr)
        {
            if (tosendb != null)
            {
                // debug only
                WriteMessage(string.Format("sent message type => 0x{0:x} at {1}", tosendb[0], DateTime.Now.TimeOfDay.ToString()));
                WriteMessage("Adding to transmit queue");
                transmitQueue.Add(tosendb);
                transmitIPV6Queue.Add(to_ipv6_addr);
            }
        }

        private void sendMessageOnG3TUN(byte[] tosendb, byte[] to_ipv6_addr)
        {
            if (_udp_conn == null)
                return; //false;
            string tosendstr = BitConverter.ToString(tosendb).Replace("-", " ");
            WriteMessage(tosendstr);
            _udp_conn.transmit(tosendb, to_ipv6_addr);
            //return true;
        }




        public bool SendDataToNode(byte[] to_send_b, byte[] dest_ipv6_addr)
        {
            string ipv6_str = BitConverter.ToString(dest_ipv6_addr);
            WriteMessage(string.Format("Send Data {0} \nto node {1}", BitConverter.ToString(to_send_b).Replace("-", ""), BitConverter.ToString(dest_ipv6_addr)));
            SafeSendMessageOnG3TUN(to_send_b, dest_ipv6_addr);
            return true;
        }

        #endregion // send_messages

        #region concurrent_funcs
        //private void TransmitDataTask(object sender, DoWorkEventArgs e) // when used with BackgroundWorker
        private void TransmitDataTask()
        {
            while (!cancelTransmitQueue.IsCancellationRequested)
            {
                try
                {
                    byte[] message_2_send;
                    byte[] to_ipv6_addr;
                    //WriteMessage("get data from Transmit queue. before take.");
                    message_2_send = transmitQueue.Take(cancelTransmitQueue.Token);
                    to_ipv6_addr = transmitIPV6Queue.Take(cancelTransmitQueue.Token);
                    //WriteMessage("got data from Transmit queue. Put on serial bus.");
                    sendMessageOnG3TUN(message_2_send, to_ipv6_addr);
                }
                catch (Exception excp)
                {
                    WriteMessage(excp.Message);
                    // perform loop again
                }
            }
        }

        private void StartTransmitTask()
        {
            if (transmitTask != null)
            {
                StopTransmitTask();
            }
            cancelTransmitQueue = new CancellationTokenSource();
            transmitTask = Task.Factory.StartNew(() => TransmitDataTask(), cancelTransmitQueue.Token, TaskCreationOptions.AttachedToParent, TaskScheduler.Current);
        }

        private void StopTransmitTask()
        {
            if (transmitTask != null)
            {
                cancelTransmitQueue.Cancel();
                transmitTask.Wait(60 * 1000); // should exit within a minute
                transmitTask = null;
            }
        }

        //private void StartTransmitBackgroundProcess()
        //{
        //    if (transmitWorker != null)
        //    {
        //        // need to stop it
        //        StopTransmitBackgroundProcess();
        //    }
        //    cancelTransmitQueue = new CancellationTokenSource();
        //    transmitWorker = new BackgroundWorker();
        //    transmitWorker.DoWork += TransmitDataTask;
        //    transmitWorker.WorkerSupportsCancellation = true;
        //    transmitWorker.RunWorkerAsync();
        //}

        //private void StopTransmitBackgroundProcess()
        //{
        //    cancelTransmitQueue.Cancel( true );
        //    Thread.Sleep(450); // give time for loop to quit
        //    transmitWorker.DoWork -= TransmitDataTask;
        //    transmitWorker = null;
        //}

        #endregion // concurrent_funcs

        #region utility_funcs
        private void WriteMessage(string message)
        {
            //if (writeToDisplay != null)
            //    writeToDisplay(s);
            //else
            //{
            DCLogger.Information(message);
            //}
        }


        private void NotifyDataTransferIndication(NodeAggregateMessage nam)
        {
            if (nam != null)
            {
                receivedDataTransferIndication(nam);
            }
            else
                DCLogger.Information(string.Format($"To notify data received {0}, from {1}", nam.data_recv.ToString(), nam.fromEP.Address.ToString()));
        }

        private void NotifyErrorOccured(string error_str)
        {
            receivedErrorNotification?.Invoke(error_str);
        }

        #endregion // utility_funcs

    }

    public class NodeAggregateMessage
    {
        public byte[] data_recv;
        public IPEndPoint fromEP;
        public NodeAggregateMessage(byte[] data, EndPoint remoteEP)
        {
            data_recv = new byte[data.Length];
            Array.Copy(data, 0, data_recv, 0, data.Length);
            IPEndPoint tmpEP = (IPEndPoint)remoteEP;
            fromEP = new IPEndPoint(tmpEP.Address, tmpEP.Port);
        }
    }



}
