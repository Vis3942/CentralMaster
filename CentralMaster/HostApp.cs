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

namespace CentralMaster
{
	public class HostApp
	{
		#region vars
		private string plc_port_name = "";
        private string ui_connection_name = "";
		private bool app_started = false;
		private Communications.SerialComm serial_port = null;
        readonly CentralMaster.HostMessages host_messaging = new CentralMaster.HostMessages();
		private int ControllerState = (int)RootNodeStates.UNINITIALIZED;
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

		private  int rd_rx_timeout_milliseconds = 3000;
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
			CentralMaster.Utils.update_custom_mask_value(rd_tone_maskvalues);
			gpioController = new System.Device.Gpio.GpioController(System.Device.Gpio.PinNumberingScheme.Logical);

			ResetPLC();

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
			dCDataServiceComms = new DCPipeServer(rd_pipeconnectionname);
			((DCPipeServer)dCDataServiceComms).StartServer();
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
			CommandForNode[] node_cmds = System.Text.Json.JsonSerializer.Deserialize<CommandForNode[]>(json_cmds_list);
			foreach (var node_cmd in node_cmds)
            {
				ServiceNode tmpsn = null;
				lock (comm_ready_lock)
				{
					if (node_name_dict.ContainsKey(node_cmd.Node))
					{
						tmpsn = comm_ready_node_dict[node_cmd.Node];
						tmpsn.ProcessCommandFromDataService(node_cmd);
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
					host_messaging.SendDataToNode(combo_pkt, HostApp.CoordinatorIPv6Address, multicast_addr, true);
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
			OpenSerialInterface();
			StartHostCommunication();
			if (host_messaging.SerialConnection == null)
			{
				return;
			}
			//InitNetwork();
			app_started = true;
			StartNode();
			StartRoundRobinDCTask();
		}

		public void StopApplication()
		{
			app_started = false;
			StopHostCommunications(true);
			CloseSerialInterface();
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
			// load default values
			rd_pan_id = NETWORK_PAN_ID;
			rd_xaddress = new byte[default_xadd_base.Length];
			Array.Copy(default_xadd_base, rd_xaddress, default_xadd_base.Length);
			rd_psk = new byte[default_psk.Length];
			Array.Copy(default_psk, rd_psk, default_psk.Length);
			rd_vendor_id = 123;
			rd_product_id = "1987654321";
			rd_modulation_type = (byte)ModulationTypes.ROBO;
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


			if (File.Exists(PARAMETERS_FILE))
            {
                var settingdata = File.ReadAllLines(PARAMETERS_FILE);
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
                                switch(svalue.ToLower())
                                {
                                    case "robo":
                                        rd_modulation_type = (byte)ModulationTypes.ROBO;
                                        break;
                                    case "qpsk":
                                        rd_modulation_type = (byte)ModulationTypes.QPSK;
                                        break;
                                    case "bpsk":
                                        rd_modulation_type = (byte)ModulationTypes.BPSK;
                                        break;
                                    case "eightpsk":
                                        rd_modulation_type = (byte)ModulationTypes.EightPSK;
                                        break;
                                    case "superrobo":
                                        rd_modulation_type = (byte)ModulationTypes.SUPER_ROBO;
                                        break;
                                    default:
                                        rd_modulation_type = (byte)ModulationTypes.ROBO;
                                        break;
                                }
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



		private void ReceviedAttachIndication(CentralMaster.AttachIndication ai)
		{
			WriteMessage(string.Format("Received Attach Indication {0}", ai.ToString()));
			sspc.PowerLineActive = true;
			//if (recv_ai == null)
			//{
			//	recv_ai = ai;
			//}
			byte[] dest_ipv6_addr = new byte[16];
			Array.Copy(ai.ipv6_addr_prefix, 0, dest_ipv6_addr, 0, 8);
			Array.Copy(ai.ipv6_addr_suffix, 0, dest_ipv6_addr, 8, 8);
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
			ServiceNode tmp_sn = new ServiceNode(dest_ipv6_addr, ai.extended_addr, host_messaging, timer,
				to_send_interface, rd_rx_timeout_milliseconds, rd_confirmation_timeout_secs,
				rd_round_robin_task_sleep_secs, dCDataServiceComms);
			string sn_id_ipv6_addr = BitConverter.ToString(dest_ipv6_addr);
			if (rd_is_subnet_gateway == 1)
			{
				tmp_sn.IsPartOfSubnetDC = true;
			}
			tmp_sn.SendToCloud = rd_sendtocloud;

			// TBD ???
			//NodesViewCleanUp(); // delete nodes that are in shutdown if any

			lock(comm_ready_lock)
			{
				List<string> to_rem_addr = new List<string>();
				foreach ( KeyValuePair<string, ServiceNode> tmp_n in comm_ready_node_dict) 
				{
					if (ai.extended_addr.SequenceEqual(tmp_n.Value.ExtendedAddress))
					{
						to_rem_addr.Add(tmp_n.Key);
					}
				}
				if (to_rem_addr.Count > 0)
				{
					foreach (string tmp_addr in to_rem_addr)
					{
						ServiceNode tnn = comm_ready_node_dict[tmp_addr];
						comm_ready_node_dict.Remove(tmp_addr);
						string node_name = tnn.CommonName + "_" + tnn.SerialNumber;
						tnn.Shutdown();
						tnn = null;
						if (node_name_dict.Keys.Contains(node_name))
                        {
							DCLogger.Information($"Removing {node_name} from internal list");
							node_name_dict.Remove(node_name);
                        }
						comm_ready_node_dict_changed = true;

					}
				}
			}

			lock(attach_sn_lock)
			{
				List<int> to_rem_index = new List<int>();
				// debug 
				WriteMessage(string.Format("attach_sn_list size => {0}", attach_sn_list.Count));
				for (int ii=0; ii<attach_sn_list.Count; ii++)
				{
					if (ai.extended_addr.SequenceEqual(attach_sn_list[ii].ExtendedAddress))
					{
						to_rem_index.Add(ii);
					}
					else if (attach_sn_list[ii].IPv6AddressBytes.SequenceEqual(dest_ipv6_addr) ) 
					{
						to_rem_index.Add(ii);
					}
				}
				if (to_rem_index.Count > 0)
				{
					to_rem_index.Reverse();
					for (int jj=0; jj<to_rem_index.Count; jj++)
					{
						attach_sn_list.RemoveAt(to_rem_index[jj]);
					}
				}
				System.Threading.Interlocked.Exchange(ref attach_seq_delay, 1);
				attach_sn_list.Add(tmp_sn);
			}

			if (attaching_sn != null)	
			{
				if (attaching_sn.ExtendedAddress.SequenceEqual(tmp_sn.ExtendedAddress) )
				{
					// debug
					WriteMessage("ext addr: attach sn is equal to working sn");
					attaching_sn.ExitMode = true;
				}
				else if (attaching_sn.IPv6AddressBytes.SequenceEqual(tmp_sn.IPv6AddressBytes))
				{
					WriteMessage("ipv6 addr: attach sn is equal to working sn");
					attaching_sn.ExitMode = true;
				}
			}


			//if (attaching_sn == null) {
			//	attaching_sn = tmp_sn;
			//	Task.Run(() => DoNodeProtocolInitActions(tmp_sn) );
			//}
		}

		private void DoNodeProtocolInitActions(ServiceNode tmp_node)
		{
			attaching_sn = tmp_node;
			string sn_id_ipv6_addr = BitConverter.ToString(tmp_node.IPv6AddressBytes);
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
				}
			}
			else
			{
				tmp_node.Shutdown();
				tmp_node = null;
			}
			attaching_sn = null;
		}

		private void ReceivedDetachIndication(CentralMaster.DetachIndication di)
		{
			WriteMessage(string.Format("Received Detach Indication {0}", di.ToString()));
			string del_ext_addr = BitConverter.ToString(di.extended_addr);
			// //var to_delete = node_ext_addr.Where(x => x.Key.Equals(del_ext_addr))
			// //	.Select(x => new { Key = x.Key, Value = x.Value })
			// //	.FirstOrDefault();
			// //
			// //if (to_delete != null)
			// //{
			// //	node_ext_addr.Remove(to_delete.Key);
			// //	node_dict.Remove(to_delete.Value);
			// //}
			// if (node_ext_addr.ContainsKey (del_ext_addr)) {
			// 	lock(comm_ready_lock)
			// 	{
			// 		comm_ready_node_dict.Remove(node_ext_addr[del_ext_addr]);
			// 	}
			// 	//DeleteFromNodeExtAddrByKey(del_ext_addr);
			// 	node_ext_addr.Remove(del_ext_addr);
			// }
			string to_remove_node_addr = null;
			if (to_remove_node_addr != null)
			{
				lock(comm_ready_lock)
				{
					comm_ready_node_dict.Remove(to_remove_node_addr);
					comm_ready_node_dict_changed = true;
				}
			}

		}

		private void ReceivedDataTransferConfirm(CentralMaster.DataTransferConfirm dtc) 
		{
			//RollingFileLogger.Debug(dtc.ToString());
			string ipv6_addr_str;
			ipv6_addr_str = CentralMaster.Utils.GetIpv6AddressForHandle(dtc.nsdu_handle);
			if (ipv6_addr_str.Length > 0) 
			{

				if ( (sent_cmd_node != null) && ipv6_addr_str.Equals(sent_cmd_node.IPv6AddresString) ) 
				{
					sent_cmd_node.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dtc });
					return;
				}
				ServiceNode tmp_sn=null;;
				// found address
				lock(comm_ready_lock)
				{
					if (comm_ready_node_dict.ContainsKey(ipv6_addr_str)) 
					{
						tmp_sn = comm_ready_node_dict[ipv6_addr_str];
						tmp_sn.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dtc });
					}
				}	
				if (tmp_sn == null) 
				{
					if (attaching_sn != null) 
					{
						WriteMessage("DTC: Searching in attaching_sn");
						string comp_ipv6_str = BitConverter.ToString(attaching_sn.IPv6AddressBytes);
						if (comp_ipv6_str.Equals(ipv6_addr_str))
						{
							tmp_sn = attaching_sn;
							attaching_sn.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dtc });
						}
					}
				}
				// is it a multicast address 
				if (ipv6_addr_str.Equals("ff-02-00-00-00-00-00-00-00-00-00-00-00-00-00-01"))
				{
					DCLogger.Information("Received confirmation for multicast command");
					return;
				}
				// debug messages; can be removed after debugging
				if (tmp_sn == null)
				{
					DCLogger.Information(string.Format("Missing node {0} for handle: 0x{1:x2}", ipv6_addr_str, dtc.nsdu_handle));
				}
			}
			else 
			{
				// debug messages
				DCLogger.Information(string.Format("Missing ipv6 address for handle: 0x{0:x2}", dtc.nsdu_handle));
			}
		}

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

		private void ReceivedDataTransferIndication(CentralMaster.DataTransferIndication dti)
		{
			//bool sts;
			WriteMessage(string.Format("Received Data transfer Indication {0}", dti.ToString()));

			byte[] node_id_ipv6_b = dti.ipv6Header.SourceAddress.GetAddressBytes();
			string node_id_ipv6_str = BitConverter.ToString(node_id_ipv6_b);
			DCLogger.Information($"recvd from : {node_id_ipv6_str}");
			if ( (sent_cmd_node != null) && node_id_ipv6_str.Equals(sent_cmd_node.IPv6AddresString) ) 
			{
				sent_cmd_node.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dti });
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
					tmp_sn.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dti });
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
							sn_item_list.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dti });
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
					attaching_sn.PutMessage(new object[] { CommandTypes.DATA_TRANSFER, dti });
				}
			}
			if (tmp_sn == null)
			{
				// node not in comm_ready_dict or in attached_node_dict
				//TODO:  if message is Reset Response ignore it, else send a reset message;
				if (ControllerState != (int)RootNodeStates.STEP12_M_NETWORK_READY)
				{
					return;
				}
				byte[] recv_data = new byte[dti.data_recv.Length];
				Array.Copy(dti.data_recv, recv_data, recv_data.Length);
				ushort recv_data_msg_id = BitConverter.ToUInt16(recv_data, 2);
				if (recv_data_msg_id != (ushort)ProtocolIdentifiers.RESET_RESPONSE_ID)
				{
					WriteMessage(string.Format("BASE NODE: received DTI for {0}. Not found in node dictionary. Send Reset command", node_id_ipv6_str));

					// send reset command to node
					ushort reset_req_pkt_len = ProtocolMessagesG3PLC.ResetRequestPacketLen();
					byte[] reset_req_pkt = new byte[reset_req_pkt_len];
					ProtocolMessagesG3PLC.ResetRequestPacket(reset_req_pkt, reset_req_pkt_len);
					host_messaging.SendDataToNode(reset_req_pkt, HostApp.CoordinatorIPv6Address, dti.ipv6Header.SourceAddress.GetAddressBytes(), true);
				}
			}
			ReceivedMessageLEDIndication();
		}

		private void ReceivedErrorNotification(ushort serial_error_code)
		{
			if (serial_error_code == (ushort)Communications.SerialErrorCodes.IOException)
			{
				if (app_started)
				{
					StopHostCommunications(false);
					CloseSerialInterface();
					Task.Factory.StartNew(() =>
						{
							while (host_messaging.SerialConnection == null)
							{
								try
								{
									OpenSerialInterface();
									StartHostCommunication();
									if (host_messaging.SerialConnection != null)
									{
										StartNode();
									}
								}
								catch (Exception e)
								{
									WriteMessage(e.Message);
									// restart
								}
								System.Threading.Thread.Sleep(30000);
							}
						}, TaskCreationOptions.LongRunning);
				}
			}
		}

		private void ReceivedMessage(byte command_type, ushort command_sub_type, CentralMaster.MessageHeader message)
		{
			ProcessMessages(command_type, command_sub_type, message);
		}

		private void StartNode()
		{
			ControllerState = (int)RootNodeStates.UNINITIALIZED;
			ProcessMessages((byte)0, 0, null);
		}

		private void StartRoundRobinDCTask()
		{
			// debug only
			WriteMessage("Starting Round Robin DC Task");
			Task.Factory.StartNew(() =>  DoRRWork(RRDCTaskCTS.Token), RRDCTaskCTS.Token);
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
			List<ServiceNode> rr_sn = new List<ServiceNode>();
			//int time_elapsed_ms = 0;
			WriteMessage("Starting DoRRWork");
			int rr_index = 0;
			ServiceNode current_sn = null;
			int node_command_count = 0;
            int timeout_secs = 0;
            DateTime start_time = DateTime.Now;
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
								timeout_secs = rd_round_robin_task_sleep_secs;
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
				//await Task.Delay(TimeSpan.FromSeconds(rd_round_robin_task_sleep_secs), ct);
				if ( (attach_sn_list.Count > 0) || (rr_index < rr_sn.Count) )
				{
					ui_message_available = UIMessages.TryTake(out ui_msg_b, 10, ct);
				}
				else 
				{
					ui_message_available = UIMessages.TryTake(out ui_msg_b, 1*1000, ct);
					//ui_message_available = UIMessages.TryTake(out ui_msg_b, rd_round_robin_task_sleep_secs*1000, ct);
				}
				if (ui_message_available)
				{
					ProcessDataFromUI(ui_msg_b);
					// are there any more ui messages 
					while (ui_message_available)
					{
						ui_message_available = UIMessages.TryTake(out ui_msg_b);
						if (ui_message_available)
						{
							ProcessDataFromUI(ui_msg_b);
						}
					}
				}
			}
		}

		private void ProcessMessages(byte command_type, ushort command_sub_type, CentralMaster.MessageHeader message)
		{
			bool loc_send_cmd;
			if (message != null)
            {
				DCLogger.Information($"Received command type: {command_type}, sub type {command_sub_type}");
				if (message.bytes != null)
                {
					DCLogger.Information(BitConverter.ToString(message.bytes));
				}
			}
			// process alarm confirmation and alarm messages independent of state machine
			if (command_type == (byte) CommandTypes.ALARM)
			{
				if (command_sub_type == (ushort) AlarmTypes.G3_NETWORK_STATUS)
				{
					WriteMessage(((CentralMaster.AlarmSubNetworkStatus)message).ToString());
				}
				else if (command_sub_type == (ushort) AlarmTypes.GENERAL_ERROR)
				{
					WriteMessage(((CentralMaster.AlarmSubGeneralError)message).ToString());
				}
				else
				{
					WriteMessage(((CentralMaster.Alarm)message).ToString());
				}
			} else if (command_type == (byte) CommandTypes.SETUP_ALARM)
			{
				WriteMessage(((CentralMaster.SetAlarmResponse)message).ToString());
			}


			// state machine implementation
			if (ControllerState == (int)RootNodeStates.UNINITIALIZED)
			{
				// read neighbour table
				host_messaging.SendGetMACPIB((uint)MACPIBIds.NEIGHBOUR_TABLE); // 0x006b //0x0050

				//host_messaging.SendGetSystemInfoRequest();
				//
				ControllerState = (int)RootNodeStates.STEP00_INIT;
			}
			else if (ControllerState == (int)RootNodeStates.STEP00_INIT)
			{
				if (command_type == (byte) CommandTypes.GET_MAC_PIB)
				{
					CentralMaster.GetMACPIBResponse gmpr = (CentralMaster.GetMACPIBResponse)message;
					WriteMessage(gmpr.ToString());
					if (gmpr.attribute_id == (uint)MACPIBIds.NEIGHBOUR_TABLE)
					{
						if (gmpr.attribute_len > 0)
						{
							// there are neighbour nodes attached
							host_messaging.SendGetSystemInfoRequest();
							ControllerState = (int)RootNodeStates.STEP08_SET_TX_PARAMS; //(int)RootNodeStates.STEP07_SET_VENDOR_IDS;
						}
						else
						{
							// no neighbour nodes attached, can go the default route
							host_messaging.SendLoadSystemConfigPorts((byte)1, (byte)1);
							ControllerState = (int)RootNodeStates.STEP02_CONFIG_PORTS;
						}
					}
				}
				/*
                if (command_type == (byte)CommandTypes.GET_SYSTEM_INFO)
                {
                    WriteMessage(((G3.GetSystemInfoResponse)message).ToString());
                    G3.GetSystemInfoResponse sir = (G3.GetSystemInfoResponse)message;
                    //if (!sir.long_address.SequenceEqual(xadd_base))
                    //{
                    host_messaging.SendLoadSystemConfigPorts((byte)1, (byte)1);
                    ControllerState = (int)RootNodeStates.STEP02_CONFIG_PORTS;
                    //}
                    //else
                    //{
                    //    host_messaging.SendShutdown((ushort)ShutdownTypes.SoftReset);
                    //    ControllerState = (int)RootNodeStates.STEP06_GET_SYSTEM_INFO_AFTER_SD;
                    //}
                }
                */
			}
			else if (ControllerState == (int)RootNodeStates.STEP02_CONFIG_PORTS)
			{
				if (command_type == (byte)CommandTypes.LOAD_SYSTEM_CONFIG)
				{
					WriteMessage(((CentralMaster.LoadSystemConfigResponse)message).ToString());
					host_messaging.SendLoadSystemConfigSystem((int)DeviceModes.Normal_Mode);
					ControllerState = (int)RootNodeStates.STEP04_LOAD_G3_SYSTEM_CONFIG; //(int)RootNodeStates.STEP03_LOAD_SYSTEM_CONFIG;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP03_LOAD_SYSTEM_CONFIG)
			{
				if (command_type == (byte)CommandTypes.LOAD_SYSTEM_CONFIG)
				{
					WriteMessage(((CentralMaster.LoadSystemConfigResponse)message).ToString());
					host_messaging.SendLoadSystemConfigSystem((int)DeviceModes.Normal_Mode);
					ControllerState = (int)RootNodeStates.STEP04_LOAD_G3_SYSTEM_CONFIG;
				}
			}
			else if (ControllerState == (byte)RootNodeStates.STEP04_LOAD_G3_SYSTEM_CONFIG)
			{
				if (command_type == (byte)CommandTypes.LOAD_SYSTEM_CONFIG)
				{
					WriteMessage(((CentralMaster.LoadSystemConfigResponse)message).ToString());
					host_messaging.SendLoadSystemConfigG3(false, false, rd_xaddress);
					ControllerState = (int)RootNodeStates.STEP05_SHUTDOWN; // (int)RootNodeStates.STEP04A_SET_PSK;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP04A_SET_MAC_PIB_PSK)
			{
				if (command_type == (byte)CommandTypes.LOAD_SYSTEM_CONFIG)
				{
					WriteMessage(((CentralMaster.LoadSystemConfigResponse)message).ToString());
					host_messaging.SendPSK(rd_psk);
					ControllerState = (int)RootNodeStates.STEP05_SHUTDOWN;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP05_SHUTDOWN)
			{
				loc_send_cmd = false;
				if (command_type == (byte)CommandTypes.LOAD_SYSTEM_CONFIG)
				{
					WriteMessage(((CentralMaster.LoadSystemConfigResponse)message).ToString());
					loc_send_cmd = true;
				} else if (command_type == (byte)CommandTypes.SET_MAC_PIB)
				{
					WriteMessage(((CentralMaster.SetMACPIBResponse)message).ToString());
					loc_send_cmd = true;
				}
				if (loc_send_cmd)
				{
					// send shutdown message; then send next message after 10 secs; change state after 10 secs too
					host_messaging.SendShutdown((ushort)ShutdownTypes.SoftReset);
					ControllerState = (int)RootNodeStates.STEP06_GET_SYSTEM_INFO_AFTER_SD;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP06_GET_SYSTEM_INFO_AFTER_SD)
			{
				if (command_type == (byte)CommandTypes.SHUTDOWN)
				{
					WriteMessage(((CentralMaster.ShutDownResponse)message).ToString());

					// wait five / ten seconds after reset
					System.Threading.Thread.Sleep(5000);  // 10000

					host_messaging.SendGetSystemInfoRequest();
					ControllerState = (int)RootNodeStates.STEP08_SET_TX_PARAMS; //(int)RootNodeStates.STEP07_SET_VENDOR_IDS;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP07_SET_VENDOR_IDS)
			{
				if (command_type == (byte)CommandTypes.GET_SYSTEM_INFO)
				{
					WriteMessage(((CentralMaster.GetSystemInfoResponse)message).ToString());

					host_messaging.SendSetInfoSubVendorProductIds((ushort)rd_vendor_id, rd_product_id);
					ControllerState = (int)RootNodeStates.STEP08_SET_TX_PARAMS;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP08_SET_TX_PARAMS)
			{
				loc_send_cmd = false;
				if (command_type == (byte)CommandTypes.GET_SYSTEM_INFO)
				{
					WriteMessage(((CentralMaster.GetSystemInfoResponse)message).ToString());
					loc_send_cmd = true;
				}
				else if (command_type == (byte)CommandTypes.SET_INFO)
				{
					WriteMessage(((CentralMaster.SetInfoResponse)message).ToString());
					loc_send_cmd = true;
				}

				if (loc_send_cmd)
				{
					//host_messaging.SendSetInfoSubTxParams(false, (byte)ModulationTypes.ROBO, 32, 0, "Cenelec A 36");
					//host_messaging.SendSetInfoSubTxParams(false, (byte)rd_modulation_type, (ushort) rd_transmit_level, 0, "Cenelec B");
					host_messaging.SendSetInfoSubTxParams(false, (byte)rd_modulation_type, (ushort) rd_transmit_level, 0, rd_tone_maskname);
					ControllerState = (int)RootNodeStates.STEP09_SET_RX_PARAMS;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP09_SET_RX_PARAMS)
			{
				if (command_type == (byte)CommandTypes.SET_INFO)
				{
					WriteMessage(((CentralMaster.SetInfoResponse)message).ToString());
					//host_messaging.SendSetInfoSubRxParams(false, false, false, 0, 0, "Cenelec A 36");
					//host_messaging.SendSetInfoSubRxParams(false, false, true,7, 0, "Cenelec B");
					host_messaging.SendSetInfoSubRxParams(false, false, rd_agc, rd_gain_value, 0, rd_tone_maskname);
					ControllerState = (int)RootNodeStates.STEP10A_M_INITIALIZING_NETWORK;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP10A_M_INITIALIZING_NETWORK)
			{
				if (command_type == (byte)CommandTypes.SET_INFO) // INSTEAD OF NETWORK_START
				{
					WriteMessage(((CentralMaster.SetInfoResponse)message).ToString());
					host_messaging.SendSetAlarm((byte)AlarmTypes.GENERAL_ERROR, true);
					ControllerState = (int)RootNodeStates.STEP10D_M_SET_MAC_PIB_COH; // STEP10D_M_START_NETWORK; //STEP10B_M_SET_ALARM; // STEP10C_M_START_NETWORK; //RootNodeStates.STEP12_M_NETWORK_READY;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP10B_M_SET_ALARM)
			{
				if (command_type == (byte)CommandTypes.SETUP_ALARM)
				{
					WriteMessage(((CentralMaster.SetAlarmResponse)message).ToString());
					host_messaging.SendSetAlarm((byte)AlarmTypes.G3_NETWORK_STATUS, true);
					ControllerState = (int)RootNodeStates.STEP10D_M_SET_MAC_PIB_COH; //STEP10D_M_START_NETWORK;
				}

			}
			else if (ControllerState == (int)RootNodeStates.STEP10D_M_SET_MAC_PIB_COH)
			{
				if (command_type == (byte)CommandTypes.SETUP_ALARM)
				{
					WriteMessage(((CentralMaster.SetAlarmResponse)message).ToString());

					host_messaging.SendSetMACPIB((uint)0xFFFFFFFA, new byte[] { 0, 0 }, 0);
					ControllerState = (int)RootNodeStates.STEP10E_M_START_NETWORK;
				}
			}

			else if (ControllerState == (int)RootNodeStates.STEP10E_M_START_NETWORK)
			{
				loc_send_cmd = false;
				if (command_type == (byte)CommandTypes.SETUP_ALARM)
				{
					WriteMessage(((CentralMaster.SetAlarmResponse)message).ToString());
					loc_send_cmd = true;
				}
				else if (command_type == (byte)CommandTypes.SET_MAC_PIB)
				{
					WriteMessage(((CentralMaster.SetMACPIBResponse)message).ToString());
					loc_send_cmd = true;
				}
				if (loc_send_cmd)
				{
					host_messaging.SendStartNetwork(rd_pan_id);
					ControllerState = (int)RootNodeStates.STEP11_M_NETWORK_START_SUCCESS;
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP11_M_NETWORK_START_SUCCESS)
			{
				if (command_type == (byte)CommandTypes.NETWORK_START)
				{
					WriteMessage(((CentralMaster.NetworkStartConfirm)message).ToString());
					ControllerState = (int)RootNodeStates.STEP12_M_NETWORK_READY;
					// initialize the local ipv6 address of coordinator
					HostApp.coord_ipv6[8] = (byte) ( (rd_pan_id >> 8) & 0x00FF);
					HostApp.coord_ipv6[9] = (byte) ( rd_pan_id & 0x00FF);
				}
			}
			else if (ControllerState == (int)RootNodeStates.STEP12_M_NETWORK_READY)
			{
				// network ready to do other functions; attach detach data transfer etc...
				ProcessMessagesInReadyState(command_type, command_sub_type, message);
			}
		}

		private void ProcessMessagesInReadyState(byte command_type, ushort command_sub_type, CentralMaster.MessageHeader message)
		{
			switch (command_type)
			{
			case (byte) CommandTypes.DATA_TRANSFER:
				WriteMessage(((CentralMaster.DataTransferConfirm)message).ToString());
				break;
			case (byte) CommandTypes.DETACH:
				WriteMessage(((CentralMaster.DetachConfirm)message).ToString());
				break;
			case (byte) CommandTypes.ATTACH:
				WriteMessage(((CentralMaster.AttachIndication)message).ToString());
				break;
			case (byte) CommandTypes.ALARM:
				if (command_sub_type == (ushort) AlarmTypes.G3_NETWORK_STATUS)
				{
					WriteMessage(((CentralMaster.AlarmSubNetworkStatus) message).ToString());
				}
				else if (command_sub_type == (ushort) AlarmTypes.GENERAL_ERROR)
				{
					WriteMessage(((CentralMaster.AlarmSubGeneralError)message).ToString());
				}
				else
				{
					WriteMessage(((CentralMaster.Alarm)message).ToString());
				}
				break;
			case (byte)CommandTypes.SETUP_ALARM:
				WriteMessage(((CentralMaster.SetAlarmResponse)message).ToString());
				break;
			default:
				WriteMessage(string.Format("ProcessMessages: received command -> {0}", command_type));
				break;
			}
		}

		#region serial_interface_funcs
		private void OpenSerialInterface()
		{
			if ( (serial_port != null) && (serial_port.IsPortOpen) && (serial_port.IsRxThreadRunning) )
			{
				// already running; simply return
				return;
			}
			else
			{
				// close it so that we can restart it
				CloseSerialInterface();
			}
            // to do find serial port
            serial_port = new Communications.SerialComm(plc_port_name)
            {
                writeToDisplay = WriteMessage,
                BaudRate = 57600,
                DataBits = 8,
                StopBitValue = System.IO.Ports.StopBits.One,
                ParityValue = System.IO.Ports.Parity.None
            };
            if (!serial_port.Open()) // serial port did not open correctly
			{
				CloseSerialInterface();
			};
		}

		private void CloseSerialInterface()
		{
			if (serial_port != null)
			{
				serial_port.Close();
				serial_port = null;
			}
		}
		#endregion //serial_interface_funcs


		#region protocol_funcs
		private void StartHostCommunication()
		{
			if (serial_port == null)
				return;
			if ( (!serial_port.IsPortOpen) || (!serial_port.IsRxThreadRunning) )
			{
				return;
			}

			// to setup hostmessages to send and rececive
			host_messaging.SerialConnection = serial_port;
			//host_messaging.writeToDisplay += WriteMessage;
			host_messaging.receivedNodeAttach += ReceviedAttachIndication;
			host_messaging.receivedNodeDetach += ReceivedDetachIndication;
			host_messaging.receivedDataTransferIndication += ReceivedDataTransferIndication;
			host_messaging.receivedDataTransferConfirm += ReceivedDataTransferConfirm;
			host_messaging.receivedErrorNotification += ReceivedErrorNotification;
			host_messaging.receivedMessage += ReceivedMessage;

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
			if (host_messaging.SerialConnection == null)
			{
				// host messaging was not setup, so simply return
				return;
			}

			//foreach(string key in node_ext_addr.Keys)
			//{
			//    byte[] ext_addr = MainWindow.StringToByteArray(key);
			//    host_messaging.SendDetachRequest(ext_addr);
			//    node_ext_addr.Clear();
			//}

			// TODO: ???
			//if (do_send_detach_req)
			//{
			//	foreach (string key in node_dict.Keys)
			//	{
			//		ServiceNode sn = node_dict[key];
			//		sn.SendDetachMessage();
			//		sn.Shutdown();
			//	}
			//}
			//node_dict.Clear();
			//node_ext_addr.Clear();


			host_messaging.SerialConnection = null;
			host_messaging.writeToDisplay -= WriteMessage;
			host_messaging.receivedNodeAttach -= ReceviedAttachIndication;
			host_messaging.receivedNodeDetach -= ReceivedDetachIndication;
			host_messaging.receivedDataTransferIndication -= ReceivedDataTransferIndication;
			host_messaging.receivedDataTransferConfirm -= ReceivedDataTransferConfirm;
			host_messaging.receivedErrorNotification -= ReceivedErrorNotification;
			host_messaging.receivedMessage -= ReceivedMessage;
		}
		#endregion // protocol_funcs

		#region threadsafe_set
		private void WriteMessage(string message)
		{
			DCLogger.Information(message);
		}

		#endregion //threadsafe_set

		#region utils
		#endregion //utils

	}
}

