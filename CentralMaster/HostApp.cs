using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Text.Json;
using CentralMaster.json;
using UtilsLibrary;
using CentralMaster.DataServiceCommunications;
using LogUtils;
using System.Timers;
using System.Security.Cryptography.X509Certificates;
using static System.Net.WebRequestMethods;

namespace CentralMaster
{
	public class HostApp
	{
		#region vars
		private string plc_port_name = "";
		private string ui_connection_name = "";
		private bool app_started = false;
        readonly CentralMaster.HostMessagesMchip host_messaging = new CentralMaster.HostMessagesMchip();
		readonly CentralMaster.Communications.UdpMchipG3Comm udp_conn = new CentralMaster.Communications.UdpMchipG3Comm();
        readonly byte[] default_xadd_base = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x09 };
        private readonly byte[] default_psk =
        {
			0x24, 0x1B, 0xD9, 0x05, 0xDF, 0x98, 0xE3, 0xAF, 
			0xD8, 0xE6, 0xD8, 0x5A, 0x53, 0x06, 0xB0, 0xC7        
		};

		private readonly byte[] default_tonemask =
		{
			0x3f, 0x10, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00			
		};
		private readonly ushort NETWORK_PAN_ID = 0x7542;
		//G3.AttachIndication recv_ai = null;
		private readonly System.Timers.Timer timer = new System.Timers.Timer();
		private readonly Dictionary<string, ServiceNode> comm_ready_node_dict = new Dictionary<string, ServiceNode>();
		private readonly Dictionary<string, ServiceNode> node_name_dict = new Dictionary<string, ServiceNode>();
		private readonly Dictionary<string, string> ext_addr_ipv6_map = new Dictionary<string, string>();
		private bool comm_ready_node_dict_changed = false;
		private readonly object comm_ready_lock = new object();
		private readonly List<ServiceNode> attach_sn_list = new List<ServiceNode>();
		private readonly object attach_sn_lock = new object();
		private Communications.XbeeMessages xbee_messaging = null;
		private Communications.SubnetCommunications subnet_gateway = null;

		private const string LOG_FILE_NAME = "central_master_errors.txt";

		// vars for leds
		System.Threading.Thread led_th = null;
        bool second_timer_exec = false;
        bool half_second_timer_exec = false;
        BlockingCollection<double> led_q = new BlockingCollection<double>(4);
        CancellationTokenSource cts = new CancellationTokenSource();
        private object lockObj = new object();

        private const string PARAMETERS_FILE = "plcdc_params.txt";
        private ushort rd_pan_id;
        private byte[] rd_xaddress = null;
        private byte[] rd_psk = null;
        private int rd_vendor_id;
        private string rd_product_id;
        private byte rd_modulation_type;
        private string rd_tone_maskname;
        private bool rd_agc;
        private ushort rd_gain_value;
		private int rd_transmit_level;
		private byte[] rd_tone_maskvalues = null;
		private int rd_round_robin_task_sleep_secs = 10; // default value
		private int rd_plc_comm_timeout_secs = 5; // default value
		private CancellationTokenSource RRDCTaskCTS = new CancellationTokenSource();
		private ServiceNode attaching_sn = null;
		private BlockingCollection<byte[]> UIMessages = new BlockingCollection<byte[]>();
		private BlockingCollection<string> UIMessagesStr = new BlockingCollection<string>();

		private int rd_rx_timeout_milliseconds = 3000;
		private UInt16 rd_number_of_nodes = 1;
		private UInt16 rd_time_per_node_secs = 3; 
		private double rd_node_reset_time_secs = 180;
		private bool rd_attach_high_priority = false;
		private int rd_confirmation_timeout_secs;
		private int rd_is_subnet_gateway = 0;
		private int rd_subnet_use_ethernet = 1; // 1 = use ethernet; 0 => use rf link
		private string rd_subnetdc_ipaddress = "192.168.1.200"; // default
		private int rd_subnetdc_port = 9001; // default
		private string rd_subnetdc_identifier = "None"; // default
		private bool rd_sendtocloud = false;
		private string rd_pipeconnectionname = "PLSDCConnection";
		private int attach_seq_delay = 0; // 0 is false; 1 is true

		private ServiceNode sent_cmd_node = null;

		private static byte[] coord_ipv6 = 
		{
			0xFE, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
			0x00, 0x00, 0x00, 0xFF, 0xFE, 0x00, 0x00, 0x00        
		};
		private IDCDataServiceComms dCDataServiceComms = null;
		int plc_gpip_pin = 48;
		System.Device.Gpio.GpioController gpioController=null;
		private StatusServicePipeClient sspc = null;

		private ManualResetEvent plc_init_timeout_event = new ManualResetEvent(false);
		private bool plc_init_resp_recvd = true; // for microchip based systems; false for TI based systems
		private System.Timers.Timer plc_init_timer = new System.Timers.Timer();

		private const string cmd_file_path = @"/media/card/dblite/";
		#endregion //vars

		#region property
		public static byte[] CoordinatorIPv6Address 
		{
			get { return coord_ipv6; }
		}
		#endregion // property

		public HostApp ()
		{
			LoadStartupSettings();
			InitApp ();
		}

		private void LoadStartupSettings()
        {
			plc_port_name = AppOptions.GetAppOptions.plcPortName;
			ui_connection_name = AppOptions.GetAppOptions.uiConnectionName;
		}

		private void ResetPLC()
        {
			System.Device.Gpio.GpioController gpioController = new System.Device.Gpio.GpioController(System.Device.Gpio.PinNumberingScheme.Logical);
			gpioController.OpenPin(plc_gpip_pin, System.Device.Gpio.PinMode.Output);
			gpioController.Write(plc_gpip_pin, System.Device.Gpio.PinValue.Low);
			System.Threading.Thread.Sleep(250);
			gpioController.Write(plc_gpip_pin, System.Device.Gpio.PinValue.High);
			System.Threading.Thread.Sleep(250);
			DCLogger.Information("PLC Reset done.");
		}
		private void InitApp()
		{
			LoadSettings();
			//gpioController = new System.Device.Gpio.GpioController(System.Device.Gpio.PinNumberingScheme.Logical);

			//ResetPLC();

			sspc = new StatusServicePipeClient();
			sspc.PowerLineActive = false;
			sspc.StartClient();

			timer.Interval = 30000; // 30 secs
			timer.AutoReset = true;
			timer.Start();

			if (rd_is_subnet_gateway == 1)
			{
				subnet_gateway = new Communications.SubnetCommunications(rd_subnetdc_ipaddress, rd_subnetdc_port, rd_subnetdc_identifier);
				subnet_gateway.writeToDisplay += WriteMessage;
				subnet_gateway.receivedData += DataFromUI;
				subnet_gateway.Start();
			}
			else 
			{
				//RollingFileLogger.Information($"uiconnection {ui_connection_name} length {ui_connection_name.Length}");
				if (ui_connection_name.Length > 0)
                {
					if (ui_connection_name.Equals("websocket", StringComparison.CurrentCultureIgnoreCase))
					{
						throw new Exception("Not implementd Websocket option");
					}
					else if (ui_connection_name.Equals("socket", StringComparison.CurrentCultureIgnoreCase))
					{
						// Todo: implement this interface.
						// read ip address from text file
						throw new Exception("Not implementd socket option");
					}
					else
					{
						// xbee serial protocol
						xbee_messaging = new Communications.XbeeMessages("channelOne");
						Communications.SerialComm xbee_serial_port;
                        xbee_serial_port = new Communications.SerialComm(ui_connection_name["xbee:".Length..])
                        {
                            BaudRate = 9600,
                            DataBits = 8,
                            StopBitValue = System.IO.Ports.StopBits.One,
                            ParityValue = System.IO.Ports.Parity.None
                        };
                        if (xbee_serial_port.Open())
						{
							xbee_messaging.SerialConnection = xbee_serial_port;
							xbee_messaging.receivedMessage += DataFromUI;
							xbee_messaging.writeToDisplay += WriteMessage;
							xbee_messaging.SetAPIMode();
							while (!xbee_messaging.DataConnectionReadyAPIMode())
							{
								System.Threading.Thread.Sleep(2500);
							}

							if (!xbee_messaging.TransmitReady("www.plscentral.com", 8182))
							{
								DCLogger.Error("Failed to connect to domain: sts = " + xbee_messaging.TransmitReadyStatus.ToString("x"));
								throw new Exception("Unable to connect to domain.");
							}
						}
					}
				}
			}

			plc_init_timer.AutoReset = false;
			plc_init_timer.Elapsed += CheckRecvStatus;
			plc_init_timer.Interval = AppOptions.GetAppOptions.PLCCommTimeoutSecs * 1000;

			dCDataServiceComms = new DCPipeServer(rd_pipeconnectionname);
			((DCPipeServer)dCDataServiceComms).receivedMessage += ProcessMessageFromDataServices; 
			((DCPipeServer)dCDataServiceComms).StartServer();
		}

		private void ProcessMessageFromDataServices(string recv_str) 
		{
			bool sts;
            if (recv_str.StartsWith("get_cmds"))
            {
                // extract node name (format 'get_cmds:<node_name>')
                string tmpNodeName = recv_str.Replace("get_cmds:", "");
                if (tmpNodeName == recv_str)
                {
                    DCLogger.Error("no node name in get_cmds. recvd: " + recv_str);
                }
                else
                {
					tmpNodeName = tmpNodeName.Trim();
                    DCLogger.Information("recvd get_cmds for node: " + tmpNodeName);
					ServiceNode tmpsn = null;
					// debug only
					foreach (string key in node_name_dict.Keys)
					{
						DCLogger.Information(key);
					}
					lock (comm_ready_lock)
					{
						if (node_name_dict.ContainsKey(tmpNodeName))
						{
							tmpsn = node_name_dict[tmpNodeName];
						}
					}
					if (tmpsn != null)
                    {
						DCLogger.Information("Host APP: send set_cmds to ui");
						tmpsn.SendNodeCommandsToDataServices();
                    }
				}
			}
			else if ( (recv_str.StartsWith("[")) && (recv_str.Contains("Node") ) )
            {
                // received command from UI or ext entity
                // add it to message queue which will be handled in roundrobin DoRRWork
                try
                {
                    sts = UIMessagesStr.TryAdd(recv_str);
                    if (!sts)
                    {
                        DCLogger.Information($"Failed to add command from dataservices to queue {recv_str}");
                    }
                }
                catch (Exception ee)
                {
					DCLogger.Error(ee, "Exception trying to add command from dataservices to queue");
					DCLogger.Error($"{recv_str}");
				}            
			}
			else       // if it is a command need to send to appropriate node 
			{

			}

        }


		private bool WaitForResponse(ServiceNode sn, int timeOut_ms)
		{
			// Stopwatch stopwatch = new Stopwatch(); 
			// stopwatch.Start();
			// while (!myPredicate() && stopwatch.ElapsedMilliseconds < timeOut)
			// {
			// Thread.Sleep(50)
			// }  
			// return myPredicate()
			int time_elapsed_ms = 0;
			while ( (time_elapsed_ms < timeOut_ms) && (sn.RxResponseForTx == false) )
			{
				Thread.Sleep(250);
				time_elapsed_ms += 250;
			}
			if (sn.RxResponseForTx == true)
			{
				return true;
			}
			else 
			{
				return false;
			}
		}		

		public void ProcessDataFromDataServices(string json_cmds_list)
        {
			DCLogger.Information(json_cmds_list);
			CommandForNode[] node_cmds = System.Text.Json.JsonSerializer.Deserialize<CommandForNode[]>(json_cmds_list);
			foreach (var node_cmd in node_cmds)
            {
				// debug
				DCLogger.Information($"Processing {node_cmd.Node} : {node_cmd.Commands}");
				ServiceNode tmpsn = null;
				lock (comm_ready_lock)
				{
					if (node_name_dict.ContainsKey(node_cmd.Node))
					{
						tmpsn = node_name_dict[node_cmd.Node];
						//tmpsn.ProcessCommandFromDataService(node_cmd);
					}
				}
				if (tmpsn != null)
                {
					tmpsn.ProcessCommandFromDataService(node_cmd);
                }
			}
		}

		private void ProcessDataFromUI(byte[] obj_b)
		{
			string sn_id_ipv6_addr = BitConverter.ToString(obj_b, 0, 16);
			if (sn_id_ipv6_addr.ToLower().Equals("ff-02-00-00-00-00-00-00-00-00-00-00-00-00-00-01"))
			{
				int dc_name_serialnum_len = BitConverter.ToUInt16(obj_b, 16); // after the ipv6 address
				string tmpstr = System.Text.Encoding.UTF8.GetString(obj_b, 16 + 2, dc_name_serialnum_len);
				DCLogger.Information(string.Format("Received multicast cmd from UI: {0}", tmpstr));
				if ( (comm_ready_node_dict.Count > 0) && (host_messaging != null) )
				{
					byte[] multicast_addr = new byte[16];
					System.Array.Copy(obj_b, 0, multicast_addr, 0, 16);
					byte[] to_send_b = new byte[obj_b.Length - 16 - dc_name_serialnum_len];
					System.Array.Copy(obj_b, 16 + dc_name_serialnum_len, to_send_b, 0, obj_b.Length - 16 - dc_name_serialnum_len);
					DCLogger.Information("Sending multicast packet");
					byte[] combo_pkt = new byte[to_send_b.Length+2];
					ushort combo_pkt_len = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, (ushort)(to_send_b.Length + 2), false, to_send_b, (ushort)to_send_b.Length);
					host_messaging.SendDataToNode(combo_pkt, HostApp.CoordinatorIPv6Address );
				}
				return;
			}
			ServiceNode tmpsn = null;
			lock(comm_ready_lock)
			{
				if (comm_ready_node_dict.ContainsKey(sn_id_ipv6_addr))
				{
					tmpsn = comm_ready_node_dict[sn_id_ipv6_addr];
				}
			}
			if (tmpsn != null)
			{
				tmpsn.RxResponseForTx = false;
				tmpsn.ReceivedFromUserInterface(obj_b);
				//System.Threading.SpinWait.SpinUntil( () => tmpsn.RxResponseForTx, rd_rx_timeout_milliseconds);
				//if (WaitForResponse(tmpsn, rd_rx_timeout_milliseconds))
				if (tmpsn.RxResponseForTx == true)
				{
					tmpsn.RxResponseForTx = false;
				}
				else
				{
					//tmpsn.SendTimeoutResponseToUserInterface();
				}
			}
		}

		void DataFromUI(byte[] obj)
		{
			UIMessages.TryAdd(obj);
			bool sts;
			lock(lockObj)
			{
				if (second_timer_exec == false)
				{
					second_timer_exec = true;
                    try
                    {
                        sts = led_q.TryAdd(1.0);
                    }
                    catch( Exception e2)
                    {
                        DCLogger.Error(e2, "ledq try add");
                    }
				}
			}
		}

		public void StartApplication()
		{
			//OpenSerialInterface();
			StartHostCommunication();
			if (host_messaging.DeviceUDPConnection == null)
			{
				DCLogger.Error("UDP connection failed");
				return;
			}
			//InitNetwork();
			app_started = true;
			StartRoundRobinDCTask();
		}

		public void StopApplication()
		{
			app_started = false;
			StopHostCommunications(true);
			StopRoundRobinDCTask();
		}

        private int ConvertToInt(string to_int_str)
        {
            int tmpi;
            string tmpstr;
            bool isHex = false;
            if (to_int_str.ToLower().StartsWith("0x"))
            {
                tmpstr = to_int_str[2..];
                isHex = true;
            }
            else
            {
                tmpstr = to_int_str[..];
                isHex = false;
            }
            if (isHex)
            {
                tmpi = Convert.ToInt32(to_int_str, 16);
            }
            else
            {
                tmpi = Convert.ToInt32(to_int_str, 10);
            }
            return tmpi;
        }

		private void LoadSettings()
        {
			StartupSettings settings = AppOptions.GetAppOptions;
			rd_pan_id = (ushort)ConvertToInt(settings.PanId);
			string[] tmp_addr_strs = settings.XAddress.Split(new char[] { ',' });
			List<byte> tmp_b = new List<byte>();
			int tmpj;
			foreach (string str in tmp_addr_strs)
			{
				if (str.Trim().Length > 0)
				{
					tmpj = ConvertToInt(str.Trim());
					tmp_b.Add((byte)tmpj);
				}
			}
			if (tmp_b.Count == 8)
			{
				rd_xaddress = tmp_b.ToArray();
			}
			else
            {
				// default value
				rd_xaddress = new byte[default_xadd_base.Length];
				Array.Copy(default_xadd_base, rd_xaddress, default_xadd_base.Length);
			}

			string[] tmp_addr_strs2 = settings.PSK.Split(new char[] { ',' });
			List<byte> tmp_b2 = new List<byte>();
			int tmpj2;
			foreach (string str in tmp_addr_strs2)
			{
				if (str.Trim().Length > 0)
				{
					tmpj2 = ConvertToInt(str.Trim());
					tmp_b2.Add((byte)tmpj2);
				}
			}
			if (tmp_b2.Count == 16)
			{
				rd_psk = tmp_b2.ToArray();
			}
			else
            {
				// default value
				rd_psk = new byte[default_psk.Length];
				Array.Copy(default_psk, rd_psk, default_psk.Length);
			}

			rd_tone_maskname = settings.ToneMaskName;

			string[] tmp_addr_strs3 = settings.ToneMaskValues.Split(new char[] { ',' });
			List<byte> tmp_b3 = new List<byte>();
			int tmpj3;
			foreach (string str in tmp_addr_strs3)
			{
				if (str.Trim().Length > 0)
				{
					tmpj3 = ConvertToInt(str.Trim());
					tmp_b3.Add((byte)tmpj3);
				}
			}
			if (tmp_b3.Count == 14)
			{
				rd_tone_maskvalues = tmp_b3.ToArray();
			}

			rd_vendor_id = settings.VendorId;

			rd_product_id = settings.ProductId;

			rd_agc = settings.AGC;

			rd_gain_value = (ushort)settings.Gain;

			rd_transmit_level = settings.TransmitLevel;

			rd_rx_timeout_milliseconds = settings.RxTimeoutMilliSecs;

			rd_round_robin_task_sleep_secs = settings.DataReportingIntervalSecs;

			rd_plc_comm_timeout_secs = settings.PLCCommTimeoutSecs;
			rd_number_of_nodes = (ushort)settings.NumberOfNodes;
			rd_time_per_node_secs = (ushort)settings.TimerPerNodeSecs;
			rd_node_reset_time_secs = settings.NodeResetTimeSecs;
			rd_attach_high_priority = settings.AttachHighPriority;
			rd_confirmation_timeout_secs = settings.ConfirmationTimeoutSecs;
			rd_is_subnet_gateway = settings.IsSubnetGateway;
			rd_subnet_use_ethernet = settings.UseEthernet;
			rd_subnetdc_ipaddress = settings.SubnetDCIpAddress;
			rd_subnetdc_port = settings.SubnetDCPort;
			rd_subnetdc_identifier = settings.SubnetDCIdentifier;
			rd_sendtocloud = settings.SendToCloud;
			rd_pipeconnectionname = settings.PipeConnectionName;
		}

		private void LoadSettingsOld()
        {
			// load default values
			rd_pan_id = NETWORK_PAN_ID;
			rd_xaddress = new byte[default_xadd_base.Length];
			Array.Copy(default_xadd_base, rd_xaddress, default_xadd_base.Length);
			rd_psk = new byte[default_psk.Length];
			Array.Copy(default_psk, rd_psk, default_psk.Length);
			rd_vendor_id = 123;
			rd_product_id = "1987654321";
			rd_modulation_type = (byte)0;
			rd_tone_maskname = "Cenelec B";
			rd_tone_maskvalues = new byte[default_tonemask.Length];
			Array.Copy(default_tonemask, rd_tone_maskvalues, default_tonemask.Length);
			rd_agc = true;
			rd_gain_value = 7;
			rd_transmit_level = 32;
			rd_rx_timeout_milliseconds = 3000;
			rd_confirmation_timeout_secs = 180;
			rd_is_subnet_gateway = 0;
			rd_subnet_use_ethernet = 1; // default is ethernet
			rd_subnetdc_ipaddress = "192.168.1.100"; // default
			rd_subnetdc_port = 9001; // default
			rd_subnetdc_identifier="None"; // default already set
			rd_sendtocloud = false;
			rd_pipeconnectionname = "PLSDCConnection";


			if (System.IO.File.Exists(PARAMETERS_FILE))
            {
                var settingdata = System.IO.File.ReadAllLines(PARAMETERS_FILE);
                for (var i = 0; i < settingdata.Length; i++)
                {
                    var line = settingdata[i];
                    if (line.Trim().StartsWith("#"))
                    {
                        continue;
                    }
                    var sidx = line.IndexOf("=");
                    if (sidx >= 0)
                    {
                        string skey = line.Substring(0, sidx);
                        string svalue = line[(sidx + 1)..];
                        switch (skey.ToLower())
                        {
                            case "panid":
                                rd_pan_id = (ushort) ConvertToInt(svalue);
                                break;

                            case "xaddress":
                                string[] tmp_addr_strs = svalue.Split(new char[] { ',' });
                                List<byte> tmp_b = new List<byte>();
                                int tmpj;
                                foreach(string str in tmp_addr_strs)
                                {
                                    if (str.Trim().Length > 0)
                                    {
                                        tmpj = ConvertToInt(str.Trim());
                                        tmp_b.Add( (byte)tmpj );
                                    }
                                }
                                if (tmp_b.Count == 8)
                                {
                                    rd_xaddress = tmp_b.ToArray();
                                }
                                break;

                            case "psk":
                                string[] tmp_addr_strs2 = svalue.Split(new char[] { ',' });
                                List<byte> tmp_b2 = new List<byte>();
                                int tmpj2;
                                foreach (string str in tmp_addr_strs2)
                                {
                                    if (str.Trim().Length > 0)
                                    {
                                        tmpj2 = ConvertToInt(str.Trim());
                                        tmp_b2.Add((byte)tmpj2);
                                    }
                                }
                                if (tmp_b2.Count == 16)
                                {
                                    rd_psk = tmp_b2.ToArray();
                                }
                                break;

                            case "tonemaskvalues":
                                string[] tmp_addr_strs3 = svalue.Split(new char[] { ',' });
                                List<byte> tmp_b3 = new List<byte>();
                                int tmpj3;
                                foreach (string str in tmp_addr_strs3)
                                {
                                    if (str.Trim().Length > 0)
                                    {
                                        tmpj3 = ConvertToInt(str.Trim());
                                        tmp_b3.Add((byte)tmpj3);
                                    }
                                }
                                if (tmp_b3.Count == 14)
                                {
                                    rd_tone_maskvalues = tmp_b3.ToArray();
                                }
                                break;


                            case "vendorid":
                                rd_vendor_id = ConvertToInt(svalue);
                                break;

                            case "productid":
                                rd_product_id = svalue;
                                break;

                            case "modulationtype":
								// not used in microchip solution in central master
								DCLogger.Information(string.Format("modulation type read: {0}", svalue.ToLower()));
                                break;

                            case "tonemaskname":
                                rd_tone_maskname = svalue;
                                break;

                            case "agc":
                                if (svalue.ToLower().Equals("on"))
                                {
                                    rd_agc = true;
                                }
                                else
                                {
                                    rd_agc = false;
                                }
                                break;

                            case "gain":
                                rd_gain_value = (ushort)ConvertToInt(svalue.ToLower());
                                break;

							case "transmitlevel":
								rd_transmit_level = ConvertToInt(svalue.ToLower());
								break;
							case "roundrobinwaitsecs":
								rd_round_robin_task_sleep_secs = ConvertToInt(svalue.ToLower());
								break;
							case "plccommtimeoutsecs":
								rd_plc_comm_timeout_secs = ConvertToInt(svalue.ToLower());
								break;
							case "rxtimeoutmillisecs":
								rd_rx_timeout_milliseconds = Convert.ToInt16(svalue.ToLower());
								break;
							case "numberofnodes":
								rd_number_of_nodes = Convert.ToUInt16(svalue.ToLower());
								break;
							case "timepernodesecs":
								rd_time_per_node_secs = Convert.ToUInt16(svalue.ToLower());
								break;
							case "noderesettimesecs":
								rd_node_reset_time_secs = (double)Convert.ToInt32(svalue.ToLower());
								break;
							case "attachhighpriority":
								rd_attach_high_priority = Convert.ToBoolean(svalue.ToLower());
								break;
							case "confirmationtimeoutsecs":
								rd_confirmation_timeout_secs = Convert.ToInt32(svalue.ToLower());
								break;
							case "issubnetgateway":
								rd_is_subnet_gateway = Convert.ToInt32(svalue.ToLower());
								break;
							case "useethernet":
								rd_subnet_use_ethernet = Convert.ToInt32(svalue.ToLower());
								break;
							case "subnetdcipaddress":
								rd_subnetdc_ipaddress = svalue.Trim().ToLower();
								break;
							case "subnetdcipport":
								rd_subnetdc_port = Convert.ToInt32(svalue.ToLower());
								break;
							case "subnetdcidentifier":
								rd_subnetdc_identifier = svalue.Trim();
								break;
							case "sendtocloud":
								if (svalue.ToLower().Equals("true"))
								{
									rd_sendtocloud = true;
								}
								else 
								{
									rd_sendtocloud = false;
								}
								break;
							case "pipeconnectionname":
								rd_pipeconnectionname = svalue;
								break;
                        }
                    }
                }
            }
        }




		private void DoNodeProtocolInitActions(ServiceNode tmp_node)
		{
			attaching_sn = tmp_node;
			string sn_id_ipv6_addr = BitConverter.ToString(tmp_node.IPv6AddressBytes);
			string sn_id_ext_addr = BitConverter.ToString(tmp_node.ExtendedAddress);
			if (tmp_node.StateMachinePause == true)
			{
				tmp_node.StateMachinePause = false;
			}
			// while ( (!tmp_node.IsReadyState) && (!tmp_node.ExitMode) )
			// {
			// 	Thread.Sleep(500);
			// 	tmp_node.ExtStateMachine();
			// 	// debug
			// 	//WriteMessage(string.Format("is Ready state => {0}, Exit mode => {1}", tmp_node.IsReadyState, tmp_node.ExitMode));
			// }
			
			// The node will go into ExitMode if the command failed to reach the node or an error was reported by teh PLC layer
			tmp_node.ExitMode = false;
			while ( (!tmp_node.IsReadyState) && (!tmp_node.ExitMode) ) {
				sent_cmd_node = tmp_node;
				tmp_node.SendProtocolSetupRequestEventWithTimeout();
				if (!tmp_node.IsReadyState) {
					Thread.Sleep(1000);
				}
			}

			bool exit_mode = tmp_node.ExitMode;
			// debug
			WriteMessage(string.Format("node ipv6 addr {0} --> exit mode {1}", sn_id_ipv6_addr, tmp_node.ExitMode));
			if (exit_mode == false) // => successfully went to ready state
			{
				lock(comm_ready_lock)
				{
					if (comm_ready_node_dict.ContainsKey(sn_id_ipv6_addr))
					{
						// to debug
						DCLogger.Information($"To add {sn_id_ipv6_addr} with ext addr {sn_id_ext_addr}");
						foreach (string key in comm_ready_node_dict.Keys)
						{
							string ext_addr = BitConverter.ToString(comm_ready_node_dict[key].ExtendedAddress);
							DCLogger.Information($"exist node ipv6 : {key} with ext addr {ext_addr}");
						}
					}
					else
					{
                        comm_ready_node_dict.Add(sn_id_ipv6_addr, tmp_node);
                        comm_ready_node_dict_changed = true;
                        WriteMessage(string.Format("{0} comm ready node dict changed is true", sn_id_ipv6_addr));
                        string node_name = tmp_node.CommonName + "_" + tmp_node.SerialNumber;
                        ServiceNode toremov_node;
                        if (node_name_dict.ContainsKey(node_name))
                        {
                            toremov_node = node_name_dict[node_name];
                            node_name_dict.Remove(node_name);
                            toremov_node.Shutdown();
                            toremov_node = null;
                        }
                        node_name_dict.Add(node_name, tmp_node);
                        ext_addr_ipv6_map.Add(sn_id_ext_addr, sn_id_ipv6_addr);
                    }
                }
			}
			else
			{
				tmp_node.Shutdown();
				tmp_node = null;
			}
			attaching_sn = null;
		}

		/**
		 * TODO: replace this with a data transfer confirm on the UDP socket in that code
		 */
		//private void ReceivedDataTransferConfirm(CentralMaster.DataTransferConfirm dtc) 
		//{
		//	var tmp_sts_str = dtc.status.ToString("X");
		//	DCLogger.Information(string.Format($"Data transfer confirm status = 0x{tmp_sts_str}"));
		//	DCLogger.Information(string.Format($"bytes: {BitConverter.ToString(dtc.bytes)}"));
		//	if ( (dtc.status != 0) && (dtc.status != 0x5088) )
  //          {
		//		DCLogger.Information("Exiting centralmaster app.");
		//		System.Environment.Exit(-1);
		//	}
		//	//RollingFileLogger.Debug(dtc.ToString());
		//	string ipv6_addr_str;
		//	ipv6_addr_str = CentralMaster.Utils.GetIpv6AddressForHandle(dtc.nsdu_handle);
		//	if (ipv6_addr_str.Length > 0) 
		//	{

		//		if ( (sent_cmd_node != null) && ipv6_addr_str.Equals(sent_cmd_node.IPv6AddresString) ) 
		//		{
		//			sent_cmd_node.PutMessage(new object[] { dtc });
		//			return;
		//		}
		//		ServiceNode tmp_sn=null;;
		//		// found address
		//		lock(comm_ready_lock)
		//		{
		//			if (comm_ready_node_dict.ContainsKey(ipv6_addr_str)) 
		//			{
		//				tmp_sn = comm_ready_node_dict[ipv6_addr_str];
		//				tmp_sn.PutMessage(new object[] { dtc });
		//			}
		//		}	
		//		if (tmp_sn == null) 
		//		{
		//			if (attaching_sn != null) 
		//			{
		//				WriteMessage("DTC: Searching in attaching_sn");
		//				string comp_ipv6_str = BitConverter.ToString(attaching_sn.IPv6AddressBytes);
		//				if (comp_ipv6_str.Equals(ipv6_addr_str))
		//				{
		//					tmp_sn = attaching_sn;
		//					attaching_sn.PutMessage(new object[] { dtc });
		//				}
		//			}
		//		}
		//		// is it a multicast address 
		//		if (ipv6_addr_str.Equals("ff-02-00-00-00-00-00-00-00-00-00-00-00-00-00-01"))
		//		{
		//			DCLogger.Information("Received confirmation for multicast command");
		//			return;
		//		}
		//		// debug messages; can be removed after debugging
		//		if (tmp_sn == null)
		//		{
		//			DCLogger.Information(string.Format("Missing node {0} for handle: 0x{1:x2}", ipv6_addr_str, dtc.nsdu_handle));
		//		}
		//	}
		//	else 
		//	{
		//		// debug messages
		//		DCLogger.Information(string.Format("Missing ipv6 address for handle: 0x{0:x2}", dtc.nsdu_handle));
		//	}
		//}

		private void ReceivedMessageLEDIndication()
		{
			bool sts;
			lock(lockObj)
			{
				if (half_second_timer_exec == false)
				{
					half_second_timer_exec = true;
                    try
                    {
                        sts = led_q.TryAdd(0.5);
                    }
                    catch( Exception e2)
                    {
                        DCLogger.Error(e2, "recv msg: ledq try add");
                    }
				}
			}
			
		}

		private void ReceviedNodeUpdateList(List<byte[]> dest_node_ipv6, List<byte[]> dest_node_ext_addr)
		{
			/*
			 * if the two lists are null => the g3plc_node_list was deleted which will happen when g3coord restarts
			 */
			sspc.PowerLineActive = true;

			//byte[] dest_ipv6_addr = new byte[16];
			Communications.ITransmit to_send_interface;
			if (xbee_messaging != null)
			{
				to_send_interface = xbee_messaging;
			}
			else if (subnet_gateway != null)
			{
				to_send_interface = subnet_gateway;
			}
			else
			{
				to_send_interface = null;
			}
			if ( (dest_node_ext_addr == null) || (dest_node_ipv6 == null) )
			{
				// remove all elemens and return
				DCLogger.Verbose("Clearing all nodes available in CentralMaster.");
                lock (comm_ready_lock)
                {
                    foreach (string key in comm_ready_node_dict.Keys)
                    {
                        ServiceNode torem_sn = comm_ready_node_dict[key];
                        torem_sn.Shutdown();
                        comm_ready_node_dict.Remove(key);
                        torem_sn = null;
                    }
                    comm_ready_node_dict_changed = true;
                }
				lock (attach_sn_lock)
				{
					foreach (ServiceNode del_sn in attach_sn_list)
					{
						del_sn.Shutdown(); 
					}
					attach_sn_list.Clear();
				}
				if (attaching_sn != null)
				{
					attaching_sn.Shutdown();
					attaching_sn = null;
				}
                return;
			}
			DCLogger.Verbose("In Received Node Updated List");
			for (int ii=0; ii < dest_node_ipv6.Count; ii++)
            {
				string dest_node_ipv6_str = BitConverter.ToString(dest_node_ipv6[ii]);
				string dest_node_ext_addr_str = BitConverter.ToString(dest_node_ext_addr[ii]);
				//bool found_in_attach_sn_list = false;
				//bool found_in_comm_ready_dict = false;
				//bool found_in_attaching_sn = false;
				string srch_ipv6_addr = "";
				bool to_add_to_init_list = true;

				DCLogger.Information(string.Format("node update notification (could be repeated) with ipv6 {0} with ext addr {1}",
					dest_node_ipv6_str, dest_node_ext_addr_str));

				if (ext_addr_ipv6_map.ContainsKey(dest_node_ext_addr_str))
				{
					DCLogger.Information("ext addr found in ext addr ipv6 map");
					srch_ipv6_addr = ext_addr_ipv6_map[dest_node_ext_addr_str];
					if (srch_ipv6_addr.Equals(dest_node_ipv6_str))
					{
						to_add_to_init_list = false;
						continue;
					}

					// srch_ipv6_addr is old IPAddres and map has older ip address, dest_node_ipv6_addr_str is new address
					// both are differenct for same ext address if code reached here
					// check if it is there in comm_ready_dict
					DCLogger.Information("STARTED comm ready lock");
					lock (comm_ready_lock)
					{
						if (comm_ready_node_dict.ContainsKey(srch_ipv6_addr))
						{
							ServiceNode torem_sn = comm_ready_node_dict[srch_ipv6_addr];
							torem_sn.Shutdown();
							comm_ready_node_dict.Remove(srch_ipv6_addr);
							comm_ready_node_dict_changed = true;
							torem_sn = null;
						}
					}
                    DCLogger.Information("EXITED comm ready lock");
                }
                else
				{
                    DCLogger.Information("ext addr NOT found in ext addr ipv6 map");
                    int jj;
					bool check_attach_sn = true;
                    DCLogger.Information("STARTED attach sn lock");
                    lock (attach_sn_lock)
					{
						for (jj = attach_sn_list.Count - 1; jj >= 0; jj--)
						{
							if ( (dest_node_ext_addr_str.Equals(attach_sn_list[jj].IPv6AddresString))
								&&
								 (dest_node_ipv6_str.Equals(BitConverter.ToString(attach_sn_list[jj].ExtendedAddress)) )
								)
							{
								DCLogger.Information("found identical node in attach sn list. No action");
								to_add_to_init_list = false;
								continue;
							}
							else
							{
                                attach_sn_list.RemoveAt(jj);
                                check_attach_sn = false;
                            }
                        }
					}
					DCLogger.Information("REMOVED attach sn lock");

                    DCLogger.Information($"STARTED attaching sn check: to check = {check_attach_sn} and attaching sn exists = {attaching_sn != null}");
                    // check attaching_sn
                    if (check_attach_sn && (attaching_sn != null) )
					{
                        if ((dest_node_ext_addr_str.Equals(attaching_sn.IPv6AddresString))
                            &&
                             (dest_node_ipv6_str.Equals(BitConverter.ToString(attaching_sn.ExtendedAddress)))
                            )
                        {
                            DCLogger.Information("found identical node in attaching sn. No action");
							to_add_to_init_list = false;
							continue;
                        }
                        else
                        {

							attaching_sn.Shutdown();
                        }
                    }
                    DCLogger.Information("COMPLETED attaching sn check");
                }

				DCLogger.Information($"attach node is {to_add_to_init_list}");
                if (to_add_to_init_list)
				{
                    // add to attach_sn_list
                    lock (attach_sn_lock)
                    {
                        ServiceNode tmp_sn = new ServiceNode(dest_node_ipv6[ii], dest_node_ext_addr[ii], host_messaging, timer,
                            to_send_interface, rd_rx_timeout_milliseconds, rd_confirmation_timeout_secs,
                            rd_round_robin_task_sleep_secs, dCDataServiceComms);
                        if (rd_is_subnet_gateway == 1)
                        {
                            tmp_sn.IsPartOfSubnetDC = true;
                        }
                        tmp_sn.SendToCloud = rd_sendtocloud;
                        System.Threading.Interlocked.Exchange(ref attach_seq_delay, 1);
                        attach_sn_list.Add(tmp_sn);
                    }
                }
            }
		}

		private void ReceivedDataTransferIndication(CentralMaster.NodeAggregateMessage nam_dt_recv)
		{
			//bool sts;
			WriteMessage(string.Format("Received Data transfer Indication {0}", nam_dt_recv.ToString()));

			byte[] node_id_ipv6_b = nam_dt_recv.fromEP.Address.GetAddressBytes();
			string node_id_ipv6_str = BitConverter.ToString(node_id_ipv6_b);
			DCLogger.Information($"recvd from : {node_id_ipv6_str}");
			if ( (sent_cmd_node != null) && node_id_ipv6_str.Equals(sent_cmd_node.IPv6AddresString) ) 
			{
				sent_cmd_node.PutMessage(new object[] { nam_dt_recv });
				ReceivedMessageLEDIndication();
				return;
			}

			ServiceNode tmp_sn = null;
			lock(comm_ready_lock)
			{
				if (comm_ready_node_dict.ContainsKey(node_id_ipv6_str)) 
				{
					tmp_sn = comm_ready_node_dict[node_id_ipv6_str];
					// if (tmp_sn.UpdateViewThisSerialNumber == false)
					// {
					// 	string tmp_serial_num = tmp_sn.SerialNumber;
					// 	// search for nodes in this series that have the same serial number (left over from an earlier sequence)
					// 	// nodes when they reboot are sometimes incrementing the last index of the ipv6 address when they rejoin
					// 	DeleteNodesWithSerialNumber(tmp_serial_num, node_id_ipv6_str);
					// 	tmp_sn.UpdateViewThisSerialNumber = true;
					// }
					tmp_sn.PutMessage(new object[] { nam_dt_recv });
				}
			}	
			// TODO: Check if the code below is necessary
			if (tmp_sn == null)
			{
				// node was not found in comm_ready_node_dict
				// is node available in attached_node_dict
				lock(attach_sn_lock)
				{
					foreach (ServiceNode sn_item_list in attach_sn_list)
					{
						if (sn_item_list.IPv6AddressBytes.SequenceEqual(node_id_ipv6_b) )
						{
							tmp_sn = sn_item_list;
							sn_item_list.PutMessage(new object[] { nam_dt_recv });
							break;
						}
					}
				}
			}			
			if ( (tmp_sn == null) && (attaching_sn != null) )
			{
				WriteMessage("DTI: Searching in attaching_sn");
				//WriteMessage(string.Format("recv addr => {0}, attaching_sn addr -> {1}", BitConverter.ToString(node_id_ipv6_b) , BitConverter.ToString(attaching_sn.IPv6AddressBytes)));
				//WriteMessage(string.Format("seq equals ipv6 addr bytes =>{0}", node_id_ipv6_b.SequenceEqual(attaching_sn.IPv6AddressBytes).ToString()));
				if (node_id_ipv6_b.SequenceEqual(attaching_sn.IPv6AddressBytes))
				{
					tmp_sn = attaching_sn;
					attaching_sn.PutMessage(new object[] { nam_dt_recv });
				}
			}
			if (tmp_sn == null)
			{
				// node not in comm_ready_dict or in attached_node_dict
				//TODO:  if message is Reset Response ignore it, else send a reset message;
				byte[] recv_data = new byte[nam_dt_recv.data_recv.Length];
				Array.Copy(nam_dt_recv.data_recv, recv_data, recv_data.Length);
				ushort recv_data_msg_id = BitConverter.ToUInt16(recv_data, 2);
				if (recv_data_msg_id != (ushort)ProtocolIdentifiers.RESET_RESPONSE_ID)
				{
					WriteMessage(string.Format("BASE NODE: received DTI for {0}. Not found in node dictionary. Send Reset command", node_id_ipv6_str));

					// send reset command to node
					ushort reset_req_pkt_len = ProtocolMessagesG3PLC.ResetRequestPacketLen();
					byte[] reset_req_pkt = new byte[reset_req_pkt_len];
					ProtocolMessagesG3PLC.ResetRequestPacket(reset_req_pkt, reset_req_pkt_len);
					host_messaging.SendDataToNode(reset_req_pkt, nam_dt_recv.fromEP.Address.GetAddressBytes());
				}
			}
			ReceivedMessageLEDIndication();
		}

		private void ReceivedErrorNotification(string error_str)
		{
			DCLogger.Error(error_str);
		}


		private void StartRoundRobinDCTask()
		{
			// debug only
			WriteMessage("Starting Round Robin DC Task");
			Task t_rr = Task.Factory.StartNew(() =>  DoRRWork(RRDCTaskCTS.Token), RRDCTaskCTS.Token);
            t_rr.Wait();
		}

		private void StopRoundRobinDCTask()
		{
			RRDCTaskCTS.Cancel();
			Thread.Sleep(10*1000);
			//Thread.Sleep(rd_round_robin_task_sleep_secs*1000);
			if ( (rd_is_subnet_gateway == 1) && (subnet_gateway != null) )
			{
				subnet_gateway.Stop();
			}
		}

		private async void DoRRWork(CancellationToken ct)
		{
			bool ui_message_available;
			byte[] ui_msg_b;
			string ui_msg_str;
			List<ServiceNode> rr_sn = new List<ServiceNode>();
			//int time_elapsed_ms = 0;
			WriteMessage("Starting DoRRWork");
			int rr_index = 0;
			ServiceNode current_sn = null;
			int node_command_count = 0;
            int timeout_secs = 0;
            DateTime start_time = DateTime.Now;
			DateTime start_rr_loop = DateTime.Now;
			double timespent_rr_loop = 0;
			int timespent_rr_loop_int = 0;
			udp_conn.CheckJoinedNodes(); // if nodes are already joined filewatcher is only triggered on created, so calling this func will setup the already joined nodes
			while (!ct.IsCancellationRequested)
			{
				// are there any messages in the attach sequence
				// if yes, process them

				// are there any messages from Ui
				// if yes, act on them

				// if the above true are done, send data request for each of nodes in the comm_ready_dict

				// TODO: ????
				// if ui signal comes and attaching_sn (attaching service node) is not null, pause the attaching node
				// if (attaching_sn != null)
				// {
				// 	attaching_sn.StateMachinePause = true;
				// }
				// debug only
				//WriteMessage(string.Format("Start of while loop RR Task loop attached list.count => {0}", attach_sn_list.Count));
				while (attach_sn_list.Count > 0)
				{
					while ( (attach_seq_delay == 1) && 
							( (attach_sn_list.Count + comm_ready_node_dict.Count) < rd_number_of_nodes ) 
						) 
					{
						System.Threading.Interlocked.Exchange(ref attach_seq_delay, 0);
						Thread.Sleep(rd_time_per_node_secs * 1000);
					}
					WriteMessage(string.Format("In While loop RR Task loop attached list.count => {0}", attach_sn_list.Count));
					// wait till it goes to zero
					if (attaching_sn == null)
					{
						ServiceNode tmp_sn = attach_sn_list[0];
						attach_sn_list.RemoveAt(0);
						if ( tmp_sn.TimeSecondsSinceAttach < rd_node_reset_time_secs )
						{
							// debug only
							WriteMessage(string.Format("Starting attach seq for {0} at {1}", BitConverter.ToString(tmp_sn.IPv6AddressBytes), DateTime.Now.TimeOfDay.ToString()));
							await Task.Run( () => DoNodeProtocolInitActions(tmp_sn), CancellationToken.None );
						}
					}
					if (rd_attach_high_priority == false)
					{
						break;
					}
				}
				//WriteMessage(string.Format("comm ready node dict => {0} ", comm_ready_node_dict_changed));

				lock( comm_ready_lock )
				{
					if ( (comm_ready_node_dict_changed == true) || 
							( (rr_index >= comm_ready_node_dict.Count) && (timeout_secs <= 0) ) || 
							(rr_sn.Count == 0)
						)
					{
						// update list
						// ServiceNode[] tmp_sn_arr = new ServiceNode[comm_ready_node_dict.Count];
						// comm_ready_node_dict.Values.CopyTo(tmp_sn_arr, 0);
						rr_sn = comm_ready_node_dict.Values.OrderByDescending(x => x.TimeSecondsSinceDataRequest).ToList();
						// rr_sn.Clear();
						// foreach (ServiceNode tsn in tmp_sn_arr)
						// {
						// 	//if (tsn.DataCommandDone == false)
						// 	//{
						// 		rr_sn.Add(tsn);
						// 	//}
						// }
						rr_index = 0;
						start_rr_loop = DateTime.Now;
						timeout_secs = 0;
						if (comm_ready_node_dict_changed == true)
						{
							WriteMessage(string.Format("{0} comm ready node dict changed is true", comm_ready_node_dict.Count));
							comm_ready_node_dict_changed = false;
							if (rr_sn.Count == 0)
                            {
								sspc.PowerLineActive = false;
                            }
						}
					}
					//if (rr_sn != null)
					//{
					//	RollingFileLogger.Information($"rr_sn count = {rr_sn.Count}, rr_idx = {rr_index}");
					//}
					//else
					//{
					//	RollingFileLogger.Information("rr_sn is null");
					//}
					if (rr_sn.Count > 0)
					{
						if (rr_index < rr_sn.Count)
						{
							current_sn = rr_sn[rr_index];
							if (current_sn != null)
                            {
								DCLogger.Debug($"current sn => {current_sn.SerialNumber}");
							}
							else
                            {
								DCLogger.Debug("current sn null in rr_idx < rr_sn count loop");
                            }
							rr_index++;
							if (rr_index >= rr_sn.Count)
							{
								timespent_rr_loop = (DateTime.Now - start_rr_loop).TotalSeconds;
								timespent_rr_loop_int = (int)System.Math.Round(timespent_rr_loop);
								if (timespent_rr_loop_int >= rd_round_robin_task_sleep_secs)
                                {
									timeout_secs = 0;
                                }
								else
                                {
									timeout_secs = rd_round_robin_task_sleep_secs - timespent_rr_loop_int;
								}
								start_time = DateTime.Now;
							}
						}
						else
						{
							if (timeout_secs > 0)
							{
								if ( (DateTime.Now - start_time).TotalSeconds > ( timeout_secs ) )
								{
									timeout_secs = 0;
								}
							}
							current_sn = null;
						}
					}
					else
					{
						current_sn = null;
					}
				}

				if (current_sn != null)
				{
					DCLogger.Debug("sending get number of commands");
					node_command_count = current_sn.NumberOfCommands();
					DCLogger.Information($"number of commands = {node_command_count}");
					for (int ii=0; ii < node_command_count; ii++)
					{
						current_sn.RxResponseForTx = false;
						sent_cmd_node = current_sn;
						//current_sn.SendDataRequestEventWithTimeout();
						current_sn.SendCommandWithTimeout(ii);
						if (current_sn.ExitMode == true)
						{
							DCLogger.Information($"Exit mode: {current_sn.ExitMode}");
							string ipv6_str = BitConverter.ToString(current_sn.IPv6AddressBytes);
							comm_ready_node_dict.Remove(ipv6_str);
							comm_ready_node_dict_changed = true;
						}
						System.Threading.Thread.Sleep(500);
					}					
				}
                //else
                //{
                //	//debug 
                //	RollingFileLogger.Information("current sn is null");
                //}


                //ProcessDataFromUI();
                //ProcessCommandsFromFiles();
                //await Task.Delay(TimeSpan.FromSeconds(rd_round_robin_task_sleep_secs), ct);
                if ( (attach_sn_list.Count > 0) || (rr_index < rr_sn.Count) )
				{
                    //ui_message_available = UIMessages.TryTake(out ui_msg_b, 10, ct);
                    ui_message_available = UIMessagesStr.TryTake(out ui_msg_str, 10, ct);
				}
				else 
				{
                    ui_message_available = UIMessagesStr.TryTake(out ui_msg_str, 1 * 1000, ct);
					//ui_message_available = UIMessages.TryTake(out ui_msg_b, 1*1000, ct);
					////ui_message_available = UIMessages.TryTake(out ui_msg_b, rd_round_robin_task_sleep_secs*1000, ct);
				}
				if (ui_message_available)
				{
					ProcessDataFromDataServices(ui_msg_str);
					//ProcessDataFromUI(ui_msg_b);
					// are there any more ui messages 
					while (ui_message_available)
					{
						ui_message_available = UIMessagesStr.TryTake(out ui_msg_str);
						//ui_message_available = UIMessages.TryTake(out ui_msg_b);
						if (ui_message_available)
						{
							ProcessDataFromDataServices(ui_msg_str);
							//ProcessDataFromUI(ui_msg_b);
						}
					}
				}
			}
		}

		private void WaitForResponseWithTimeout(int timeout_secs)
        {
			plc_init_timeout_event.Reset();
			DCLogger.Debug("reset wait for resp timeout");
			bool sts = plc_init_timeout_event.WaitOne(timeout_secs * 1000);
			DCLogger.Debug($" wait for response with timeout, sts = {sts}");
			if (!sts)
            {
				DCLogger.Error($"PLC Init: Timed out waiting for response: {timeout_secs} secs");
				System.Environment.Exit(2);
            }
		}

		private void CheckRecvStatus(Object source, System.Timers.ElapsedEventArgs e)
		{
			if (!plc_init_resp_recvd)
			{
				DCLogger.Error($"Failed to recv response from plc layer (during init) in {plc_init_timer.Interval}");
				DCLogger.Error("Exiting CentralMaster app");
				System.Environment.Exit(1);
			}
		}

		#region serial_interface_funcs

		#endregion //serial_interface_funcs


		#region protocol_funcs
		private void StartHostCommunication()
		{
			udp_conn.nodeUpdated += ReceviedNodeUpdateList;
			host_messaging.DeviceUDPConnection = udp_conn;
			// to setup hostmessages to send and rececive
			//host_messaging.writeToDisplay += WriteMessage;
			//host_messaging.receivedNodeAttach += ReceviedAttachIndication;
			//host_messaging.receivedNodeDetach += ReceivedDetachIndication;
			host_messaging.receivedDataTransferIndication += ReceivedDataTransferIndication;
			//host_messaging.receivedDataTransferConfirm += ReceivedDataTransferConfirm;
			host_messaging.receivedErrorNotification += ReceivedErrorNotification;
			//host_messaging.receivedMessage += ReceivedMessage;

			//byte[] to_send_raw =
			//{
			//        0x03, 0x80, 0x0A,  0x00,
			//        0x4D, 0x4F, 0xBB,  0xDF,
			//        0x6E, 0x00, 0x00, 0x00, 0x00, 0x00
			//    };
			//host_messaging.SendCustomMessage(to_send_raw);
			//Thread.Sleep(500);

		}

		private void StopHostCommunications(bool do_send_detach_req)
		{
			udp_conn.nodeUpdated -= ReceviedNodeUpdateList;
			host_messaging.DeviceUDPConnection = null;
			//host_messaging.writeToDisplay -= WriteMessage;
			//host_messaging.receivedNodeAttach -= ReceviedAttachIndication;
			//host_messaging.receivedNodeDetach -= ReceivedDetachIndication;
			host_messaging.receivedDataTransferIndication -= ReceivedDataTransferIndication;
			//host_messaging.receivedDataTransferConfirm -= ReceivedDataTransferConfirm;
			host_messaging.receivedErrorNotification -= ReceivedErrorNotification;
			//host_messaging.receivedMessage -= ReceivedMessage;
		}
		#endregion // protocol_funcs

		#region threadsafe_set
		private void WriteMessage(string message)
		{
			DCLogger.Information(message);
		}

        #endregion //threadsafe_set

        #region utils
        // Function to read JSON data from a file if no other process is writing to it
        private string ReadJsonFromFileIfNotInUse(string filename)
        {
            try
            {
                // Try to open the file with exclusive access
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // If we can open the file, it means it's not in use
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        string jsonString = reader.ReadToEnd();
                        var jsonObject = JsonSerializer.Deserialize<CommandForNode>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        // Output the resulting object
                        DCLogger.Information("Read command: " + jsonString);
						return jsonString;
                    }
                }
            }
            catch (IOException ex)
            {
                // The file might be in use or locked
                DCLogger.Error($"Error reading file {filename}: {ex.Message}");
				return null;
            }
        }


        public void ProcessCommandsFromFiles()
        {
			
            //Function to check if a directory exists and list all *.json files in it
			// Check if the directory exists

            if (Directory.Exists(cmd_file_path))
            {
                try
                {
                    // Get all *.json files in the directory
                    string[] jsonFiles = Directory.GetFiles(cmd_file_path, "*.json");

                    // Check if there are any .json files
                    if (jsonFiles.Length > 0)
                    {
                        foreach (string file in jsonFiles)
                        {
                            DCLogger.Information($"Processing cmd file {Path.GetFileName(file)}");
                            string mycmd = ReadJsonFromFileIfNotInUse(file);
							if (mycmd != null)
							{
								string tmpstr = "[" + mycmd + "]";
                                try
                                {
                                    bool sts = UIMessagesStr.TryAdd(tmpstr);
                                    if (!sts)
                                    {
                                        DCLogger.Information($"Failed to add command from dataservices to queue {tmpstr}");
                                    }
                                }
                                catch (Exception ee)
                                {
                                    DCLogger.Error(ee, "Exception trying to add command from dataservices to queue");
                                    DCLogger.Error($"{tmpstr}");
                                }
                            }
                            else
							{
								DCLogger.Error($"Failed to process command {Path.GetFileName(file)}");
							}
                        }
                    }
                    else
                    {
                        Console.WriteLine("No .json files found in the directory.");
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions such as access issues
                    Console.WriteLine("Error listing files: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Directory does not exist.");
            }
        }


        #endregion //utils

    }
}

