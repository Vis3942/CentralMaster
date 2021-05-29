using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster.Communications
{
    public class SubnetCommunications: ITransmit
    {

        #region vars
        private const int SUBNET_IDENTIFIER_POSITION = 4;
        private const int SUBNET_IDENTIFIER_LENGTH_POSITION = 2;
        private string _bridge_ipaddr = null;
        private int _bridge_port = 9001;
        private TcpListener server = null;

		private CancellationTokenSource TCPListenerTaskCTS = new CancellationTokenSource();

        public Action<byte[]> receivedData = null;

        public Action<string> errorOccured = null;

        public Action<string> writeToDisplay = null;

        public string _subnet_identifier = "SubnetDCNone";

        public byte[] _subnet_id_bytes = null;

		private AutoResetEvent response_recv_event = new AutoResetEvent(false);

        private int _timeout_response_secs = 7;

        private bool _wait_for_response = false;
		private ConcurrentDictionary<string,byte[]> Messages4UI = new ConcurrentDictionary<string, byte[]>();


        #endregion // vars

        #region property
        public string subnetdcipaddress 
        {
            get { return _bridge_ipaddr; }
        }

        public int BridgePort 
        {
            get { return _bridge_port; }
        }

        public string SubnetDCIdentifier 
        {
            get { return _subnet_identifier; }
        }

        public int TimeoutResponseSeconds 
        {
            get { return _timeout_response_secs; }
            set { _timeout_response_secs = value; }
        }
        #endregion //property

        public SubnetCommunications(string bridge_ipaddress, int bridge_port, string subnet_identifier)
        {
            _bridge_ipaddr = bridge_ipaddress;
            _bridge_port = bridge_port;
            _subnet_identifier = subnet_identifier;
            _subnet_id_bytes = System.Text.ASCIIEncoding.ASCII.GetBytes(_subnet_identifier);
            //Start();
        }

        public void Start()
        {
            IPAddress localAddr = IPAddress.Parse(_bridge_ipaddr);
            server = new TcpListener(localAddr, _bridge_port);
            server.Start();
            Task.Factory.StartNew(() =>  StartListener());
            //StartListener();
        }

        public void Stop()
        {
            TCPListenerTaskCTS.Cancel();
            Thread.Sleep(3*1000);
        }
        public void StartListener()
        {
            try
            {
                while (true)
                {
                    WriteMessage("Waiting for a connection...");
                    TcpClient client = server.AcceptTcpClient();
                    WriteMessage("Connected!");
                    Task.Run(() => HandleDevice(client, TCPListenerTaskCTS.Token));
                    //Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                    //t.Start(client);
                }
            }
            catch (SocketException e)
            {
                WriteMessage(string.Format("SocketException: {0}. Stopping communications server", e));
                server.Stop();
                server = null;
                if (!TCPListenerTaskCTS.IsCancellationRequested)
                {
                    Thread.Sleep(10*1000);
                    WriteMessage("Restarting communications server");
                    Start();
                }
            }
        }
        public async void HandleDevice(Object obj, CancellationToken cts)
        {
            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();
            string imei = String.Empty;
            //byte[] recv_bytes = new byte[3*1500];
            //int recv_bytes_index;
            byte[] bytes = new byte[2048];
            int numBytesRead;
            int pkt_len;
            bool sts; 
            try
            {
                //recv_bytes_index = 0;
                while (!cts.IsCancellationRequested)
                {
                    numBytesRead = await stream.ReadAsync(bytes, 0, 2048, cts);
                    DCLogger.Debug(" ------------xxxxxxxxxxxxxxxxxxxxxxx--------------------");
                    DCLogger.Debug(string.Format(" bytes read = {0}", numBytesRead));
                    DCLogger.Debug(BitConverter.ToString(bytes, 0, numBytesRead));
                    DCLogger.Debug("-------------xxxxxxxxxxxxxxxxxxxxxxx--------------------");
                    if (numBytesRead <= 0)
                    {
                        client.Close();
                        return;
                    }
                    else 
                    {
                        pkt_len = BitConverter.ToUInt16(bytes, 0);
                        DCLogger.Debug(string.Format(" num_bytesRead = {0}", numBytesRead));
                        DCLogger.Debug(BitConverter.ToString(bytes, 0, numBytesRead));
                        DCLogger.Information(string.Format(" recvd from bridge node: pkt len {0}", pkt_len));
                        if (numBytesRead == pkt_len)
                        {
                            if (pkt_len == ("subnetdcname?".Length + 2))
                            {
                                string tmpstr = System.Text.ASCIIEncoding.ASCII.GetString(bytes, 2, pkt_len - 2);
                                if (tmpstr.ToLower().Equals("subnetdcname?"))
                                {
                                    DCLogger.Information(string.Format("recv req = {0} ", tmpstr));
                                    DCLogger.Information(_subnet_identifier);
                                    byte[] name_resp_b = new byte[_subnet_identifier.Length + 2];
                                    byte[] tmp_name_len_b = BitConverter.GetBytes((ushort)name_resp_b.Length);
                                    name_resp_b[0] = tmp_name_len_b[0];
                                    name_resp_b[1] = tmp_name_len_b[1];
                                    System.Array.Copy(_subnet_id_bytes, 0, name_resp_b, 2, _subnet_id_bytes.Length);
                                    int length = name_resp_b.Length;
                                    await stream.WriteAsync(name_resp_b, 0, length);
                                    continue;// return;
                                }
                            }
                            // format ofdata received <uint16 packet len>, <uint16 subnet dc length>, 
                            //                    <subnet dc identifier bytes> , <data packet....>
                            //    {data packet} => <ipv6 addres>, <uint16 commonname;serialnum length>, 
                            //                    <common name; serial number bytes>, <message>
                            //    {message} => <uint16 lenth of message including this 2 bytes>, <command id>,
                            //                    <data corresponding to command id>

                            // confirm the identifer is equal, else discard
                            //int cmd_id = BitConverter.ToUInt16(bytes, 2);
                            int id_len = BitConverter.ToUInt16(bytes, SUBNET_IDENTIFIER_LENGTH_POSITION);
                            DCLogger.Debug(string.Format(" id_len recvd = {0}", id_len-2));
                            string id_str = System.Text.Encoding.UTF8.GetString(bytes, SUBNET_IDENTIFIER_POSITION, id_len-2);
                            DCLogger.Debug(string.Format("id str len = {0}", id_str.Length));
                            WriteMessage(string.Format("received message from BridgeNode: subnet id: {0} ", id_str));
                            if (!id_str.Equals(_subnet_identifier))
                            {
                                WriteMessage(string.Format("message meant for subnet dc: {0}, received by {1}", id_str, _subnet_identifier));
                                continue; //return;
                            }
                            // if it is command request, then wait for command response 
                            // if it is data request, then pipe any data available, 
                            //              else return empty data packet (to handle this case in the user interface)
                            //IMPORTANT at this point we expect only a single data request or command request
                            int recv_b_len = pkt_len - 2 - id_len; // 2 for overall len, 2 for id length
                            if (recv_b_len > 0) 
                            {
                                byte[] recv_b = new byte[recv_b_len];
                                System.Array.Copy(bytes, id_len + 2, recv_b, 0, recv_b_len);

                                raiseAppDataEvent(recv_b); // Note: ASSSUMTION: USER INTERFACE WILL ONLY PACKAGE ONE MESSAGE (COMMAND REQUEST / DATA REQUEST)
                                response_recv_event.Reset();
                                _wait_for_response = true;
                                sts = response_recv_event.WaitOne(TimeoutResponseSeconds*1000);
                                if (sts)
                                {
                                    // response received
                                    // debug
                                    WriteMessage("SubnetDC response received");
                                }
                                else
                                {
                                    // response timeout
                                    WriteMessage("SubnetDC response TIMEOUT");
                                }
                            }
                            string[] ipv6_keys = Messages4UI.Keys.ToArray();
                            int send_data_len = 0;
                            using(System.IO.MemoryStream memStream = new System.IO.MemoryStream())
                            {
                                foreach(string ipv6_key in ipv6_keys)
                                {
                                    byte[] to_b;
                                    sts = Messages4UI.TryRemove(ipv6_key, out to_b);
                                    if (sts)
                                    {
                                        memStream.Write(to_b, 0, to_b.Length);
                                        send_data_len += to_b.Length;
                                    }
                                }
                                byte[] from_mem_b = memStream.ToArray();
                                // 2 for first length bytes, 2 for message identifier, 2 for subnet identifier length
                                int pre_length =2+2+_subnet_identifier.Length ;
                                byte[] tmp_b = new byte[pre_length + from_mem_b.Length];
                                if (from_mem_b.Length > 0)
                                {
                                    System.Array.Copy(from_mem_b, 0, tmp_b, pre_length, from_mem_b.Length);
                                }
                                byte[] tmp_1 = BitConverter.GetBytes(send_data_len + pre_length);
                                tmp_b[0] = tmp_1[0];
                                tmp_b[1] = tmp_1[1];
                                //tmp_1 = BitConverter.GetBytes(CentralMaster.ProtocolMessagesG3PLC.BRIDGENODE_DATA_RESPONSE_ID);
                                //tmp_b[2] = tmp_1[0];
                                //tmp_b[3] = tmp_1[1];
                                tmp_1 = BitConverter.GetBytes(_subnet_identifier.Length+2);
                                tmp_b[SUBNET_IDENTIFIER_LENGTH_POSITION] = tmp_1[0];
                                tmp_b[SUBNET_IDENTIFIER_LENGTH_POSITION+1] = tmp_1[1];
                                byte[] tmp_2 = System.Text.Encoding.ASCII.GetBytes(_subnet_identifier);
                                System.Array.Copy(tmp_2, 0, tmp_b, SUBNET_IDENTIFIER_POSITION, tmp_2.Length);
                                DCLogger.Information(string.Format("writing to brudge node len = {0}", tmp_b.Length));
                                await stream.WriteAsync(tmp_b, 0, tmp_b.Length);
                            }
                        }
                        else
                        {
                            WriteMessage(string.Format("Subnet comms data: {0}\n, recv len = {1}, computed pkt len {2}",
                                BitConverter.ToString(bytes), numBytesRead, pkt_len));
                            WriteMessage("in stream read");
                        }
                    }
                }
            }
            catch(Exception e)
            {
                WriteMessage(string.Format("cient communications Exception: {0}", e.ToString()));
                client.Close();
            }
        }

		private void WriteMessage(string s)
        {
            if (writeToDisplay != null)
                writeToDisplay(s);
            else
                DCLogger.Information(s);
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

        public void transmit(byte[] to_send)
        {
            string ipv6_str = BitConverter.ToString(to_send, 0, 16);
            DCLogger.Information(BitConverter.ToString(to_send));
            Messages4UI.AddOrUpdate(ipv6_str, to_send, (key, oldValue) => { return to_send; } ); 
            if (_wait_for_response == true)
            {
                _wait_for_response = false;
                response_recv_event.Set();
            }
        }

    }
}
