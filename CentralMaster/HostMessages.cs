using CentralMaster.Communications;
using CentralMaster.ip;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogUtils;

enum CommandTypes : byte
{
    DATA_TRANSFER = 0x00,
    GET_SYSTEM_INFO = 0x01,
    GET_PHY_PIB = 0x02,
    GET_MAC_PIB = 0x03,
    SET_INFO = 0x04,
    SHUTDOWN = 0x05,
    SETUP_ALARM = 0x06,
    ALARM = 0x07,
    NETWORK_START = 0x08,
    LOAD_SYSTEM_CONFIG = 0x0c,
    SET_MAC_PIB = 0x0d,
    CLEAR_PHY_PIB = 0x0e,
    CLEAR_MAC_PIB = 0x0f,
    ATTACH = 0x10,
    DETACH = 0x11,
    DISCOVER = 0x12,
    FW_UPGRADE = 0x13,
    GET_INFO = 0x14 
}


enum LoadConfigTypes : ushort
{
    Port_Designation = 0x0001,
    System_Configuration = 0x0003,
    PHY_Configuration = 0x0004,
    MAC_Configuration = 0x0005,
    G3_Configuration = 0x0008,
    Reserved1 = 0x0a,
    Reserved2 = 0x0b
}


enum DeviceModes : byte
{
    Normal_Mode = 0x00,
    MAC_Mode = 0x01,
    P2P_Mode = 0x03,
    G3_Embedded_AppEnm = 0x07
}

enum SetInfoTypes : ushort
{
    Reserved0 = 0x0000,
    Reserver1 = 0x0001,
    G3_Phy_Tx = 0x0002,
    G3_Phy_Rx = 0x0003,
    Vendor_Product_Ids = 0x0004,
    Tx_Phy_AttenuationGain_Params = 0x0009
}

enum ShutdownTypes: ushort
{
    SoftReset = 0x0000,
    SoftShutdown = 0x0001
}


enum ModulationTypes: byte
{
    ROBO = 0x00,
    BPSK = 0x01,
    QPSK = 0x02,
    EightPSK = 0x03,
    Reserved = 0x04,
    SUPER_ROBO = 0x05
}

enum DiscoverTypes: byte
{
    NETWORK = 0x00,
    ROUTE = 0x01,
    PATH = 0x02,
    EXHAUSTIVE_BEACON = 0x03
}

enum RootNodeStates: int
{
    UNINITIALIZED,
    STEP00_INIT, // always start in this state
    STEP01_GET_SYSTEM_INFO,
    STEP02_CONFIG_PORTS,
    STEP03_LOAD_SYSTEM_CONFIG,
    STEP04_LOAD_G3_SYSTEM_CONFIG,
    STEP04A_SET_MAC_PIB_PSK, // SKIP THIS STEP
    STEP05_SHUTDOWN,
    STEP06_GET_SYSTEM_INFO_AFTER_SD, // this is after shutdown
    STEP07_SET_VENDOR_IDS,
    STEP08_SET_TX_PARAMS,
    STEP09_SET_RX_PARAMS,
    STEP10A_M_INITIALIZING_NETWORK, // For master/base node
    STEP10B_M_SET_ALARM, // For master/base node
    STEP10C_M_SET_PSK, // TODO: For master/base node
    STEP10D_M_SET_MAC_PIB_COH, // for master node, same command will occur for service node at a different step in the sequence
    STEP10E_M_START_NETWORK, // FOR MASTER/Base NODE
    STEP11_M_NETWORK_START_SUCCESS,
    STEP12_M_NETWORK_READY,
    STEP10A_S_SET_PSK, // TODO: FOR Service NODE
    STEP10C_S_SET_MAC_PIB_COH,
    STEP10D_S_DISCOVER_NETWORK, // FOR Service NODE
    STEP11_S_ATTACH_REQUEST, // FOR Service NODE
    STEP12_S_ATTACHING,
    STEP12_S_ATTACH_CANCELLED,
    STEP13_S_NODE_READY
}

enum AlarmTypes: ushort
{
    G3_NETWORK_STATUS=0x0007,
    GENERAL_ERROR = 0x00FF
}


enum ErrorInformation: ushort
{
    PORT_DISCARDED = 0x0001,
    HEADER_UNSUPPORTED_FEATURE = 0x0003,
    MESSAGE_DISCARDED = 0x0004,
    INVALID_MESSAGE_FORMAT = 0x0008
}

enum MACPIBIds : uint
{
    NEIGHBOUR_TABLE = 0x006e
}

namespace CentralMaster
{
    public class HostMessages
    {

        // set CheckResponse on this class if resp required
        // then send messages
        // then await for checkReceivedMessageAsync


        #region vars

        private SerialComm _serial_conn = null;
        private bool serialInterfaceReady = false;

        // vars supporting property
        //private bool _checkResponse;

        private byte[] tmpQueueBuf = new byte[4096];
        private int tmpQueueBufUsedLength = 0;
        //private int remainingMessageLength = 0;
        private List<byte> commandValues = Enum.GetValues(typeof(CommandTypes)).Cast<byte>().ToList<byte>();
        private CancellationTokenSource cancelParsingMessages = new CancellationTokenSource();
        private BlockingCollection<byte[]> receiveQueue = new BlockingCollection<byte[]>();
        //private BlockingCollection<MessageHeader> recvMsgs = new BlockingCollection<MessageHeader>();

        public Action<string> writeToDisplay = null;
        public Action<AttachIndication> receivedNodeAttach = null;
        public Action<DetachIndication> receivedNodeDetach = null;
        public Action<DataTransferIndication> receivedDataTransferIndication = null;
        public Action<ushort> receivedErrorNotification = null;
        public Action<byte, ushort, MessageHeader> receivedMessage = null;
        public Action<DataTransferConfirm> receivedDataTransferConfirm = null;
        private BlockingCollection<byte[]> transmitQueue = new BlockingCollection<byte[]>();
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

        public SerialComm SerialConnection
        {
            get
            {
                return _serial_conn;
            }
            set
            {
                if (_serial_conn != null)
                {
                    SerialInterfaceClose();
                }
                _serial_conn = value;
                SerialInterfaceOpen();
            }
        }

        #endregion property

        private void serialDataReceivedEvent(byte[] received)
        {
            DCLogger.Verbose(string.Format("recv -> {0}", received.Length));
            if (received == null)
            {
                return;
            }
            if (received.Length == 0)
            {
                return;
            }
            
            // copy received data to buffer
            // if buf is insufficient reallocate it; something wrong ... going on

            Array.Copy(received, 0, tmpQueueBuf, tmpQueueBufUsedLength, received.Length);
            tmpQueueBufUsedLength += received.Length;
            // now process the data
            // if the first byte is command set, then process it, else something is wrong....
            // that should never happen
            bool done = false;
            int msglen;
            while (!done)
            {
                if (commandValues.Contains(tmpQueueBuf[0]))
                {
                    // found valid command value
                    // is valid message length data available if yes copy it and delete it from tmpQuueuBuf
                    if (tmpQueueBufUsedLength <= 4)
                    {
                        done = true;
                        break;
                    }
                    msglen = (int)BitConverter.ToUInt16(tmpQueueBuf, 2);
                    if ( (msglen % 2) != 0)
                    {
                        msglen = msglen + 1;
                    }
                    if (tmpQueueBufUsedLength >= (msglen + 4))
                    {
                        // valid packat available
                        byte[] forMsgQ = new byte[msglen + 4];
                        Array.Copy(tmpQueueBuf, 0, forMsgQ, 0, msglen + 4);
                        addToReceivedMessagesQueue(forMsgQ);
                        // delete data from tmpQueueBuf
                        int tocopylen  = tmpQueueBufUsedLength - msglen - 4;
                        byte[] tmpBuf = new byte[tocopylen];
                        Array.Copy(tmpQueueBuf, msglen + 4, tmpBuf, 0, tocopylen);
                        Array.Copy(tmpBuf, 0, tmpQueueBuf, 0, tocopylen);
                        tmpQueueBufUsedLength = tocopylen;
                    }
                    else
                    {
                        done = true;
                    }

                }
                else
                {
                    // discard value
                    tmpQueueBufUsedLength = 0;
                    done = true;
                }
            }
        }


        private void SerialInterfaceOpen()
        {
            if (serialInterfaceReady)
                return;

            if (_serial_conn == null)
                return;

            _serial_conn.receivedData += serialDataReceivedEvent;
            _serial_conn.errorOccured += NotifyErrorOccured;
            startParsingData();
            //StartTransmitBackgroundProcess();
            StartTransmitTask();
            serialInterfaceReady = true;
        }

        private void SerialInterfaceClose()
        {
            if (!serialInterfaceReady)
                return;

            _serial_conn.receivedData -= serialDataReceivedEvent;
            _serial_conn.errorOccured -= NotifyErrorOccured;
            //StopTransmitBackgroundProcess();
            StopTransmitTask();
            serialInterfaceReady = false;
        }

        private bool addToReceivedMessagesQueue(byte[] msgRecvd)
        {
            try
            {
                receiveQueue.Add(msgRecvd, cancelParsingMessages.Token);
            }
            catch (Exception e)
            {
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
                    	//RollingFileLogger.Verbose(string.Format("rx Queue command = 0x{0:x2}, ", processDatab[0]));
                        ProcessReceivedMessage(processDatab);
                    }
                    catch (Exception e)
                    {
                        WriteMessage(e.Message);
                        // restart
                    }
                }
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent);
        }

        private void ProcessReceivedMessage(byte[] message_bytes)
        {
            //WriteMessage(string.Format("recv message type => 0x{0:x} at {1}", message_bytes[0], DateTime.Now.TimeOfDay.ToString())); 
            DCLogger.Information(string.Format("recv message type => 0x{0:x} at {1}", message_bytes[0], DateTime.Now.TimeOfDay.ToString()));
            switch (message_bytes[0])
            {
                case (byte)CommandTypes.GET_SYSTEM_INFO:
                    GetSystemInfoResponse gsif = new GetSystemInfoResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, gsif);

                    break;
                case (byte)CommandTypes.LOAD_SYSTEM_CONFIG:
                    LoadSystemConfigResponse glcs = new LoadSystemConfigResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, glcs);
                    break;

                case (byte)CommandTypes.SHUTDOWN:
                    ShutDownResponse sdr = new ShutDownResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, sdr);
                    break;

                case (byte)CommandTypes.SET_INFO:
                    SetInfoResponse sir = new SetInfoResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, sir);
                    break;

                case (byte)CommandTypes.NETWORK_START:
                    NetworkStartConfirm nsc = new NetworkStartConfirm(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, nsc);
                    break;

                case (byte)CommandTypes.ATTACH:
                    if ((message_bytes[2] == 30) && (message_bytes[3] == 0))
                    {
                        AttachIndication ai = new AttachIndication(message_bytes);
                        NotifyAttachIndication(ai);
                    }
                    else
                    {
                        // it is an attach confirmation
                        AttachConfirm ac = new AttachConfirm(message_bytes);
                        NotifyMessageReceived(message_bytes[0], 0, ac);
                    }
                    break;

                case (byte)CommandTypes.DETACH:
                    if ((message_bytes[2] == 12) && (message_bytes[3] == 0))
                    {
                        DetachIndication di = new DetachIndication(message_bytes);
                        NotifyDetachIndication(di);
                    }
                    else
                    {
                        DetachConfirm dc = new DetachConfirm(message_bytes);
                        NotifyMessageReceived(message_bytes[0], 0, dc);
                    }
                    break;

                case (byte)CommandTypes.DATA_TRANSFER:
                    // if payload len is 8 it is a data transfer confirmation else it is a data indication
                    if ((message_bytes[2] == 8) && (message_bytes[3] == 0))
                    {
                        DataTransferConfirm dtc = new DataTransferConfirm(message_bytes);
                        NotifyDataTransferConfirm(dtc);
                        //NotifyMessageReceived(message_bytes[0], 0, dtc);
                    }
                    else
                    {
                        DataTransferIndication dti = new DataTransferIndication(message_bytes);
                        NotifyDataTransferIndication(dti);
                    }
                    break;

                case (byte)CommandTypes.DISCOVER:
                    if (message_bytes[9] == (byte)DiscoverTypes.NETWORK)
                    {
                        DiscoverConfirmSubNetwork dcsn = new DiscoverConfirmSubNetwork(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort) DiscoverTypes.NETWORK, dcsn);
                    }
                    else if (message_bytes[9] == (byte)DiscoverTypes.ROUTE)
                    {
                        DiscoverConfirmSubRouteDiscovery dcsrd = new DiscoverConfirmSubRouteDiscovery(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort)DiscoverTypes.ROUTE, dcsrd);
                    }
                    else if (message_bytes[9] == (byte)DiscoverTypes.PATH)
                    {
                        DiscoverConfirmSubPathDiscovery dcspd = new DiscoverConfirmSubPathDiscovery(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort)DiscoverTypes.PATH, dcspd);
                    }
                    else if (message_bytes[9] == (byte)DiscoverTypes.EXHAUSTIVE_BEACON)
                    {
                        DiscoverConfirmExhaustiveBeacon dceb = new DiscoverConfirmExhaustiveBeacon(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort)DiscoverTypes.EXHAUSTIVE_BEACON, dceb);
                    }

                    // To
                    break;
                case (byte) CommandTypes.GET_PHY_PIB:
                    GetPHYPIBResponse gppr = new GetPHYPIBResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, gppr);
                    break;
                case (byte)CommandTypes.GET_MAC_PIB:
                    GetMACPIBResponse gmpr = new GetMACPIBResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, gmpr);
                    break;
                case (byte)CommandTypes.SET_MAC_PIB:
                    SetMACPIBResponse smpr = new SetMACPIBResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, smpr);
                    break;
                case (byte)CommandTypes.SETUP_ALARM:
                    SetAlarmResponse sar = new SetAlarmResponse(message_bytes);
                    NotifyMessageReceived(message_bytes[0], 0, sar);
                    break;
                case (byte)CommandTypes.ALARM:
                    if ( ( message_bytes[9] == 0x07 ) && (message_bytes[8] == 0x00) )
                    {
                        AlarmSubNetworkStatus asns = new AlarmSubNetworkStatus(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort)AlarmTypes.G3_NETWORK_STATUS, asns);
                        break;
                    }
                    else if ( ( message_bytes[9] == 0xFF ) && (message_bytes[8] == 0x00) )
                    {
                        AlarmSubGeneralError asge = new AlarmSubGeneralError(message_bytes);
                        NotifyMessageReceived(message_bytes[0], (ushort)AlarmTypes.GENERAL_ERROR, asge);
                        break;
                    }
                    else
                    {
                        Alarm alarm = new Alarm(message_bytes);
                        NotifyMessageReceived(message_bytes[0], 0, alarm);
                        break;
                    }


            }
        }




        #region send_messages

		public int TransmitQueueLength()
		{
			return transmitQueue.Count;
		}

        private void SafeSendMessageOnSerial(byte[] tosendb)
        {
            if (tosendb != null)
            {
                // debug only
                WriteMessage(string.Format("sent message type => 0x{0:x} at {1}", tosendb[0], DateTime.Now.TimeOfDay.ToString()));
                WriteMessage("Adding to transmit queue");
                transmitQueue.Add(tosendb);
            }
        }

        private void sendMessageOnSerial(byte[] tosendb)
        {
            if (_serial_conn == null)
                return; //false;
            string tosendstr = BitConverter.ToString(tosendb).Replace("-", " ");
            WriteMessage(tosendstr);
            _serial_conn.transmit(tosendb);
            //return true;
        }

        public bool SendCustomMessage(byte[] tosend)
        {
            WriteMessage("Send Custom msg -> " + BitConverter.ToString(tosend).Replace("-", ""));
            SafeSendMessageOnSerial(tosend);
            return true;
        }

        public bool SendGetSystemInfoRequest()
        {

            GetSystemInfoRequest gsir = new GetSystemInfoRequest();
            byte[] tosendb = gsir.ToBytes();
            WriteMessage("Send Get Information Request");
            //WriteMessage(gsir.ToString());
            //SafeSendMessageOnSerial(new byte[] { 0xF9, 0xF2 });
            //Thread.Sleep(250);
            //SafeSendMessageOnSerial(tosendb);
            //Thread.Sleep(3000);
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendLoadSystemConfigPorts(int data_port, int diag_port)
        {
            // data_port or diag_port can take values of 0 => SCI-A or 1 => SCI-B
            if ( ( (data_port == 1) || (data_port == 0) ) && ( (diag_port == 1) || ( diag_port == 0) ) ) 
            {
                // valid data continue
                LoadSystemConfigCommand lscc = new LoadSystemConfigCommand((ushort)LoadConfigTypes.Port_Designation, new object[] { (byte)data_port, (byte)diag_port });
                byte[] tosendb = lscc.ToBytes();
                WriteMessage("Send Load System Config: Ports Designation");
                SafeSendMessageOnSerial(tosendb);
                return true;
            }
            else
            {
                // invalid port data; return false or throw exception
                throw new Exception(string.Format("Invalid port values. Expected values of data or diag port are 0,1"));
            }
        }

        public bool SendLoadSystemConfigSystem(int device_mode)
        {
            if (device_mode != (int)DeviceModes.Normal_Mode)
            {
                // invalid device mode; return false or throw exception
                //throw new Exception(string.Format("Invalid device mode. Expected values Noraml modes(0)"));
            }
            LoadSystemConfigCommand lscc = new LoadSystemConfigCommand((ushort)LoadConfigTypes.System_Configuration, new object[] { (byte)device_mode } );
            byte[] tosendb = lscc.ToBytes();
            WriteMessage("Send Load System Config: System Config (device mode)");
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendLoadSystemConfigG3(bool tmr_enabled, bool coh_enabled, byte[] long_addr)
        {
            if (long_addr.Length != 8)
            {
                throw new Exception(string.Format("long address should be 8 bytes long only, got {0}", long_addr.Length));
            }
            LoadSystemConfigCommand lscc = new LoadSystemConfigCommand((ushort)LoadConfigTypes.G3_Configuration, new object[] { tmr_enabled, coh_enabled, long_addr });
            byte[] tosendb = lscc.ToBytes();
            WriteMessage("Send Load System Config: G3 Config: Long Address");
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendShutdown(ushort shutdown_type)
        {
            ShutDownRequest sr = new ShutDownRequest(shutdown_type);
            byte[] tosendb = sr.ToBytes();
            WriteMessage("Send Shutdown Request");
            WriteMessage(sr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendSetInfoSubVendorProductIds(ushort vendor_id, string product_id)
        {
            SetInfoRequest sir = new SetInfoRequest((ushort)SetInfoTypes.Vendor_Product_Ids, vendor_id, product_id );
            byte[] tosendb = sir.ToBytes();
            WriteMessage("Send set Info vendor & product Ids");
            WriteMessage(sir.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendSetInfoSubTxParams(bool COH_enabled, byte tx_modulation_type, ushort tx_level, ushort band_sel, string tone_mask_name)
        {
            SetInfoRequest sir = new SetInfoRequest((ushort)SetInfoTypes.G3_Phy_Tx, COH_enabled, tx_modulation_type,
                tx_level, band_sel, tone_mask_name);
            byte[] tosendb = sir.ToBytes();
            WriteMessage("Send set Info G3 Tx params");
            WriteMessage(sir.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendSetInfoSubRxParams(bool COH_on, bool BLI_on, bool AGC_on, ushort gain_val, ushort band_sel, string tone_mask_name)
        {
            SetInfoRequest sir = new SetInfoRequest((ushort)SetInfoTypes.G3_Phy_Rx,COH_on, BLI_on, AGC_on,
                gain_val, band_sel, tone_mask_name);
            byte[] tosendb = sir.ToBytes();
            WriteMessage("Send set Info G3 Rx params");
            WriteMessage(sir.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendStartNetwork(ushort pan_id)
        {
            StartNetworkRequest snr = new StartNetworkRequest(pan_id);
            byte[] tosendb = snr.ToBytes();
            WriteMessage("Send Start Network");
            WriteMessage(snr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        // public bool SendDataToNode(string to_send_str, byte[] ipv6_addr, bool isUDP)
        // {
        //     byte[] to_send_b = Encoding.ASCII.GetBytes(to_send_str);
        //     DataTransferRequest dtr = new DataTransferRequest(to_send_b, ipv6_addr, isUDP);
        //     byte[] tosendb = dtr.ToBytes();
        //     WriteMessage(string.Format("Send Data {0} to node {1}", to_send_str, BitConverter.ToString(ipv6_addr)) );
        //     SafeSendMessageOnSerial(tosendb);
        //     return true;
        // }

        public bool SendDataToNode(byte[] to_send_b, byte[] ipv6_addr, bool isUDP)
        {
            string ipv6_str = BitConverter.ToString(ipv6_addr);
            byte nsdu_hdl = CentralMaster.Utils.GetHandleForIpv6Addr(ipv6_str);
            DataTransferRequest dtr = new DataTransferRequest(to_send_b, ipv6_addr, isUDP, nsdu_hdl);
            byte[] tosendb = dtr.ToBytes();
            WriteMessage(string.Format("Send Data {0} \nwith handle 0x{1:x2} \nto node {2}", BitConverter.ToString(to_send_b).Replace("-", ""), dtr.NsduHandle, BitConverter.ToString(ipv6_addr)));
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendDataToNode(byte[] to_send_b, byte[] src_ipv6_addr, byte[] dest_ipv6_addr, bool isUDP)
        {
            string ipv6_str = BitConverter.ToString(dest_ipv6_addr);
            byte nsdu_hdl = CentralMaster.Utils.GetHandleForIpv6Addr(ipv6_str);
            DataTransferRequest dtr = new DataTransferRequest(to_send_b, src_ipv6_addr, dest_ipv6_addr, isUDP, nsdu_hdl);
            byte[] tosendb = dtr.ToBytes();
            WriteMessage(string.Format("Send Data {0} \nwith handle 0x{1:x2} \nto node {2}", BitConverter.ToString(to_send_b).Replace("-", ""), dtr.NsduHandle, BitConverter.ToString(dest_ipv6_addr)));
            SafeSendMessageOnSerial(tosendb);
            return true;
        }



        public bool SendDiscoverNetworkRequest(byte duration_secs)
        {
            DiscoverRequest dr = new DiscoverRequest((byte)DiscoverTypes.NETWORK, (byte)duration_secs);
            byte[] tosendb = dr.ToBytes();
            WriteMessage("Send Discover Network Request");
            WriteMessage(dr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendAttachRequest(ushort pan_id, ushort short_addr)
        {
            AttachRequest ar = new AttachRequest(pan_id, short_addr);
            byte[] tosendb = ar.ToBytes();
            WriteMessage("Send Attach Request");
            WriteMessage(ar.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendDetachRequest(byte[] extended_addr)
        {
            DetachRequest dr = new DetachRequest(extended_addr);
            byte[] tosendb = dr.ToBytes();
            WriteMessage("Send Detach Request");
            WriteMessage(dr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendPSK(byte[] psk_b)
        {
            SetMACPIBReuest smpr = new SetMACPIBReuest(0x001f, psk_b);
            byte[] tosendb = smpr.ToBytes();
            WriteMessage("Send set mac pib");
            WriteMessage(smpr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendGetMACPIB(uint pib_id)
        {
            GetMACPIBReuest gmpr = new GetMACPIBReuest(pib_id);
            byte[] tosendb = gmpr.ToBytes();
            WriteMessage("Send get mac pib");
            WriteMessage(gmpr.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendSetAlarm(byte alarm_type, bool start_recv)
        {
            SetAlarmRequest sar = new SetAlarmRequest(alarm_type, start_recv);
            byte[] tosendb = sar.ToBytes();
            WriteMessage(string.Format("Send Set Alarm type -> 0x{0:x4}", alarm_type));
            WriteMessage(sar.ToString());
            SafeSendMessageOnSerial(tosendb);
            return true;
        }

        public bool SendSetMACPIB(uint loc_attribute_id, byte[] loc_attribute_vals, ushort loc_attribute_index = 0)
        {
            SetMACPIBReuest smpr = new SetMACPIBReuest(loc_attribute_id, loc_attribute_vals, loc_attribute_index);
            byte[] tosendb = smpr.ToBytes();
            WriteMessage(string.Format("Send Set MAC PIB TYPE-> 0x{0:x8} [{1}] -> 0x{2}", loc_attribute_id, loc_attribute_index, BitConverter.ToString(loc_attribute_vals).Replace("-", "") ) );
            WriteMessage(smpr.ToString());
            SafeSendMessageOnSerial(tosendb);
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
                    //WriteMessage("get data from Transmit queue. before take.");
                    message_2_send = transmitQueue.Take(cancelTransmitQueue.Token);
                    //WriteMessage("got data from Transmit queue. Put on serial bus.");
                    sendMessageOnSerial(message_2_send);
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
                transmitTask.Wait(60*1000); // should exit within a minute
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

        private void NotifyMessageReceived(byte msg_type, ushort msg_sub_type, MessageHeader msg)
        {
            if (receivedMessage != null)
            {
                receivedMessage(msg_type, msg_sub_type, msg);
            }
            else
            {
                DCLogger.Information(string.Format("Received Message -> {0}", msg_type));
            }
        }

        private void NotifyAttachIndication(AttachIndication ai)
        {
            if (receivedNodeAttach != null)
                receivedNodeAttach(ai);
            else
                DCLogger.Information(string.Format("to notify Attach Indication {0} received", ai.ToString()));
        }

        private void NotifyDetachIndication(DetachIndication di)
        {
            if (receivedNodeDetach != null)
            {
                receivedNodeDetach(di);
            }
            else
                DCLogger.Information(string.Format("To notify detach indication {0} received", di.ToString()));
        }

        private void NotifyDataTransferIndication(DataTransferIndication dti)
        {
            if (receivedDataTransferIndication != null)
            {
                receivedDataTransferIndication(dti);
            }
            else
                DCLogger.Information(string.Format("To notify data transfer indication {0} received", dti.ToString()));
        }

        private void NotifyDataTransferConfirm(DataTransferConfirm dtc)
        {
            if (receivedDataTransferConfirm != null)
            {
                receivedDataTransferConfirm(dtc);
            }
            else 
                DCLogger.Information(string.Format("To notify data transfer confirm {0} received", dtc.ToString()));
            
        }

        private void NotifyErrorOccured(ushort serial_error)
        {
            receivedErrorNotification?.Invoke(serial_error);
        }

        // public bool ResponseForMessageTypeReceived(int recv_msg_type, int timeout_ms)
        // {
        //     lock(recv_msg_lock)
        //     {
        //         message_received_type = -1;
        //     }
        //     Thread.Sleep(RECEIVE_MESSAGE_CHECK_TIMEOUT_MS);
        //     int time_to_wait_ms = timeout_ms-RECEIVE_MESSAGE_CHECK_TIMEOUT_MS;
        //     int tmp_msg_recv = -1;
        //     while (time_to_wait_ms > 0)
        //     {
        //         lock(recv_msg_lock)
        //         {
        //             tmp_msg_recv = message_received_type; 
        //         }
        //         if (tmp_msg_recv)
        //         Thread.Sleep(RECEIVE_MESSAGE_CHECK_TIMEOUT_MS);
        //         time_to_wait_ms -= RECEIVE_MESSAGE_CHECK_TIMEOUT_MS;
        //     }
        // }

        #endregion // utility_funcs

    }


    public class Utils
    {

        public const double MAX_TIME_NSDU_HANDLE_SECS = 300.0;

        private static byte[] custom_tone_mask = // corr to Cenelec B 
        {
            0x3f, 0x10, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00			
        };
        
        public static void update_custom_mask_value(byte[] loc_custom_tone_maskvalues)
        {
            if (loc_custom_tone_maskvalues.Length == 14)
            {
                custom_tone_mask = new byte[14];
                Array.Copy(loc_custom_tone_maskvalues, custom_tone_mask, 14);
            }
        }



        public static string GetErrorString(ushort error_code)
        {
            StringBuilder sb = new StringBuilder();
            switch (error_code)
            {
                case 0x0000:
                    sb.AppendLine("OK");
                    break;
                case 0x0002:
                    sb.AppendLine("Invalid Parameter");
                    break;
                case 0x0003:
                    sb.AppendLine("Feature Unsupported");
                    break;
                case 0x0004:
                    sb.AppendLine("Payload CRC Error");
                    break;
                case 0x0005:
                    sb.AppendLine("No EEPROM");
                    break;
                case 0x0006:
                    sb.AppendLine("Message CRC Error");
                    break;
                case 0x0007:
                    sb.AppendLine("Invalid Handle");
                    break;
                case 0x0008:
                    sb.AppendLine("Invalid Format");
                    break;
                case 0x000a:
                    sb.AppendLine("Unblock message recevied when not blocked");
                    break;
                case 0x000b:
                    sb.AppendLine("Invalid message length");
                    break;
                case 0x00f3:
                    sb.AppendLine("No handler");
                    break;
                case 0x00fc:
                    sb.AppendLine("Operation Pending");
                    break;
                case 0x00fd:
                    sb.AppendLine("Operation Timed out");
                    break;
                case 0x00fe:
                    sb.AppendLine("Out of memory error");
                    break;
                case 0x00ff:
                    sb.AppendLine("General Failure / errro");
                    break;
                case 0x0081:
                    sb.AppendLine("Read PIB failure");
                    break;
                case 0x0090:
                    sb.AppendLine("Unsupported attribute");
                    break;
                case 0x0091:
                    sb.AppendLine("Invalid Index");
                    break;
                case 0x0094:
                    sb.AppendLine("Invalid Entry");
                    break;
                default:
                    string err_str = string.Format("0x{0:x4}", error_code);
                    if (err_str.StartsWith("0x1"))
                    {
                        sb.AppendLine(string.Format("PHY error code {0}", err_str));
                    }
                    else if (err_str.StartsWith("0x2"))
                    {
                        sb.AppendLine(string.Format("MAC error code {0}", err_str));
                    }
                    else if (err_str.StartsWith("0x3"))
                    {
                        sb.AppendLine(string.Format("CL error code {0}", err_str));
                    }
                    else if (err_str.StartsWith("0x4"))
                    {
                        sb.AppendLine(string.Format("ECA error code {0}", err_str));
                    }
                    else
                    {
                        sb.AppendLine(string.Format("Unknown error code {0}", err_str));
                    }
                    break;
            }
            return sb.ToString();
        }

        public static byte[] GetToneMask(string name)
        {
            byte [] toret = null;
            switch (name) {
                case "Cenelec A 36":
                    toret = new byte[14] {0x17, 0x24, 0xff, 0xff, 0xff, 0xff, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "Cenelec A 25":
                    toret = new byte[14] {0x17, 0x24, 0xff, 0xff, 0x00, 0xf8, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "Cenelec B":
                    toret = new byte[14] {0x3f, 0x10, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "Cenelec BC":
                    toret = new byte[14] {0x3f, 0x1a, 0xff, 0xff, 0xff, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "Cenelec BCD":
                    toret = new byte[14] {0x3f, 0x20, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "FCC low band":
                    toret = new byte[14] {0x21, 0x24, 0xff, 0xff, 0xff, 0xff, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "FCC high band":
                    toret = new byte[14] {0x45, 0x24, 0xff, 0xff, 0xff, 0xff, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "FCC full band":
                    toret = new byte[14] {0x21, 0x48, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00};
                    break;

                case "FCC ARIB 54":
                    toret = new byte[14] {0x21, 0x36, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x3f, 0x00, 0x00, 0x00, 0x00, 0x00};
                    break;

                case "Custom":
                    toret = new byte[14];
                    Array.Copy(custom_tone_mask, toret, 14);
                    break;

                case "custom":
                    toret = new byte[14];
                    Array.Copy(custom_tone_mask, toret, 14);
                    break;

            }
            return toret;
        }

        public static string GetBandName(int band_sel)
        {
            StringBuilder sb = new StringBuilder();
            switch (band_sel) {
                case 0:
                    sb.Append("Cenelec (0x00)");
                    break;
                case 1:
                    sb.Append("FCC (0x01)");
                    break;
                case 2:
                    sb.Append("Reserved (0x02)");
                    break;
                case 3:
                    sb.Append("ARIB (0x03)");
                    break;
                default:
                    sb.Append(string.Format("Unknown (0x{0:x2})", band_sel) );
                    break;
            }
            return sb.ToString();
        }

        public static string GetTxModulationName(int modulation_type)
        {
            StringBuilder sb = new StringBuilder();
            switch (modulation_type) {
                case 0:
                    sb.Append("ROBO (0x00)");
                    break;
                case 1:
                    sb.Append("BPSK (0x01)");
                    break;
                case 2:
                    sb.Append("QPSK (0x02)");
                    break;
                case 3:
                    sb.Append("8PSK (0x03)");
                    break;
                case 5:
                    sb.Append("SUPER ROBO (0x05)");
                    break;
                default:
                    sb.Append(string.Format("Unknown (0x{0:x2})", modulation_type) );
                    break;
            }
            return sb.ToString();
        }

        private static Dictionary<byte, string> data_nsdu_handles = new Dictionary<byte, string>();
        private static Dictionary<byte, DateTime> dt_dnh = new Dictionary<byte, DateTime>();
        private static readonly object _lock_dnh = new object();
        private static Random util_random = new Random();

        private static string _ipv6_addr_str = "";
        private static byte _handle = (byte)0x00; 


        /** get an handle given an ipv6 address string, add handle to dictionary */
        public static byte GetHandleForIpv6Addr(string loc_ipv6_addr) 
        {
            byte to_ret_b;
            lock(_lock_dnh) 
            {
                byte val = (byte)(255 * util_random.NextDouble());
                if (val == 0x00)
                {
                    val = 0xab;
                }
                to_ret_b = val;
            }
            _ipv6_addr_str = loc_ipv6_addr;
            _handle = to_ret_b;
            return to_ret_b;
        } 
        // public static byte GetHandleForIpv6Addr(string loc_ipv6_addr) 
        // {
        //     lock(_lock_dnh) 
        //     {
        //         byte val = (byte)(255 * util_random.NextDouble());
        //         int loop_count = 0;
        //         while ( (data_nsdu_handles.ContainsKey(val) ) && (loop_count <= 255) )
        //         {
        //             val = (byte) ( (val + 1) % 255 );
        //             loop_count ++;
        //         }
        //         if (loop_count > 255)
        //         {
        //             RemoveAllExpiredElements();
        //             // try again
        //             val = (byte)(255 * util_random.NextDouble());
        //             loop_count = 0;
        //             while ( (data_nsdu_handles.ContainsKey(val) ) && (loop_count < 255) )
        //             {
        //                 val = (byte) ( (val + 1) % 255 );
        //                 loop_count ++;
        //             }
        //             if (loop_count >= 255)
        //             {
        //                 // failed to generate a unique key, not adding to dictionary
        //                 val = (byte)(255 * util_random.NextDouble());
        //                 return val;
        //             }
        //             else 
        //             {
        //                 data_nsdu_handles.Add(val, loc_ipv6_addr);
        //                 dt_dnh.Add(val, DateTime.Now);
        //                 return val;
        //             }
        //         }
        //         else 
        //         {
        //             data_nsdu_handles.Add(val, loc_ipv6_addr);
        //             dt_dnh.Add(val, DateTime.Now);
        //             return val;
        //         }


        //     }
        // } 

        /**
         * Get ipv6 address corresponding to handle, and delete from dictionary
         * return "" string if not available in dictionary
         */
        public static string GetIpv6AddressForHandle(byte recv_nsdu_handle)
        {
            string ret_str = ""; 
            lock(_lock_dnh) 
            {
                if (recv_nsdu_handle == _handle) 
                {
                    ret_str = _ipv6_addr_str;
                    _handle = 0;
                }
                else 
                {
                    ret_str = "";
                }
            }
            return ret_str;
        }

        private static void RemoveAllExpiredElements() 
        {
            DateTime dt_now = DateTime.Now;
            // no sync here, this method to be called from lock method
            // foreach(KeyValuePair<byte, DateTime> item in dt_dnh.ToList())
            // {
            //     if ( ( dt_now - (DateTime)item.Value ).TotalSeconds > MAX_TIME_NSDU_HANDLE_SECS ) 
            //     {
            //         dt_dnh.Remove(item.Key);
            //     }
            // }
            dt_dnh.Where( kvp => (dt_now - kvp.Value).TotalSeconds > MAX_TIME_NSDU_HANDLE_SECS ).ToList().ForEach(kvp2 => dt_dnh.Remove(kvp2.Key));

        }

    }


    // options ORG-> origin of message: values 1 => from host, 0 => from receiver
    //         RPY -> acknowledge reply bit
    //         RESV -> reserved; ignore
    //         SEQ -> 4 bit sequence number
    public class MessageHeader
    {
        public byte message_type;
        public byte options; // consists of bit 7 ORG, bit 6 RPY, bits 5 and 4 are RESV, bits 3 to 0 SEQ
        public byte payload_length_lsb;
        public byte payload_length_msb;
        public byte[] header_crc16;
        public byte[] payload_crc16;
        public byte[] bytes;
        public bool byte_arr_computed = false;

        public const bool displayRAW = true; // IF SET TO FALSE will not display raw data

        public MessageHeader()
        {
            header_crc16 = new byte[2];
            payload_crc16 = new byte[2];
        }

        private static ushort[] table = {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0
        };

        public static ushort ComputeCRC16Old(byte[] data)
        {
            ushort crc = 0;
            for (int i = 0; i < data.Length; ++i)
            {
                byte index = (byte)(crc ^ data[i]);
                crc = (ushort)((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        public static ushort ComputeCRC16Old(byte[] data, int startIndex, int length)
        {
            ushort crc = 0;
            for (int i = startIndex; i < (startIndex + length); ++i)
            {
                byte index = (byte)(crc ^ data[i]);
                crc = (ushort)((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        public static ushort ComputeCRC16(byte[] c, int startIndex = 0, int length=-1)
        {
            ushort Polynominal = 0x1021;
            ushort InitValue = 0x0;

            ushort i, j, index = 0;
            ushort CRC = InitValue;
            ushort Remainder, tmp, short_c;
            int data_len;
            index = (ushort)startIndex;
            data_len = (length == -1) ? c.Length : length;
            for (i = (ushort)startIndex; i < (startIndex + data_len); i++)
            {
                short_c = (ushort)(0x00ff & (ushort)c[index]);
                tmp = (ushort)((CRC >> 8) ^ short_c);
                Remainder = (ushort)(tmp << 8);
                for (j = 0; j < 8; j++)
                {

                    if ((Remainder & 0x8000) != 0)
                    {
                        Remainder = (ushort)((Remainder << 1) ^ Polynominal);
                    }
                    else
                    {
                        Remainder = (ushort)(Remainder << 1);
                    }
                }
                CRC = (ushort)((CRC << 8) ^ Remainder);
                index++;
            }
            return CRC;
        }

    }

    public class GetSystemInfoRequest: MessageHeader
    {

        public GetSystemInfoRequest()
        {
            message_type = (byte)CommandTypes.GET_SYSTEM_INFO;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)4;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[8];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("GetSystemInfoRequest id " + message_type.ToString());
            return sb.ToString();
        }

    }

    public class GetSystemInfoResponse: MessageHeader
    {
        public uint firmware_version; // major.minor.revision.build
        public byte device_type; // 3 is G3, 8 is P1901.2
        public byte device_mode; // 0 - NORMAL MODE, 1 - MAC MODE, 3 - P2P mode (point 2 point test mode), 7 - G3 embedded APPEmu mode
        public ushort hardware_revision; // 0 -> version before 'D', else is it 'D' => a value of 68
        public byte port_assignments; // bit 0 (data port), bit 3 (Diag port) : value 0 is port SCI-A, value 1 is port SCI-B
        public byte flags; // TMR and COH flags bit 3 is COH flag (coherent modulation enabled (1), disabled (0)) bit 5 is TMR flag (tone mask request enabled(1), disabled (0))
        public List<byte> long_address = new List<byte>(8); // long address is 8 bytes

        public GetSystemInfoResponse( byte[] data)
        {
            FromBytes(data);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 58 + header is 8
            if (data.Length != 66)
                throw new Exception(string.Format("Invalid GetSystemInfoResponse received, expected 66 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            firmware_version = BitConverter.ToUInt32(data, 0+8);
            device_type = data[22+8];
            device_mode = data[23+8];
            port_assignments = data[32+8];
            flags = data[40+8];
            for (int ii=0; ii<8; ii++)
            {
                long_address.Add(data[44 + 8 + ii]);
                //long_address[ii] = data[44 + ii];
            }
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SystemInfoResponse (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            byte[] fw_ver_b = BitConverter.GetBytes(firmware_version);
            sb.AppendFormat("firmware version = {0}. {1}. {2}. {3}", fw_ver_b[3], fw_ver_b[2], fw_ver_b[1], fw_ver_b[0]);
            sb.Append(System.Environment.NewLine);
            if (device_type == 3)
            {
                sb.AppendLine("device type -> G3");
            }
            else if (device_type == 8)
            {
                sb.AppendLine("device type -> P1901.2");
            }
            else
            {
                sb.AppendLine(" unknown device type");
            }
            sb.Append("Device Mode -> ");
            switch (device_mode)
            {
                case 0:
                    sb.AppendLine("Normal");
                    break;
                case 1:
                    sb.AppendLine("MAC mode");
                    break;
                case 3:
                    sb.AppendLine("P2P (point to point)");
                    break;
                case 7:
                    sb.AppendLine("G3 Embedded AppEnm");
                    break;
                default:
                    sb.AppendFormat(" Unknown mode {0}", device_mode);
                    sb.AppendLine();
                    break;
            }
            sb.Append("HW Version -> ");
            if (hardware_revision == 0)
            {
                sb.AppendLine(" Pre rev D");
            }
            else
            {
                sb.AppendLine(" Rev D");
            }

            if ((byte)(port_assignments & 0x01) == (byte)1)
            {
                sb.AppendLine("Data port = SCI-B");
            }
            else
            {
                sb.AppendLine("Data port = SCI-A");

            }
            if ((byte)(port_assignments & 0x04) == (byte)4)
            {
                sb.AppendLine("Diag port = SCI-B");
            }
            else
            {
                sb.AppendLine("Diag port = SCI-A");
            }
            sb.Append("Coherent modulation -> ");
            if ( (flags & 0x08) == 0x08)
            {
                sb.AppendLine("enabled");
            }
            else
            {
                sb.AppendLine("disabled");
            }

            sb.Append("Tone Mask Request -> ");
            if ((flags & 0x10) == 0x10)
            {
                sb.AppendLine("enabled");
            }
            else
            {
                sb.AppendLine("disabled");
            }

            sb.Append("Long Address -> ");
            string addr_str = BitConverter.ToString(long_address.ToArray()).Replace("-", " ");
            sb.Append(addr_str);
            return sb.ToString();
        }
        
    }

    public class ShutDownRequest: MessageHeader
    {
        public ushort shutdownType;
        
        public ShutDownRequest(ushort shutdown_type)
        {
            message_type = (byte)CommandTypes.SHUTDOWN;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)6;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
            shutdownType = shutdown_type;
        }

        private void ComputeBytes()
        {
            bytes = new byte[8 + 2];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(shutdownType);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 2);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("ShutdownRequest id {0}, type -> 0x{1:x4}", message_type.ToString(), shutdownType) );
            return sb.ToString();
        }

    }

    public class ShutDownResponse: MessageHeader
    {
       public ushort status; // 

       public ShutDownResponse(byte[] data)
        {
            FromBytes(data);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 2 + header is 8
            if (data.Length != 10)
                throw new Exception(string.Format("Invalid ShutDownResponse received, expected 10 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Shutdown Response (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            sb.Append(Utils.GetErrorString(status));
            return sb.ToString();
        }

    }

    public class LoadSystemConfigSubPortDesignation
    {
        public byte data_port;
        public byte diag_port;
        public LoadSystemConfigSubPortDesignation(byte dataPort, byte diagPort)
        {
            data_port = dataPort;
            diag_port = diagPort;
        }

        public LoadSystemConfigSubPortDesignation(object p1, object p2): this((byte)p1, (byte)p2) { }

        public byte[] ToBytes()
        {
            byte[] retb = new byte[1]; 
            retb[0] = (byte)(((diag_port & 0x03) << 2) | (data_port & 0x03));
            return retb;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("LoadSystemConfig: Port Designation");
            sb.Append("Data port -> ");
            if (data_port == 1)
            {
                sb.AppendLine("SCI-B");
            }
            else
            {
                sb.AppendLine("SCI-A");
            }
            sb.Append("Diag port -> ");
            if (diag_port == 1)
            {
                sb.Append("SCI-B");
            }
            else
            {
                sb.Append("SCI-A");
            }
            return sb.ToString();
        }

    }

    public class LoadSystemConfigSubSystemConfiguration
    {
        public byte device_mode;
        public LoadSystemConfigSubSystemConfiguration(byte devicemode)
        {
            device_mode = devicemode;
        }

        public LoadSystemConfigSubSystemConfiguration(object p1): this((byte)p1) { }

        public byte[] ToBytes()
        {
            byte[] retb = new byte[26];
            for (int ii = 0; ii < retb.Length; ii++ )
            {
                retb[ii] = 0;
            }
            retb[24] = device_mode;
            return retb;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("LoadSystemConfig: System Config");
            sb.Append("Device Mode -> ");
            sb.Append(((DeviceModes)device_mode).ToString());
            return sb.ToString();
        }

    }

    public class LoadSystemConfigSubG3Configuration
    {
        public bool COH_enabled = false; // false => value 0 in bit 1 ; true => value 1 in bit 1 
        public bool TMR_enabled = false; // false => value 0 in bit 1 ; true => value 1 in bit 1
        public byte[] long_address; // little endian should be 8 bytes long
        public LoadSystemConfigSubG3Configuration(bool TMREnabled, bool COHEnabled, byte[] longAddress)
        {
            COH_enabled = COHEnabled;
            TMR_enabled = TMREnabled;
            long_address = new byte[8];
            Array.Copy(longAddress, long_address, 8); // assume longAddress is in little endian format
        }

        public LoadSystemConfigSubG3Configuration(object p1, object p2, object p3): this ((bool) p1, (bool)p2, (byte[]) p3) { }

        public byte[] ToBytes()
        {
            byte[] retb = new byte[16];
            byte coh_val = COH_enabled ? (byte)1 : (byte)0;
            byte tmr_val = TMR_enabled ? (byte)1 : (byte)0;
            retb[0] = (byte)(((coh_val & 0x01) << 1) | (tmr_val & 0x01));
            retb[1] = 0; // reserved
            Array.Copy(long_address, 0, retb, 2, long_address.Length);
            retb[10] = 0; // reserved
            retb[11] = 0; // reserved
            retb[12] = 0; // reserved
            retb[13] = 0; // reserved
            retb[14] = 0xEF; // default value of 239 for compabitility (little endian of G3 seg size (obsolete) LSB)
            retb[15] = 0; // default value of 239 for compabitility (little endian of G3 seg size (obsolete) MSB)
            return retb;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("LoadSystemConfig: G3 Configuration");
            sb.Append("COH Enabled -> ");
            sb.AppendLine(COH_enabled.ToString());
            sb.Append("TMR Enabled -> ");
            sb.AppendLine(TMR_enabled.ToString());
            sb.Append("Long Address -> ");
            byte[] tmp_addr = new byte[long_address.Length];
            Array.Copy(long_address, tmp_addr, long_address.Length);
            tmp_addr.Reverse();
            sb.Append(BitConverter.ToString(tmp_addr, 0));
            return sb.ToString();
        }

    }

    public class LoadSystemConfigCommand: MessageHeader
    {
        public ushort config_type;
        public ushort config_len;
        public byte[] config_bytes;
        public object config_object; // holds config data based on config_type

        public LoadSystemConfigCommand(ushort subtype, params object[] param_list)
        {

            switch (subtype)
            {
                case (ushort)LoadConfigTypes.Port_Designation:
                    // expect params object[] list to contain [ byte, byte ] as (data_Port, diag_port)
                    if (param_list.Length != 2)
                        throw new Exception(" Invalid number of elements for Load System Config: Port Designation. Require data port and diag port values");
                    if (param_list[0] is byte && param_list[1] is byte)
                    {
                        LoadSystemConfigSubPortDesignation lscpd = new LoadSystemConfigSubPortDesignation(param_list[0], param_list[1]);
                        config_bytes = lscpd.ToBytes();
                        config_len = (ushort)config_bytes.Length;
                        config_type = subtype;
                        config_object = lscpd;
                    }
                    else
                    {
                        throw new Exception(" Expected (byte, byte) for Load System Config: Port Designation for data port and diag port values");
                    }
                    break;

                case (ushort) LoadConfigTypes.System_Configuration:
                    // expect params object list to contain [ byte] only as device_mode
                    if (param_list.Length != 1)
                        throw new Exception(" Invalid number of elements for Load System Config: System Configuration. Require device mode value only");
                    if (param_list[0] is byte)
                    {
                        LoadSystemConfigSubSystemConfiguration lscsc = new LoadSystemConfigSubSystemConfiguration(param_list[0]);
                        config_bytes = lscsc.ToBytes();
                        config_len = (ushort)config_bytes.Length;
                        config_type = subtype;
                        config_object = lscsc;
                    }
                    else
                    {
                        throw new Exception(" Expected (byte) for Load System Config:System Configuration for device mode value");
                    }
                    break;

                case (ushort)LoadConfigTypes.G3_Configuration:
                    // expect params object list to contain [ bool, bool, byte[8] ] for (COH enabled, TMR enabled, long address as byte[8] little endian)
                    if (param_list.Length != 3)
                        throw new Exception(" Invalid number of elements for Load System Config: G3 Configuration. Require (bool, bool, byte[8]");
                    if ( (param_list[0] is bool) && (param_list[1] is bool) && (param_list[2].GetType().IsArray) )
                    {
                        LoadSystemConfigSubG3Configuration lscgc = new LoadSystemConfigSubG3Configuration(param_list[0], param_list[1], param_list[2]);
                        config_bytes = lscgc.ToBytes();
                        config_len = (ushort)config_bytes.Length;
                        config_type = subtype;
                        config_object = lscgc;
                    }
                    else
                    {
                        throw new Exception(" Expected (byte) for Load System Config:System Configuration for device mode value");
                    }
                    break;

                default:
                    throw new Exception(" Unknown Load System Config: sub type");
                    //break;
            }
            message_type = (byte)CommandTypes.LOAD_SYSTEM_CONFIG;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            // need to add 4 = header crc + payload crc; 4 for config_type and config_len
            payload_length_lsb = (byte)((4+config_len + 4) & 0x00FF);
            payload_length_msb = (byte)( ( (4+config_len + 4) & 0xFF00) >> 8);
            //ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //header_crc16[0] = (byte)(crc16 & 0xFF);
            //header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            header_crc16[0] = 0;
            header_crc16[1] = 0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            // total bytes = config_len + 4 + header len (8 bytes)
            int pad_len = (config_len + 4 + 8) % 2;
 
            bytes = new byte[config_len + 4 + 8 + pad_len];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            bytes[8] = (byte)(config_type & 0x00FF);
            bytes[9] = (byte)((config_type & 0xFF00) >> 8);
            bytes[10] = (byte)(config_len & 0x00FF);
            bytes[11] = (byte)((config_len & 0xFF00) >> 8);
            Array.Copy(config_bytes, 0, bytes, 12, config_len);
            if (pad_len == 1)
                bytes[bytes.Length - 1] = 0;
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, config_len + 4);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (config_type)
            {
                case (ushort) LoadConfigTypes.Port_Designation:
                    LoadSystemConfigSubPortDesignation lscpd = (LoadSystemConfigSubPortDesignation)config_object;
                    sb.Append(lscpd.ToString());
                    break;
                case (ushort) LoadConfigTypes.System_Configuration:
                    LoadSystemConfigSubSystemConfiguration lscsc = (LoadSystemConfigSubSystemConfiguration)config_object;
                    sb.Append(lscsc.ToString());
                    break;

                case (ushort)LoadConfigTypes.G3_Configuration:
                    LoadSystemConfigSubG3Configuration lscgc = (LoadSystemConfigSubG3Configuration)config_object;
                    sb.Append(lscgc.ToString());
                    break;

                default:
                    sb.Append("Unknow Load Config Sub Type");
                    break;
            }
            return sb.ToString();
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }
    }

    public class LoadSystemConfigResponse: MessageHeader
    {
       public ushort status; // 

        public LoadSystemConfigResponse(byte[] data)
        {
            FromBytes(data);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 2 + header is 8
            if (data.Length != 10)
                throw new Exception(string.Format("Invalid GetLoadSystemConfigResponse received, expected 10 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("LoadConfig Response (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            sb.Append(Utils.GetErrorString(status));
            return sb.ToString();
        }

    }

    public class SetInfoSubG3Phy_TxParams
    {
        public ushort band_sel;
        public bool COH_enabled;
        public byte tx_modulation_type;
        public ushort tx_level;
        public string tone_mask_name;

        public SetInfoSubG3Phy_TxParams(bool COHEnabled, byte txModulationType,
        ushort txLevel, ushort bandSel, string toneMaskName)
        {
            band_sel = bandSel;
            COH_enabled = COHEnabled;
            tx_modulation_type = txModulationType;
            tx_level = txLevel;
            tone_mask_name = toneMaskName;
        }

        public SetInfoSubG3Phy_TxParams( object p1, object p2, object p3, object p4, object p5):
            this((bool)p1, (byte)p2, (ushort)p3, (ushort)p4, (string)p5)
        { }

       public byte[] ToBytes()
       {
           byte[] retb = new byte[1 + 1 + 2 + 14];
           retb[0] = (byte) ( 
                              ( (band_sel             & 0x0003) << 6 ) | 
                              (((COH_enabled ? 1 : 0) & 0x0001) << 3 )  
                           );
           retb[1] = (byte) (tx_modulation_type & 0x03);
           byte[] tmpb = BitConverter.GetBytes(tx_level);
           retb[2] = tmpb[0];
           retb[3] = tmpb[1];
           byte[] tmpb2 = Utils.GetToneMask(tone_mask_name);
           if ( (tmpb2 == null) || (tmpb2.Length != 14) )
           {
               throw new Exception("Invalid tone mask set... Please check.. it needs to have a length of 14 values.");
           }
           Array.Copy(tmpb2, 0, retb, 4, 14);
           return retb;
        }

       public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Set Info: G3 Tx Params");
            sb.AppendLine(string.Format("Band Selection -> {0}", Utils.GetBandName(band_sel) ) );
            sb.Append("COH Enabled -> ");
            if (COH_enabled )
            {
                sb.AppendLine("1");
            }
            else
            {
                sb.AppendLine("0");
            }
            sb.AppendLine(string.Format("Tx modulation -> {0}", Utils.GetTxModulationName(tx_modulation_type)));
            sb.AppendLine(string.Format("Tx level -> {0}", tx_level.ToString()));
            sb.Append(string.Format("Tone Mask -> {0} ", tone_mask_name));
            string tmn_str = BitConverter.ToString(Utils.GetToneMask(tone_mask_name)).Replace("-", " ");
            sb.Append(tmn_str);
            return sb.ToString();
        }


    }


    public class SetInfoSubG3Phy_RxParams
    {
        private ushort band_sel;
        private bool COH_enabled;
        private bool BLI_on; // on '1' or off '0'
        private bool AGC_on; // automatic gain control on '1' or off '0'
        private ushort gain_val;
        private string tone_mask_name;
        public SetInfoSubG3Phy_RxParams(bool COHEnabled, bool bliOn, bool agcOn,
            ushort gainVal, ushort bandSel, string toneMaskName)
        {
            band_sel = bandSel;
            COH_enabled = COHEnabled;
            BLI_on = bliOn;
            AGC_on = agcOn;
            gain_val = gainVal; // THIS IS IGNORED IF agcOn is 1
            tone_mask_name = toneMaskName;
        }

        public SetInfoSubG3Phy_RxParams(object p1, object p2, object p3, object p4, object p5, object p6) :
            this((bool)p1, (bool)p2, (bool)p3, (ushort)p4, (ushort)p5, (string)p6)
        { }

        public byte[] ToBytes()
        {
            byte[] retb = new byte[1 + 1 + 2 + 14];
            retb[0] = 0; // reserved
            retb[1] = (byte)(
                               ((band_sel & 0x0003) << 6) |
                               (((COH_enabled ? 1 : 0) & 0x0001) << 3) |
                               (((BLI_on ? 1 : 0) & 0x0001) << 2) | 
                               ( (AGC_on ? 1 : 0) & 0x0001)
                            );
            byte[] tmpb = BitConverter.GetBytes(gain_val);
            retb[2] = tmpb[0];
            retb[3] = tmpb[1];
            byte[] tmpb2 = Utils.GetToneMask(tone_mask_name);
            if ((tmpb2 == null) || (tmpb2.Length != 14))
            {
                throw new Exception("Invalid tone mask set... Please check.. it needs to have a length of 14 values.");
            }
            Array.Copy(tmpb2, 0, retb, 4, 14);
            return retb;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Set Info: G3 Rx Params");
            sb.Append("COH Enabled -> ");
            if (COH_enabled)
            {
                sb.AppendLine("1");
            }
            else
            {
                sb.AppendLine("0");
            }
            sb.Append("BLI -> ");
            if (BLI_on) { sb.Append("1, "); } else { sb.Append("0, "); }
            sb.Append("AGC -> ");
            if (AGC_on) { sb.AppendLine("1"); } else { sb.AppendLine("0"); }
            sb.AppendLine(string.Format("Gain Val -> {0}", gain_val.ToString()));
            sb.AppendLine(string.Format("Band Selection -> {0}", Utils.GetBandName(band_sel)));
            sb.AppendLine(string.Format("Tone Mask -> {0} : ", tone_mask_name));
            string tmn_str = BitConverter.ToString(Utils.GetToneMask(tone_mask_name)).Replace("-", " ");
            sb.Append(tmn_str);
            return sb.ToString();
        }


    }

    public class SetInfoSubVendorProductIds
    {
        public ushort vendor_id;
        public string product_id;

        public SetInfoSubVendorProductIds(ushort vendorId, string productId)
        {
            if (productId.Length > 15)
                throw new Exception(string.Format("Product id must be a max of 15 chars, got {0}", productId.Length ) );
            vendor_id = vendorId;
            product_id = productId;
        }

        public SetInfoSubVendorProductIds(object p1, object p2) : this((ushort)p1, (string)p2)
        {  }

        public byte[] ToBytes()
        {
            byte[] toret = new byte[18];
            byte[] tmpb = BitConverter.GetBytes(vendor_id);
            toret[0] = tmpb[0];
            toret[1] = tmpb[1];
            Encoding ascii = Encoding.ASCII;
            byte[] tmpb2 = ascii.GetBytes(product_id);
            Array.Copy(tmpb2, 0, toret, 2, tmpb2.Length);
            for (int ii=tmpb2.Length + 2; ii < toret.Length; ii++)
            {
                toret[ii] = 0;
            }
            return toret;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Set Info: Vendor and Product Ids");
            sb.AppendLine(string.Format("Vendor Id -> {0}", vendor_id));
            sb.Append(string.Format("Product Id -> {0}", product_id));
            return sb.ToString();
        }
    }

    public class SetInfoSubGainAttenuationParams
    {
        public sbyte gain_atten_val; // -6 db  to  3db in steps of 0.1; values are -60 (0xC4) to 30 (0x1E)
        public byte pga_atten; // pga attenuation val 0 => 0 db, val 1 => -3 db, val 2 => -6 db

        public SetInfoSubGainAttenuationParams(sbyte gainAttenVal, byte pgaAtten)
        {
            gain_atten_val = gainAttenVal;
            pga_atten = pgaAtten;
        }

        public SetInfoSubGainAttenuationParams(object p1, object p2): this ((sbyte)p1, (byte)p2)
        { }

        public byte[] ToBytes()
        {
            byte[] toret = new byte[2];
            toret[0] = (byte)(gain_atten_val & 0XFF);
            toret[1] = pga_atten;
            return toret;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Set Info: Gain/Attenuation, PGA Attenuations");
            sb.AppendLine(string.Format("Gain/Attenuation -> {0}", gain_atten_val));
            sb.Append(string.Format("PGA atten -> {0}", pga_atten));
            return sb.ToString();
        }


    }

    public class SetInfoRequest: MessageHeader
    {
        public ushort info_type;
        public ushort info_len;
        public byte[] info_bytes;
        private object info_object; // holds data based on info_type

        public SetInfoRequest(ushort subtype, params object[] param_list)
        {

            switch (subtype)
            {
                case (ushort)SetInfoTypes.G3_Phy_Tx:
                    // params "COH mod enabled" (bool),  tx modulation (byte), 
                    //        Tx Level (ushort) 0-32,"Band Selection" (ushort), Tone Mask (string)
                    // expect params object[] list to contain [ bool, byte, ushort, ushort, string ] 
                    if (param_list.Length != 5)
                        throw new Exception(" Invalid number of elements for Set Info: G3 Tx Phy Params. Require band selec, COH mod enabled, tx modulation, tx level, tone mask values");
                    if (param_list[0] is bool && param_list[1] is byte &&
                        param_list[2] is ushort && param_list[3] is ushort && param_list[4] is string)
                    {
                        SetInfoSubG3Phy_TxParams sisgptp = new SetInfoSubG3Phy_TxParams(param_list[0], param_list[1], param_list[2],
                            param_list[3], param_list[4]);
                        info_bytes = sisgptp.ToBytes();
                        info_len = (ushort)info_bytes.Length;
                        info_type = subtype;
                        info_object = sisgptp;
                    }
                    else
                    {
                        throw new Exception(" Expected (bool, byte, ushort, ushort, string) for Set Info: G3 Phy Tx params");
                    }
                    break;

                case (ushort)SetInfoTypes.G3_Phy_Rx:
                    // params " "COH mod enabled" (bool),  BLI on (bool), AGC on (bool) 
                    //        Gain value (ushort) 0-7, Band Selection" (ushort), Tone Mask (string)
                    // expect params object[] list to contain [ bool, bool, bool, ushort, ushort, string ] 
                    if (param_list.Length != 6)
                        throw new Exception(" Invalid number of elements for Set Info: G3 Rx Phy Params. Require COH mod enabled, BLI on, AGC on, gain value, band sel, tone mask values");
                    if (param_list[0] is bool && param_list[1] is bool && param_list[2] is bool &&
                        param_list[3] is ushort && param_list[4] is ushort && param_list[5] is string)
                    {
                        SetInfoSubG3Phy_RxParams sisgpxp = new SetInfoSubG3Phy_RxParams(param_list[0], param_list[1], param_list[2],
                            param_list[3], param_list[4], param_list[5]);
                        info_bytes = sisgpxp.ToBytes();
                        info_len = (ushort)info_bytes.Length;
                        info_type = subtype;
                        info_object = sisgpxp;
                    }
                    else
                    {
                        throw new Exception(" Expected (bool, bool, bool, ushort, ushort, string) for SetInfo: G3 PHY Rx params");
                    }
                    break;

                case (ushort)SetInfoTypes.Vendor_Product_Ids:
                    // params " vendor id (ushort) product id (max 15 char string)
                    // expect params object[] list to contain [ ushort, string ] 
                    if (param_list.Length != 2)
                        throw new Exception(" Invalid number of elements for Set Info: Vendor and Product Ids. Require vendor id, product id (max 15 chars)");
                    if (param_list[0] is ushort && param_list[1] is string)
                    {
                        SetInfoSubVendorProductIds sisvpi = new SetInfoSubVendorProductIds(param_list[0], param_list[1]);
                        info_bytes = sisvpi.ToBytes();
                        info_len = (ushort)info_bytes.Length;
                        info_type = subtype;
                        info_object = sisvpi;
                    }
                    else
                    {
                        throw new Exception(" Expected (ushort, string) for Set Info: Vendor product ids");
                    }
                    break;

                case (ushort)SetInfoTypes.Tx_Phy_AttenuationGain_Params:
                    // params " gain/attenuation (sbyte) aga_atten (byte)
                    // expect params object[] list to contain [ sbyte, byte ] 
                    if (param_list.Length != 2)
                        throw new Exception(" Invalid number of elements for Set Info: Vendor and Product Ids. Require vendor id, product id (max 15 chars)");
                    if (param_list[0] is sbyte && param_list[1] is byte)
                    {
                        SetInfoSubGainAttenuationParams sisgap = new SetInfoSubGainAttenuationParams(param_list[0], param_list[1]);
                        info_bytes = sisgap.ToBytes();
                        info_len = (ushort)info_bytes.Length;
                        info_type = subtype;
                        info_object = sisgap;
                    }
                    else
                    {
                        throw new Exception(" Expected (sbyte, byte) for Set Info: atten/gain params");
                    }
                    break;


                default:
                    throw new Exception(string.Format("Unknown Set info type {0} ", subtype));
                    //break;
            }
            message_type = (byte)CommandTypes.SET_INFO;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            // need to add 4 = header crc + payload crc
            payload_length_lsb = (byte)((4 + info_len + 4) & 0x00FF);
            payload_length_msb = (byte)(((4 + info_len + 4) & 0xFF00) >> 8);
            //ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //header_crc16[0] = (byte)(crc16 & 0xFF);
            //header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            header_crc16[0] = 0;
            header_crc16[1] = 0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();

        }

        private void ComputeBytes()
        {
            // total bytes = info_len + 4 + header len (8 bytes)
            int pad_len = (info_len + 4 + 8) % 2;

            bytes = new byte[info_len + 4 + 8 + pad_len];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];

            bytes[8] = (byte)(info_type & 0x00FF);
            bytes[9] = (byte)((info_type & 0xFF00) >> 8);
            bytes[10] = (byte)(info_len & 0x00FF);
            bytes[11] = (byte)((info_len & 0xFF00) >> 8);
            Array.Copy(info_bytes, 0, bytes, 12, info_len);
            if (pad_len == 1)
                bytes[bytes.Length - 1] = 0;
            
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, info_len + 4);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (info_type)
            {
                case (ushort)SetInfoTypes.G3_Phy_Tx:
                    SetInfoSubG3Phy_TxParams sisgptp = (SetInfoSubG3Phy_TxParams)info_object;
                    sb.Append(sisgptp.ToString());
                    break;
                case (ushort)SetInfoTypes.G3_Phy_Rx:
                    SetInfoSubG3Phy_RxParams sisgpxp = (SetInfoSubG3Phy_RxParams)info_object;
                    sb.Append(sisgpxp.ToString());
                    break;
                case (ushort)SetInfoTypes.Vendor_Product_Ids:
                    SetInfoSubVendorProductIds sisvpi = (SetInfoSubVendorProductIds)info_object;
                    sb.Append(sisvpi.ToString());
                    break;  
                case (ushort)SetInfoTypes.Tx_Phy_AttenuationGain_Params:
                    SetInfoSubGainAttenuationParams sisgap = (SetInfoSubGainAttenuationParams)info_object;
                    sb.Append(sisgap.ToString());
                    break;
                default:
                    sb.Append("Unknow Set Info Sub Type");
                    break;
            }
            return sb.ToString();
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

    }

    public class SetInfoResponse : MessageHeader
    {
        public ushort status; // 

        public SetInfoResponse(byte[] data)
        {
            FromBytes(data);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 2 + header is 8
            if (data.Length != 10)
                throw new Exception(string.Format("Invalid SetInfoResponse received, expected 10 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Set Info Response (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            sb.Append(Utils.GetErrorString(status));
            return sb.ToString();
        }

    }

    public class StartNetworkRequest : MessageHeader
    {
        public ushort panId;

        public StartNetworkRequest(ushort pan_id)
        {
            message_type = (byte)CommandTypes.NETWORK_START;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)6;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            panId = pan_id;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[8 + 2];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(panId);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 2);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("Start Network Request, pan id -> 0x{0:x4}", panId));
            return sb.ToString();
        }

    }

    public class NetworkStartConfirm : MessageHeader
    {
        public ushort status; // 

        public NetworkStartConfirm(byte[] data)
        {
            FromBytes(data);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 2 + header is 8
            if (data.Length != 10)
                throw new Exception(string.Format("Invalid NetworkStartConfirm received, expected 10 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Network start Confirm (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            sb.Append(Utils.GetErrorString(status));
            return sb.ToString();
        }

    }

    public class AttachRequest: MessageHeader
    {
        public ushort pan_id;
        public ushort lba_addr; // what is it? short addr of device

        public AttachRequest(ushort panId, ushort short_addr)
        {
            message_type = (byte)CommandTypes.ATTACH;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)8;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            pan_id = panId;
            lba_addr = short_addr;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[8 + 4];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(pan_id);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            tmpu_b = BitConverter.GetBytes(lba_addr);
            bytes[10] = tmpu_b[0];
            bytes[11] = tmpu_b[1];
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 4);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("Attach Network Request, pan id -> 0x{0:x4}", pan_id));
            return sb.ToString();
        }

    }

    public class AttachConfirm : MessageHeader
    {
        public ushort status;
        public ushort nw_addr;
        public ushort pan_id;

        public AttachConfirm(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 6 + header is 8
            if (data.Length != 14)
                throw new Exception(string.Format("Invalid Attach Confirm received, expected 14 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            nw_addr = BitConverter.ToUInt16(data, 10);
            pan_id = BitConverter.ToUInt16(data, 12);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Attach Confirm: status {0}, nw addr 0x{1:x4}, pan id {2:x4}", Utils.GetErrorString(status), nw_addr, pan_id);
            return sb.ToString();
        }
    }


    public class AttachIndication: MessageHeader
    {
        public byte[] ipv6_addr_prefix; // 8 bytes 
        public byte[] ipv6_addr_suffix; // 8 bytes
        public byte[] extended_addr; // 8 bytes
        public byte capability_info;
        public byte reserved;

        public AttachIndication(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 8 + 8 + 8 + 2 + header is 8
            if (data.Length != 34)
                throw new Exception(string.Format("Invalid Attach Indication received, expected 34 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            ipv6_addr_prefix = new byte[8];
            Array.Copy(data, 8, ipv6_addr_prefix, 0, 8);
            byte tmpb;
            tmpb = ipv6_addr_prefix[0];
            ipv6_addr_prefix[0] = ipv6_addr_prefix[1];
            ipv6_addr_prefix[1] = tmpb;
            tmpb = ipv6_addr_prefix[2];
            ipv6_addr_prefix[2] = ipv6_addr_prefix[3];
            ipv6_addr_prefix[3] = tmpb;
            tmpb = ipv6_addr_prefix[4];
            ipv6_addr_prefix[4] = ipv6_addr_prefix[5];
            ipv6_addr_prefix[5] = tmpb;
            tmpb = ipv6_addr_prefix[6];
            ipv6_addr_prefix[6] = ipv6_addr_prefix[7];
            ipv6_addr_prefix[7] = tmpb;

            ipv6_addr_suffix = new byte[8];
            Array.Copy(data, 16, ipv6_addr_suffix, 0, 8);
            tmpb = ipv6_addr_suffix[0];
            ipv6_addr_suffix[0] = ipv6_addr_suffix[1];
            ipv6_addr_suffix[1] = tmpb;
            tmpb = ipv6_addr_suffix[2];
            ipv6_addr_suffix[2] = ipv6_addr_suffix[3];
            ipv6_addr_suffix[3] = tmpb;
            tmpb = ipv6_addr_suffix[4];
            ipv6_addr_suffix[4] = ipv6_addr_suffix[5];
            ipv6_addr_suffix[5] = tmpb;
            tmpb = ipv6_addr_suffix[6];
            ipv6_addr_suffix[6] = ipv6_addr_suffix[7];
            ipv6_addr_suffix[7] = tmpb;

            extended_addr = new byte[8];
            Array.Copy(data, 24, extended_addr, 0, 8);
            capability_info = data[32];
            reserved = data[33];
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Attach Indication (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            string ipv6_prefix_str = BitConverter.ToString(ipv6_addr_prefix).Replace("-", " ");
            string ipv6_suffix_str = BitConverter.ToString(ipv6_addr_suffix).Replace("-", " ");
            sb.AppendLine(string.Format("ipv6 addr {0} {1}", ipv6_prefix_str, ipv6_suffix_str));
            string ext_addr_str = BitConverter.ToString(extended_addr).Replace("-", " ");
            sb.Append(string.Format("extended addr -> {0}", ext_addr_str ));
            return sb.ToString();
        }

    }


    public class DetachRequest: MessageHeader
    {
        public byte[] ext_addr;

        public DetachRequest(byte[] x_addr)
        {
            message_type = (byte)CommandTypes.DETACH;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)12;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            if (x_addr.Length != 8)
            {
                throw new Exception(string.Format("Extended address size is 8 bytes, received {0} bytes", x_addr.Length) );
            }
            ext_addr = new byte[x_addr.Length];
            Array.Copy(x_addr, 0, ext_addr, 0, x_addr.Length);
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[8 + 8];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            Array.Copy(ext_addr, 0, bytes, 8, ext_addr.Length);
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 8);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("Detach Network Request, extended addr -> 0x{0:x4}", BitConverter.ToString(ext_addr).Replace("-", " ")));
            return sb.ToString();
        }

    }

    public class DetachConfirm: MessageHeader
    {
        public ushort status;
        public byte[] ext_addr;

        public DetachConfirm(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 8 + 2 + header is 8
            if (data.Length != 18)
                throw new Exception(string.Format("Invalid Detach Confirm received, expected 18 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            status = BitConverter.ToUInt16(data, 8);
            ext_addr = new byte[8];
            Array.Copy(data, 10, ext_addr, 0, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Detach confirm: status {0}, extended addr {1}", status, BitConverter.ToString(ext_addr).Replace("-", ":"));
            return sb.ToString();
        }

    }

    public class DetachIndication : MessageHeader
    {
        public byte[] extended_addr; // 8 bytes

        public DetachIndication(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            // payload length is 8 + header is 8
            if (data.Length != 16)
                throw new Exception(string.Format("Invalid Detach Indication received, expected 16 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            extended_addr = new byte[8];
            Array.Copy(data, 8, extended_addr, 0, 8);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Detach Indication (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }
            string ext_addr_str = BitConverter.ToString(extended_addr).Replace("-", " ");
            sb.Append(string.Format("extended addr -> {0}", ext_addr_str));
            return sb.ToString();
        }

    }

    public class DataTransferIndication : MessageHeader
    {
        public byte link_quality;
        public bool confirmation_flag; // CFM in document; // confirmation flag
        public byte phase;
        public bool security_enabled; // SEC in document
        public byte[] data_recv;
        public Ipv6Header ipv6Header = null;
        public UdpHeader udpHeader = null;
        public TcpHeader tcpHeader = null;

        public DataTransferIndication(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            int payload_len = BitConverter.ToUInt16(data, 2);
            // payload length is 8 + header is 8
            if (data.Length < payload_len + 4)
            {
                throw new Exception(string.Format("Invalid Data Indication received, expected at least {0} bytes, got {1} bytes", payload_len + 4, data.Length));
            }

            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            link_quality = data[8];
            if ( (data[9] & 0x01) == 0x01)
            {
                security_enabled = true;
            }
            else
            {
                security_enabled = false;
            }
            if ( (data[9] & 0x80) == 0x80)
            {
                confirmation_flag = true;
            }
            else
            {
                confirmation_flag = false;
            }
            phase = (byte)((data[9] >> 3) & 0x0f);
            // ipv6 frame sent here; so get the frame and then the data
            int ipv6_bytes_used = 0;
            ipv6Header = Ipv6Header.Create(data, 10, ref ipv6_bytes_used);

            int tmp_bytes_used = 0;
            int actual_data_len;
            if (ipv6Header.NextHeader == 17)
            {
                udpHeader = UdpHeader.Create(data, 10 + ipv6_bytes_used, ref tmp_bytes_used);
                actual_data_len = udpHeader.Length - 8;
            }
            else
            {
                tcpHeader = TcpHeader.Create(data, 10 + ipv6_bytes_used, ref tmp_bytes_used);
                ushort tcp_pkt_len = (ushort)System.Net.IPAddress.HostToNetworkOrder((short)ipv6Header.PayloadLength);
                actual_data_len = tcp_pkt_len - tmp_bytes_used;
            }

            if (actual_data_len <= 0)
            {
                throw new Exception(string.Format("No data in TCP/UDP packet in Data Indication received"));
            }
            data_recv = new byte[actual_data_len];
            Array.Copy(data, 10+ipv6_bytes_used + tmp_bytes_used, data_recv, 0, actual_data_len);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Data Indication (from device)");
            if (displayRAW)
            {
                string bytes_str = BitConverter.ToString(bytes).Replace("-", " ");
                sb.AppendLine(bytes_str);
            }

            if (udpHeader != null)
            {
                sb.AppendFormat("From: {0}:{1} To: {2}:{3}", ipv6Header.SourceAddress.ToString(),
                    udpHeader.SourcePort.ToString(), ipv6Header.DestinationAddress.ToString(),
                    udpHeader.DestinationPort.ToString());
            }
            else if (tcpHeader != null)
            {
                sb.AppendFormat("From: {0}:{1} To: {2}:{3}", ipv6Header.SourceAddress.ToString(),
                    tcpHeader.SourcePort.ToString(), ipv6Header.DestinationAddress.ToString(),
                    tcpHeader.DestinationPort.ToString());
            }

            string data_str = BitConverter.ToString(data_recv).Replace("-", " ");
            sb.Append(string.Format("recv data -> {0}", data_str));
            return sb.ToString();
        }


    }

    public class DataTransferRequest : MessageHeader
    {
        private byte nsdu_handle; // rnd num between 0x00 and 0xff
        private bool qos_normal_priority; // 0 => normal priority, 1 => high prirority
        private bool security_enabled; // 1 -> eanbled 0 -> disabled
        private bool discovery_route_enabled; // 1 -> enabled 0 -> disabled
        private byte[] send_bytes;
        private Ipv6Header ipv6Header;
        private UdpHeader udpHeader;
        private TcpHeader tcpHeader;
        private bool makeUDPPacket;

        public byte NsduHandle {
            get {
                return nsdu_handle;
            }
        }
 
        public DataTransferRequest(byte[] data_to_send_b, byte[] send_ipv6_addr, bool isUDP, byte loc_nsdu_handle)
        {
            send_bytes = data_to_send_b;
            message_type = (byte)CommandTypes.DATA_TRANSFER;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            ipv6Header = new Ipv6Header();
            //string ipv6_src_addr = BitConverter.ToString(send_ipv6_addr).Replace("-", ":");
            //System.Net.IPAddress src_addr;
            //bool succ = System.Net.IPAddress.TryParse(ipv6_src_addr, out src_addr);
            //if (!succ)
            //{
            //    throw new Exception("Failed parsing dest ipv6 addr as a system.net.ipaddress object from " + ipv6_src_addr);
            //}
            System.Net.IPAddress src_addr = new System.Net.IPAddress(send_ipv6_addr);
            System.Net.IPAddress dest_addr = new System.Net.IPAddress(send_ipv6_addr);
            ipv6Header.Version = 6;
            ipv6Header.TrafficClass = 1;
            ipv6Header.Flow = 2;
            ipv6Header.HopLimit = 2;
            this.makeUDPPacket = isUDP;
            if (isUDP)
            {
                ipv6Header.NextHeader = 17;
            }
            else
            {
                ipv6Header.NextHeader = 6;
            }
            ipv6Header.SourceAddress = src_addr;
            ipv6Header.DestinationAddress = dest_addr;
            if (isUDP)
            {
                ipv6Header.PayloadLength = (ushort)(UdpHeader.UdpHeaderLength + data_to_send_b.Length);
                udpHeader = new UdpHeader();
                udpHeader.ipv6PacketHeader = ipv6Header;
                udpHeader.SourcePort = 0;
                udpHeader.DestinationPort = 0;
                udpHeader.Length = (ushort)(UdpHeader.UdpHeaderLength + data_to_send_b.Length);
                udpHeader.Checksum = 0;
            }
            else
            {
                ipv6Header.PayloadLength = (ushort)(TcpHeader.TcpHeaderLength + data_to_send_b.Length);
                tcpHeader = new TcpHeader();
                tcpHeader.ipv6PacketHeader = ipv6Header;
                tcpHeader.SourcePort = 0;
                tcpHeader.DestinationPort = 0;
                tcpHeader.Checksum = 0;
            }

            payload_length_lsb = (byte)0; // cannot compute length now...
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            // Random random = new Random();
            // int val = (int)(255 * random.NextDouble());
            // nsdu_handle = (byte)val;
            nsdu_handle = loc_nsdu_handle;
            qos_normal_priority = true;
            security_enabled = false;
            discovery_route_enabled = true;
            ComputeBytes();
        }

        public DataTransferRequest(byte[] data_to_send_b, byte[] send_ipv6_addr, byte[] dest_ipv6_addr, bool isUDP, byte loc_nsdu_handle)
        {
            send_bytes = data_to_send_b;
            message_type = (byte)CommandTypes.DATA_TRANSFER;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            ipv6Header = new Ipv6Header();
            //string ipv6_src_addr = BitConverter.ToString(send_ipv6_addr).Replace("-", ":");
            //System.Net.IPAddress src_addr;
            //bool succ = System.Net.IPAddress.TryParse(ipv6_src_addr, out src_addr);
            //if (!succ)
            //{
            //    throw new Exception("Failed parsing dest ipv6 addr as a system.net.ipaddress object from " + ipv6_src_addr);
            //}
            System.Net.IPAddress src_addr = new System.Net.IPAddress(send_ipv6_addr);
            System.Net.IPAddress dest_addr = new System.Net.IPAddress(dest_ipv6_addr);
            ipv6Header.Version = 6;
            ipv6Header.TrafficClass = 1;
            ipv6Header.Flow = 2;
            ipv6Header.HopLimit = 2;
            this.makeUDPPacket = isUDP;
            if (isUDP)
            {
                ipv6Header.NextHeader = 17;
            }
            else
            {
                ipv6Header.NextHeader = 6;
            }
            ipv6Header.SourceAddress = src_addr;
            ipv6Header.DestinationAddress = dest_addr;
            if (isUDP)
            {
                ipv6Header.PayloadLength = (ushort)(UdpHeader.UdpHeaderLength + data_to_send_b.Length);
                udpHeader = new UdpHeader();
                udpHeader.ipv6PacketHeader = ipv6Header;
                udpHeader.SourcePort = 0;
                udpHeader.DestinationPort = 0;
                udpHeader.Length = (ushort)(UdpHeader.UdpHeaderLength + data_to_send_b.Length);
                udpHeader.Checksum = 0;
            }
            else
            {
                ipv6Header.PayloadLength = (ushort)(TcpHeader.TcpHeaderLength + data_to_send_b.Length);
                tcpHeader = new TcpHeader();
                tcpHeader.ipv6PacketHeader = ipv6Header;
                tcpHeader.SourcePort = 0;
                tcpHeader.DestinationPort = 0;
                tcpHeader.Checksum = 0;
            }

            payload_length_lsb = (byte)0; // cannot compute length now...
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            // Random random = new Random();
            // int val = (int)(255 * random.NextDouble());
            // nsdu_handle = (byte)val;
            nsdu_handle = loc_nsdu_handle;
            qos_normal_priority = true;
            security_enabled = false;
            discovery_route_enabled = true;
            ComputeBytes();
        }


        private void ComputeBytes()
        {
            //byte[] send_array = Encoding.ASCII.GetBytes(send_bytes);
            byte[] send_array = new byte[send_bytes.Length];
            Array.Copy(send_bytes, send_array, send_bytes.Length); 

            //byte[] ipv6_packet = udpHeader.GetProtocolPacketBytes(send_array);
            byte[] builtPacket;
            ArrayList headerList = new ArrayList();
            headerList.Add(ipv6Header);
            if (makeUDPPacket)
            {
                headerList.Add(udpHeader);
                builtPacket = udpHeader.BuildPacket(headerList, send_array);
            }
            else
            {
                headerList.Add(tcpHeader);
                builtPacket = tcpHeader.BuildPacket(headerList, send_array);
            }
            int pad_len = builtPacket.Length % 2;
            bytes = new byte[8 + 2 + builtPacket.Length + pad_len];
            bytes[0] = message_type;
            bytes[1] = options;
            payload_length_lsb = (byte)((6 + builtPacket.Length) & 0xFF);
            payload_length_msb = (byte)(((6 + builtPacket.Length) & 0xFF00) >> 8); 
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            bytes[8] = nsdu_handle;
            bytes[9] = (byte) (0x00 | (discovery_route_enabled ? 1 : 0) | (security_enabled? 2 : 0) | (qos_normal_priority ? 0 : 4) );
            Array.Copy(builtPacket, 0, bytes, 10, builtPacket.Length);

            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 2 + builtPacket.Length);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            if (pad_len == 1)
            {
                bytes[bytes.Length - 1] = 0;
            }
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("Send DATA:  TODO"));
            return sb.ToString();
        }

    }

    public class DataTransferConfirm : MessageHeader
    {
        public byte nsdu_handle;
        public bool confirmation_flag;
        public ushort status;

        public DataTransferConfirm(byte[] message_bytes)
        {
            FromBytes(message_bytes);

        }

        private void FromBytes(byte[] data)
        {
            // payload length is 4 + header is 8
            if (data.Length != 12)
                throw new Exception(string.Format("Invalid Data Transfer confirm received, expected 12 bytes, got only {0} bytes", data.Length));
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];
            nsdu_handle = data[8];
            if ( (data[9] & 0x80) == 0x80 )
            {
                confirmation_flag = true;
            }
            else
            {
                confirmation_flag = false;
            }
            status = BitConverter.ToUInt16(data, 10);
            bytes = new byte[data.Length];
            Array.Copy(data, bytes, data.Length);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Data Transfer Confirm :" + System.Environment.NewLine);
            sb.AppendFormat("nsdu_handle: 0x{0:x2}, confirmation flag: {1}, status {2}", nsdu_handle, confirmation_flag, Utils.GetErrorString(status));
            return sb.ToString();
        }
    }


    public class DiscoverRequest : MessageHeader
    {
        private byte discover_type;
        private int discover_len;
        private byte scan_duration; // for network and exhaustive beacond discovery
        private byte max_hops; // for route discover
        private ushort dst_addr; // for route discovery and path discovery
        private byte path_metric_type; // for path discovery


        public DiscoverRequest(byte discoverType, params object[] param_list)
        {
            discover_type = discoverType;
            switch(discoverType)
            {
                case (byte)DiscoverTypes.NETWORK:
                case (byte)DiscoverTypes.EXHAUSTIVE_BEACON:
                    if (param_list.Length != 1)
                        throw new Exception(" Invalid number of elements for Discover Network Request");
                    if (param_list[0] is byte)
                    {
                        scan_duration = (byte)param_list[0];
                    }
                    else
                    {
                        throw new Exception(" Expected (byte) for Discover Network Request");
                    }
                    discover_len = 4 + 2; // base header len is 4 + 1 byte discover type + scan duration
                    break;
                case (byte)DiscoverTypes.ROUTE:
                    if (param_list.Length != 2)
                        throw new Exception(" Invalid number of elements for Route Discovery");
                    if ( (param_list[0] is byte ) && (param_list[1] is ushort) )
                    {
                        max_hops = (byte) param_list[0];
                        dst_addr = (ushort) param_list[1];
                    }
                    else
                    {
                        throw new Exception(" Expected (byte, ushort) for Route Discovery");
                    }
                    discover_len = 4 + 3; // header len 4 + base hops 1 + dst addr 2
                    break;
                case (byte)DiscoverTypes.PATH:
                    if (param_list.Length != 2)
                        throw new Exception(" Invalid number of elements for Path Discovery");
                    if ( (param_list[0] is byte ) && (param_list[1] is ushort) )
                    {
                        path_metric_type = (byte) param_list[0];
                        dst_addr = (ushort) param_list[1];
                    }
                    else
                    {
                        throw new Exception(" Expected (byte, ushort) for Route Discovery");
                    }
                    discover_len = 4 + 3; // header len 4 + metric type 1 + dst addr 2
                    break;

                default:
                    throw new Exception(string.Format("Invalid Discover Request Type {0}", discoverType));
            }
            message_type = (byte)CommandTypes.DISCOVER;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            // need to add 4 = header crc + payload crc
            payload_length_lsb = (byte)((discover_len) & 0x00FF);
            payload_length_msb = (byte)(((discover_len) & 0xFF00) >> 8);
            //ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //header_crc16[0] = (byte)(crc16 & 0xFF);
            //header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            header_crc16[0] = 0;
            header_crc16[1] = 0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();

        }

        private void ComputeBytes()
        {
            // total bytes = info_len + 4 + header len (8 bytes)
            int pad_len = (discover_len) % 2;

            bytes = new byte[4 + discover_len + pad_len];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];

            bytes[8] = discover_type;
            if (discover_type == (byte)DiscoverTypes.NETWORK)
            {
                bytes[9] = scan_duration;
            }
            else if (discover_type == (byte)DiscoverTypes.EXHAUSTIVE_BEACON)
            {
                bytes[9] = scan_duration;
            } else if (discover_type == (byte)DiscoverTypes.ROUTE)
            {
                bytes[9] = max_hops;
                byte[] tmpb = BitConverter.GetBytes(dst_addr);
                bytes[10] = tmpb[0];
                bytes[11] = tmpb[1];
            }
            else if (discover_type == (byte)DiscoverTypes.PATH )
            {
                bytes[9] = path_metric_type;
                byte[] tmpb2 = BitConverter.GetBytes(dst_addr);
                bytes[10] = tmpb2[0];
                bytes[11] = tmpb2[1];

            }
            if (pad_len == 1)
                bytes[bytes.Length - 1] = 0;

            crc16 = MessageHeader.ComputeCRC16(bytes, 8, bytes.Length - 8);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (discover_type)
            {
                case (byte)DiscoverTypes.NETWORK:
                    sb.AppendFormat("Disover Network: scan duration secs {0}", scan_duration);
                    break;
                case (byte)DiscoverTypes.EXHAUSTIVE_BEACON:
                    sb.AppendFormat("Exhaustive Beacond Search: scan duration secs {0}", scan_duration);
                    break;
                case (byte)DiscoverTypes.ROUTE:
                    sb.AppendFormat("Router Discovery: max hops {0}, dst Addr 0x{1:x4}", max_hops, dst_addr);
                    break;
                case (byte)DiscoverTypes.PATH:
                    sb.AppendFormat("Path Discovery: metric type {0}, dst Addr 0x{1:x4}", path_metric_type, dst_addr);
                    break;
                default:
                    sb.AppendFormat("Unknown Discver Type Request ({0})", discover_type);
                    break;
            }
            return sb.ToString();
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }


    }


    public class PANDesc_v7000_later
    {
        public byte coord_address_mode;
        public byte lqi; // link becon quality
        public ushort coord_pan_id;
        public ushort coord_addr;
        public ushort routing_cost;
        public static int Length = 8;

        public PANDesc_v7000_later(byte[] message_bytes, int start_idx = 0)
        {
            FromBytes(message_bytes, start_idx);
        }

        private void FromBytes(byte[] data, int start_idx)
        {
            int ii = start_idx;
            coord_address_mode = data[ii++];
            lqi = data[ii++];
            coord_pan_id = BitConverter.ToUInt16(data, ii);
            ii += 2;
            coord_addr = BitConverter.ToUInt16(data, ii); 
            ii += 2;
            routing_cost = BitConverter.ToUInt16(data, ii);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("PAN Desc: mode 0x{0:x2}, pan id 0x{1:x4}, ", coord_address_mode, coord_pan_id);
            sb.AppendLine();
            sb.AppendFormat("cooord short Addr {0}", coord_addr );
            return sb.ToString();
        }

    }

    public class PANDesc_before_7000
    {
        // PAN DESC for DSP before version 7.1.x.20 
        public byte coord_address_mode;
        public byte lqi; // link becon quality
        public ushort coord_pan_id;
        public ushort coord_short_addr;
        public static int Length = 6;

        public PANDesc_before_7000(byte[] message_bytes, int start_idx = 0)
        {
            FromBytes(message_bytes, start_idx);
        }

        private void FromBytes(byte[] data, int start_idx)
        {
            int ii = start_idx;
            coord_address_mode = data[ii++];
            lqi = data[ii++];
            coord_pan_id = BitConverter.ToUInt16(data, ii);
            ii += 2;
            coord_short_addr = BitConverter.ToUInt16(data, ii);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("PAN Desc: mode 0x{0:x2}, pan id 0x{1:x4}, ", coord_address_mode, coord_pan_id);
            sb.AppendLine();
            sb.AppendFormat("cooord short Addr 0x{0:x4}", coord_short_addr );
            return sb.ToString();
        }

    }


    public class DiscoverConfirmSubNetwork: MessageHeader
    {
        public byte discover_type;
        public byte reserved;
        public ushort status;
        public List<PANDesc_v7000_later> networks_found = new List<PANDesc_v7000_later>();

        public DiscoverConfirmSubNetwork(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        
        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            discover_type = data[8];
            reserved = data[9];
            //status = BitConverter.ToUInt16(data, 8);
            //ushort nw_count = BitConverter.ToUInt16(data, 10);
            //networks_found.Clear();
            //int jj = 12;
            status = BitConverter.ToUInt16(data, 10);
            ushort nw_count = BitConverter.ToUInt16(data, 12);
            networks_found.Clear();
            int jj = 14;
            for (int ii = 0; ii < (int)nw_count; ii++)
            {
                PANDesc_v7000_later panDesc = new PANDesc_v7000_later(data, jj);
                networks_found.Add(panDesc);
                jj += PANDesc_v7000_later.Length;
            }

        }
        

        /*
        private void FromBytes_v7120_later(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            discover_type = data[8];
            reserved = data[9];
            status = BitConverter.ToUInt16(data, 10);
            ushort nw_count = BitConverter.ToUInt16(data, 12);
            networks_found.Clear();
            int jj = 14;
            for (int ii=0; ii < (int)nw_count; ii ++)
            {
                PANDesc_v7120_later panDesc = new PANDesc_v7120_later(data, jj);
                networks_found.Add(panDesc);
                jj += PANDesc_v7120_later.Length;
            }

        }
        */

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Status {0}: Found {1} networks", Utils.GetErrorString(status), networks_found.Count);
            sb.AppendLine();
            for (int ii = 0; ii < networks_found.Count; ii++ )
            {
                sb.Append(networks_found[ii].ToString());
                if (ii != networks_found.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

    }

    public class DiscoverConfirmSubRouteDiscovery : MessageHeader
    {
        public byte discover_type;
        public byte reserved;
        public ushort status;

        public DiscoverConfirmSubRouteDiscovery(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            discover_type = data[8];
            reserved = data[9];
            status = BitConverter.ToUInt16(data, 10);
 
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Status {0} ", Utils.GetErrorString(status));
            return sb.ToString();
        }

    }

    public class PathDesc
    {
        public ushort path_addr;
        public byte mns; // zero or 1
        public byte link_cost;
        public static int Length = 4; // length of bytes in a byte[] message 

        public PathDesc(byte[] message_bytes, int start_idx = 0)
        {
            FromBytes(message_bytes, start_idx);
        }

        private void FromBytes(byte[] data, int start_idx)
        {
            path_addr = BitConverter.ToUInt16(data, start_idx);
            mns = data[start_idx + 2];
            link_cost = data[start_idx + 3];
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Path Desc -> path addr {0}, mns {1}, link cost -> {2}", path_addr, mns, link_cost);
            return sb.ToString();
        }
    }

    public class DiscoverConfirmSubPathDiscovery: MessageHeader
    {
        public byte discover_type;
        public byte reserved;
        public ushort status;
        public ushort dest_addr;
        public ushort path_table_dest_addr; // Network order
        public ushort path_table_expected_originator;  // Network order
        public byte metric_type;
        public byte reserved2;
        public ushort path_table_originator;
        public List<PathDesc> path_descs = new List<PathDesc>();

        public DiscoverConfirmSubPathDiscovery(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            discover_type = data[8];
            reserved = data[9];
            status = BitConverter.ToUInt16(data, 10);
            dest_addr = BitConverter.ToUInt16(data, 12);
            byte[] tmpb = new byte[2];
            tmpb[0] = data[15];
            tmpb[1] = data[14];
            path_table_dest_addr = BitConverter.ToUInt16(tmpb, 0);
            tmpb[0] = data[16];
            tmpb[1] = data[17];
            path_table_expected_originator = BitConverter.ToUInt16(tmpb, 0);
            metric_type = data[18];
            reserved2 = data[19];
            tmpb[0] = data[21];
            tmpb[1] = data[20];
            path_table_originator = BitConverter.ToUInt16(tmpb, 0);
            int count = (data.Length - 22) / PathDesc.Length;
            path_descs.Clear();
            int jj = 22;
            while (jj < data.Length)
            {
                PathDesc pd = new PathDesc(data, jj);
                path_descs.Add(pd);
                jj += PathDesc.Length;
            }

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Path discovery confirm: dest addr {0}, Path table dest addr {1}", dest_addr, path_table_dest_addr);
            sb.AppendLine();
            sb.AppendFormat("path table expected originator {0}, path table originator {1}, metric {2}", path_table_expected_originator, path_table_originator, metric_type);
            sb.AppendLine();
            sb.AppendFormat("Status {0}: Found {1} path desc", Utils.GetErrorString(status), path_descs.Count);
            sb.AppendLine();
            for (int ii = 0; ii < path_descs.Count; ii++)
            {
                sb.Append(path_descs[ii].ToString());
                if (ii != path_descs.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

    }

    public class DiscoverConfirmExhaustiveBeacon : MessageHeader
    {
        // A Discover.Confirm with PAN Count =1 and Status = 0x50E7 , shall be generated for every received beacon
        public byte discover_type;
        public byte reserved;
        public ushort status;
        public List<PANDesc_v7000_later> networks_found = new List<PANDesc_v7000_later>();

        public DiscoverConfirmExhaustiveBeacon(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            discover_type = data[8];
            reserved = data[9];
            status = BitConverter.ToUInt16(data, 10);
            ushort nw_count = BitConverter.ToUInt16(data, 12);
            networks_found.Clear();
            int jj = 14;
            for (int ii = 0; ii < (int)nw_count; ii++)
            {
                PANDesc_v7000_later panDesc = new PANDesc_v7000_later(data, jj);
                networks_found.Add(panDesc);
                jj += PANDesc_v7000_later.Length;
            }

        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Status {0}: Found {1} networks", Utils.GetErrorString(status), networks_found.Count);
            sb.AppendLine();
            for (int ii = 0; ii < networks_found.Count; ii++)
            {
                sb.Append(networks_found[ii].ToString());
                if (ii != networks_found.Count - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

    }

    public class GetPHYPIBReuest: MessageHeader
    {
        public ushort attribute_id;

        public GetPHYPIBReuest( ushort loc_attribute_id)
        {
            attribute_id = loc_attribute_id;

            message_type = (byte)CommandTypes.GET_PHY_PIB;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)6;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[10];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(attribute_id);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 2);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("GetPHYPIB id " + attribute_id.ToString());
            return sb.ToString();
        }

    }

    public class GetPHYPIBResponse: MessageHeader
    {
        public ushort status;
        public ushort attribute_id;
        public ushort attribute_len;
        public byte[] attribute_vals_b;

        public GetPHYPIBResponse(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            status = BitConverter.ToUInt16(data, 8);
            attribute_id = BitConverter.ToUInt16(data, 10);
            attribute_len = BitConverter.ToUInt16(data, 12);
            attribute_vals_b = new byte[attribute_len];
            Array.Copy(data, 14, attribute_vals_b, 0, attribute_len);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("GetPHYPIBResponse Status {0}: attribute id {1} ", Utils.GetErrorString(status), attribute_id);
            sb.AppendLine();
            sb.AppendFormat("attribute value -> {0}", BitConverter.ToString(attribute_vals_b).Replace("-", ""));
            sb.AppendLine();
            return sb.ToString();
        }


    }

    public class GetMACPIBReuest : MessageHeader
    {
        public uint attribute_id;
        public ushort attribute_index; // can be zero for non-table parameters

        public GetMACPIBReuest(uint loc_attribute_id, ushort loc_attribute_index = 0)
        {
            attribute_id = loc_attribute_id;
            attribute_index = loc_attribute_index;

            message_type = (byte)CommandTypes.GET_MAC_PIB;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            payload_length_lsb = (byte)10;
            payload_length_msb = (byte)0;
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[14];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(attribute_id);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            bytes[10] = tmpu_b[2];
            bytes[11] = tmpu_b[3];
            tmpu_b = BitConverter.GetBytes(attribute_index);
            bytes[12] = tmpu_b[0];
            bytes[13] = tmpu_b[1];

            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 6);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("GetMACPIB id 0x{0:x4} [{1}]",  attribute_id.ToString(), attribute_index);
            sb.AppendLine();
            return sb.ToString();
        }

    }

    public class GetMACPIBResponse : MessageHeader
    {
        public ushort status;
        public uint attribute_id;
        public ushort attribute_index; 
        public ushort attribute_len;
        public byte[] attribute_vals_b;

        public GetMACPIBResponse(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            status = BitConverter.ToUInt16(data, 8);
            attribute_id = BitConverter.ToUInt32(data, 10);
            attribute_index = BitConverter.ToUInt16(data, 14);
            attribute_len = BitConverter.ToUInt16(data, 16);
            attribute_vals_b = new byte[attribute_len];
            Array.Copy(data, 18, attribute_vals_b, 0, attribute_len);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("GetMACPIBResponse Status {0}: attribute id {1} ", Utils.GetErrorString(status), attribute_id);
            sb.AppendLine();
            sb.AppendFormat("attribute value[{0}] -> {1}", attribute_index, BitConverter.ToString(attribute_vals_b).Replace("-", ""));
            sb.AppendLine();
            return sb.ToString();
        }

    }


    // G3 PSK PIB id is TI propritary PIB and not found in G3 MAC Spec
    // G3 PSK PIB id => 0x1F, value is 8 bytes
    public class SetMACPIBReuest : MessageHeader
    {
        public uint attribute_id;
        public ushort attribute_index; // can be zero for non-table parameters
        public byte[] attribute_vals;


        public SetMACPIBReuest(uint loc_attribute_id, byte[] loc_attribute_vals, ushort loc_attribute_index = 0)
        {

            if (loc_attribute_vals == null)
            {
                throw new Exception(string.Format("SEtMACPIB request cannot except null values for attribute values"));
            }
            attribute_id = loc_attribute_id;
            attribute_index = loc_attribute_index;
            attribute_vals = new byte[loc_attribute_vals.Length];
            Array.Copy(loc_attribute_vals, attribute_vals, loc_attribute_vals.Length);

            message_type = (byte)CommandTypes.SET_MAC_PIB;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            byte[] tmp_b = BitConverter.GetBytes((ushort) (12+loc_attribute_vals.Length) );
            payload_length_lsb = tmp_b[0];
            payload_length_msb = tmp_b[1];
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            bytes = new byte[4+12+attribute_vals.Length];
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(attribute_id);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            bytes[10] = tmpu_b[2];
            bytes[11] = tmpu_b[3];
            tmpu_b = BitConverter.GetBytes(attribute_index);
            bytes[12] = tmpu_b[0];
            bytes[13] = tmpu_b[1];
            tmpu_b = BitConverter.GetBytes((ushort)attribute_vals.Length);
            bytes[14] = tmpu_b[0];
            bytes[15] = tmpu_b[1];

            Array.Copy(attribute_vals, 0, bytes, 16, attribute_vals.Length);

            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 8+attribute_vals.Length);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("SetMACPIB id 0x{0:x4} [{1}]", attribute_id, attribute_index);
            sb.AppendLine();
            sb.AppendFormat("value {0} ", BitConverter.ToString(attribute_vals).Replace("-", ""));
            sb.AppendLine();
            return sb.ToString();
        }

    }

    public class SetMACPIBResponse : MessageHeader
    {
        public ushort status;

        public SetMACPIBResponse(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            status = BitConverter.ToUInt16(data, 8);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("SetMACPIBResponse Status {0}", Utils.GetErrorString(status));
            sb.AppendLine();
            return sb.ToString();
        }

    }

    public class SetAlarmRequest : MessageHeader
    {
        public ushort alarm_type;
        public bool turn_on; // if true, it turns on the alarm, else turs off the alarm, sets the clr bit i.e bit 0 in value


        public SetAlarmRequest(ushort loc_alarm_type, bool loc_turn_on)
        {

            if (!AlarmTypes.IsDefined (typeof(AlarmTypes), loc_alarm_type))
            {
                throw new Exception(string.Format("Alarm types supported are: 0x0007 and 0x00ff"));
            }
            alarm_type = loc_alarm_type;
            turn_on = loc_turn_on;

            message_type = (byte)CommandTypes.SETUP_ALARM;
            // ORG bit is 1, RPY bit is 0, seq is 0
            options = 0x80; // 1000 0000
            byte[] tmp_b = BitConverter.GetBytes((ushort)(9));
            payload_length_lsb = tmp_b[0];
            payload_length_msb = tmp_b[1];
            payload_crc16[0] = 0;
            payload_crc16[1] = 0;
            ComputeBytes();
        }

        private void ComputeBytes()
        {
            int msg_len = 8 + 4 + 1;// 8 header, 4 => alarm type and alarm data length, 1 alarm data
            int pad_len = msg_len % 2;
            bytes = new byte[msg_len + pad_len]; 
             
            bytes[0] = message_type;
            bytes[1] = options;
            bytes[2] = payload_length_lsb;
            bytes[3] = payload_length_msb;
            ushort crc16 = MessageHeader.ComputeCRC16(bytes, 0, 4);
            //ushort testcrs16 = GenCrc16(bytes, 4);
            header_crc16[0] = (byte)(crc16 & 0xFF);
            header_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[4] = header_crc16[0];
            bytes[5] = header_crc16[1];
            byte[] tmpu_b = BitConverter.GetBytes(alarm_type);
            bytes[8] = tmpu_b[0];
            bytes[9] = tmpu_b[1];
            // alarm length of additional data
            bytes[10] = 0x01;
            bytes[11] = 0x00;
            if (turn_on)
            {
                bytes[12] = 0x00;
            }
            else
            {
                bytes[12] = 0x01;
            }

            crc16 = MessageHeader.ComputeCRC16(bytes, 8, 5);
            payload_crc16[0] = (byte)(crc16 & 0xFF);
            payload_crc16[1] = (byte)((crc16 & 0xFF00) >> 8);
            bytes[6] = payload_crc16[0];
            bytes[7] = payload_crc16[1];
            if (pad_len == 1)
            {
                bytes[bytes.Length - 1] = 0;
            }
            byte_arr_computed = true;
        }

        public byte[] ToBytes()
        {
            if (!byte_arr_computed)
            {
                ComputeBytes();
            }
            return bytes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("SetAlarm type 0x{0:x2} [{1}]", alarm_type, turn_on ? "on" : "off");
            sb.AppendLine();
            return sb.ToString();
        }

    }

    public class SetAlarmResponse : MessageHeader
    {
        public ushort status;

        public SetAlarmResponse(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            status = BitConverter.ToUInt16(data, 8);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("SetAlarmResponse Status {0}", Utils.GetErrorString(status));
            sb.AppendLine();
            return sb.ToString();
        }

    }

    public class Alarm : MessageHeader
    {
        public ushort alarm_type;
        public ushort alarm_len;
        public byte[] alarm_b = null;

        public Alarm(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            alarm_type = BitConverter.ToUInt16(data, 8);
            alarm_len = BitConverter.ToUInt16(data, 10);
            alarm_b = new byte[alarm_len];
            Array.Copy(data, 12, alarm_b, 0, alarm_len);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Alarm Type [0x{0:x2}] -> {1}", alarm_type, BitConverter.ToString(alarm_b).Replace("-", ""));
            sb.AppendLine();
            return sb.ToString();
        }

    }
    
    public class AlarmSubNetworkStatus : MessageHeader
    {
        public ushort alarm_type;
        public ushort alarm_len;
        public byte[] alarm_b = null;
        ushort status;
        /* ERROR CODES
            Counter Error                       0xDB
            Improper Key Type                   0xDC
            Unsupported Security                0xDF
            Sync Loss (Conflict PAN ID)         0x21F
            Security Error                      0xE4
            Frame Too Long                      0xE5
            Invalid Index                       0xF9         
         */
        byte security_level;
        byte key_id_mode;
        ushort key_index;
        ushort pan_id;
        ushort addr_mode;
        // based on addr_mode values: if addr_mode = 0 => no addr (address: 0 bytes), 
        //                               addr_mode = 2 => short address (address: 2 bytes), 
        //                               addr_mode = 3 => long address (address: 8bytes)
        byte[] address = null;
        public AlarmSubNetworkStatus(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            message_type = data[0];
            options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            alarm_type = BitConverter.ToUInt16(data, 8);
            alarm_len = BitConverter.ToUInt16(data, 10);
            alarm_b = new byte[alarm_len];
            Array.Copy(data, 12, alarm_b, 0, alarm_len);
            status = BitConverter.ToUInt16(data, 12);
            security_level = data[14];
            key_id_mode = data[15];
            key_index = BitConverter.ToUInt16(data, 16);
            pan_id = BitConverter.ToUInt16(data, 18);
            addr_mode = BitConverter.ToUInt16(data, 20);
            if (addr_mode == 0)
            {
                // no address
                address = null;
            }
            else if (addr_mode == 2)
            {
                address = new byte[2];
                address[0] = data[22];
                address[1] = data[23];
            }
            else if (addr_mode == 3)
            {
                address = new byte[8];
                Array.Copy(data, 22, address, 0, 8);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Alarm Type Network:  Status -> 0x{0:x2}, pan_id -> 0x{1:x4}", status, pan_id);
            sb.AppendLine();
            sb.AppendFormat("Security level -> 0x{0:x2}, key_id mode -> 0x{1:x2}, key index -> {2:x2}", security_level, key_id_mode, key_index);
            sb.AppendLine();
            if (addr_mode == 0)
            {
                sb.AppendFormat("src short addr -> 0x{0:x4}", BitConverter.ToUInt16(address, 0));
                sb.AppendLine();
            } else if (addr_mode == 3)
            {
                sb.AppendFormat("src long addr -> {0}", BitConverter.ToString(address).Replace("-", ""));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class AlarmSubGeneralError : MessageHeader
    {
        public ushort alarm_type;
        public ushort alarm_len;
        public byte[] alarm_b = null;
        public ushort error;
        public ushort num_octets_discarded;
        public byte alarm_message_type;
        public byte alarm_options;
        public ushort reason;

        public AlarmSubGeneralError(byte[] message_bytes)
        {
            FromBytes(message_bytes);
        }

        private void FromBytes(byte[] data)
        {
            alarm_message_type = data[0];
            alarm_options = data[1];
            payload_length_lsb = data[2];
            payload_length_msb = data[3];
            header_crc16[0] = data[4];
            header_crc16[1] = data[5];
            payload_crc16[0] = data[6];
            payload_crc16[1] = data[7];

            alarm_type = BitConverter.ToUInt16(data, 8);
            alarm_len = BitConverter.ToUInt16(data, 10);
            alarm_b = new byte[alarm_len];
            Array.Copy(data, 12, alarm_b, 0, alarm_len);
            error = BitConverter.ToUInt16(data, 12);
            if (error == (ushort) ErrorInformation.PORT_DISCARDED)
            {
                num_octets_discarded = BitConverter.ToUInt16(data, 14);
            }
            else if (error == (ushort) ErrorInformation.HEADER_UNSUPPORTED_FEATURE)
            {
                // no other information presetn
            }
            else if (error == (ushort) ErrorInformation.MESSAGE_DISCARDED)
            {
                alarm_message_type = data[14];
                alarm_options = data[15];
                reason = BitConverter.ToUInt16(data, 16);
            }
            else if (error == (ushort) ErrorInformation.INVALID_MESSAGE_FORMAT)
            {
                // no other info
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            //String oemString = Enum.GetName(typeof(GroupTypes), GroupTypes.OEM);
            if (ErrorInformation.IsDefined (typeof(ErrorInformation), error) )
            {
                sb.AppendFormat("Alarm Type Error -> {0}", Enum.GetName(typeof(ErrorInformation), error));
                sb.AppendLine();
                if (error == (ushort)ErrorInformation.PORT_DISCARDED)
                {
                    sb.AppendFormat("Num octets discarded-> {0}", num_octets_discarded);
                    sb.AppendLine();
                }
                else if (error == (ushort)ErrorInformation.HEADER_UNSUPPORTED_FEATURE)
                {
                    // no other information presetn
                }
                else if (error == (ushort)ErrorInformation.MESSAGE_DISCARDED)
                {
                    string str_reason = Utils.GetErrorString(reason);;
                    sb.AppendFormat("message type -> 0x{0:x2}, options -> 0x{1:x2}, reason -> {2}", alarm_message_type, alarm_options, str_reason);
                    sb.AppendLine();
                }
                else if (error == (ushort)ErrorInformation.INVALID_MESSAGE_FORMAT)
                {
                    // no other info
                }
            }
            else
            {
                sb.AppendFormat("Alarm type {0}", error);
                // should never come here...
            }
            return sb.ToString();
        }

    }
}
