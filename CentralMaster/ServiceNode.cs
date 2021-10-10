using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
//using CentralMaster.DataServiceCommunications;
using CentralMaster.json;
//using Newtonsoft.Json;
using UtilsLibrary;
using LogUtils;

/*
enum ServiceNodeStates : ushort
{
    INITIALIZED, // INTRODUCED TO INCLUDE A DELAY OF 500 MS before sending out any messages from this node
    ATTACHED,
    AUTHENTICATE_DEVICE,
    AUTHENTICATE_KEYS,
    SESSION_KEYS,
    COMM_READY,
    SHUTDOWN
}
*/



// replacing states with message based named in that state till it goes to READY
enum ServiceNodeStates : ushort
{

	ATTACHED, // INTRODUCED TO INCLUDE A DELAY OF 500 MS before sending out any messages from the next node
	COMMON_NAME,
	SYNC_TIME,
	CAPABILITY,
	PROTOCOL_SETUP,
	COMM_READY,
	SHUTDOWN
}

enum StatusValues : byte
{
	SUCCESS = 0x00,
	DECRYPT_FAILED = 0x01,
	INVALID_COMMAND = 0x02,
	INVALID_RANGE = 0x03,
	OTHER = 0x04
}

enum ProtocolIdentifiers : ushort
{
	CAPABILITY_REQUEST_ID = 0x0001,
	CAPABILITY_RESPONSE_ID = 0x0002,
	DATA_REQUEST_ID = 0x0003,
	DATA_RESPONSE_ID = 0x0004,
	COMMAND_REQUEST_ID = 0x0005,
	COMMAND_RESPONSE_ID = 0x0006,
	SIGNATURE_REQUEST_ID = 0x0007,
	SIGNATURE_RESPONSE_ID = 0x0008,
	PUBLICKEY_REQUEST_ID = 0x0009,
	PUBLICKEY_RESPONSE_ID = 0x000a,
	SESSION_REQUEST_ID = 0x000b,
	SESSION_RESPONSE_ID = 0x000c,
	RESET_REQUEST_ID = 0x000d,
	RESET_RESPONSE_ID = 0x000e,
	COMMON_NAME_REQUEST_ID = 0X000f,
	COMMON_NAME_RESPONSE_ID = 0X0010,
	SYNC_TIME_REQUEST_ID = 0x0011,
	SYNC_TIME_RESPONSE_ID = 0x0012,
	RS232_COMMAND_ID = 0x0017,
	RS232_RESPONSE_ID = 0x0018,
	BRIDGENODE_DATA_RESPONSE_ID = 0x0020,
	BRIDGENODE_COMMAND_RESPONSE_ID = 0x0022,
	PROTOCOL_CONFIRM_COMMAND_ID = 0x2D,
	PROTOCOL_CONFIRM_RESPONSE_ID = 0x2E

}

enum ServiceNodeCapability:ushort
{
	/*
     * Note if any elements are added to NODE_CAPABILITY_IDS in the ServiceNode class, a new enum value must be added to this class
     */ 
	CPB_ONOFF = 0x8001,
	CPB_INTENSITY = 0x8002,
	CPB_RGB = 0x8003,
	CPB_ACTIVE_POWER = 0x0004,
	CPB_CUMULATIVE_ENERGY = 0x0005,
	CPB_VOLTAGE = 0x0006,
	CPB_MONITORING_STATUS = 0x0007,
	CPB_PERSON_DETECT = 0x0008,
	CPB_TEMPERATURE = 0x0009,
	CPB_HUMIDITY = 0x000a,
	CPB_PRESSURE = 0x000b,
	CPB_AMBIENT_LIGHT = 0x000c,
	CPB_VOLATILE_ORGANIC = 0x000d,
	CPB_C02 = 0x000e,
	CPB_MOTION_DETECT = 0x000f,
	CPB_REPORT_INTERVAL = 0x8011,
	CPB_LINE_CURRENT = 0x0012,
	CPB_ASYNC_MSG_COUNT = 0x0016,
	CPB_LINK_QUALITY = 0x0017

}

enum RRGetCommandTypes: int
{
	RR_CMD_CONFIG = 0x01,
	RR_CMD_INIT = 0x02,
	RR_CMD_RS232 = 0x03,
	RR_CMD_RS485 = 0x04,
	RR_CMD_DALI = 0x05,
	RR_CMD_ANALOG = 0x06,
	RR_CMD_DIGITAL = 0x07,
	RR_CMD_POWERMEASUREMENT = 0x08,
	RR_CMD_I2C = 0x09
}

namespace CentralMaster
{
	public class ServiceNode //: INotifyPropertyChanged
	{

		#region static_utils_func
		public static string GetCapabilityName(ushort capability)
		{
			string tmp_name = "";
			switch (capability)
			{
				case (ushort)ServiceNodeCapability.CPB_ONOFF:
					tmp_name = "OnOff";
					break;
				case (ushort)ServiceNodeCapability.CPB_INTENSITY:
					tmp_name = "Brightness";
					break;
				case (ushort)ServiceNodeCapability.CPB_RGB:
					tmp_name = "RGB";
					break;
				case (ushort)ServiceNodeCapability.CPB_ACTIVE_POWER:
					tmp_name = "Active_Power";
					break;
				case (ushort)ServiceNodeCapability.CPB_CUMULATIVE_ENERGY:
					tmp_name = "Cumulative_Energy";
					break;
				case (ushort)ServiceNodeCapability.CPB_VOLTAGE:
					tmp_name = "Voltage";
					break;
				case (ushort)ServiceNodeCapability.CPB_MONITORING_STATUS:
					tmp_name = "Monitoring_Status";
					break;
				case (ushort)ServiceNodeCapability.CPB_PERSON_DETECT:
					tmp_name = "Person_In";
					break;
				case (ushort)ServiceNodeCapability.CPB_TEMPERATURE:
					tmp_name = "Temperature";
					break;
				case (ushort)ServiceNodeCapability.CPB_HUMIDITY:
					tmp_name = "Humidity";
					break;
				case (ushort)ServiceNodeCapability.CPB_PRESSURE:
					tmp_name = "Pressure";
					break;
				case (ushort)ServiceNodeCapability.CPB_AMBIENT_LIGHT:
					tmp_name = "Ambient_Light";
					break;
				case (ushort)ServiceNodeCapability.CPB_VOLATILE_ORGANIC:
					tmp_name = "Volatile_Organic";
					break;
				case (ushort)ServiceNodeCapability.CPB_C02:
					tmp_name = "co2";
					break;
				case (ushort)ServiceNodeCapability.CPB_MOTION_DETECT:
					tmp_name = "Motion";
					break;
				case (ushort)ServiceNodeCapability.CPB_REPORT_INTERVAL:
					tmp_name = "Report_Interval";
					break;
				case (ushort)ServiceNodeCapability.CPB_LINE_CURRENT:
					tmp_name = "Current";
					break;
				case (ushort)ServiceNodeCapability.CPB_ASYNC_MSG_COUNT:
					tmp_name = "Async_Message_Count";
					break;
				case (ushort)ServiceNodeCapability.CPB_LINK_QUALITY:
					tmp_name = "Link_Quality";
					break;
			}
			return tmp_name;

		}

		public static string GetCapabilityUnits(ushort capability)
		{
			string tmp_units = "";
			switch (capability)
			{
				case (ushort)ServiceNodeCapability.CPB_INTENSITY:
					tmp_units = "%";
					break;
				case (ushort)ServiceNodeCapability.CPB_ACTIVE_POWER:
					tmp_units = "watts";
					break;
				case (ushort)ServiceNodeCapability.CPB_CUMULATIVE_ENERGY:
					tmp_units = "Wh";
					break;
				case (ushort)ServiceNodeCapability.CPB_VOLTAGE:
					tmp_units = "V rms";
					break;
				case (ushort)ServiceNodeCapability.CPB_TEMPERATURE:
					tmp_units = "degC";
					break;
				case (ushort)ServiceNodeCapability.CPB_HUMIDITY:
					tmp_units = "%RH";
					break;
				case (ushort)ServiceNodeCapability.CPB_PRESSURE:
					tmp_units = "hPa";
					break;
				case (ushort)ServiceNodeCapability.CPB_AMBIENT_LIGHT:
					tmp_units = "lux";
					break;
				case (ushort)ServiceNodeCapability.CPB_VOLATILE_ORGANIC:
					tmp_units = "ppb";
					break;
				case (ushort)ServiceNodeCapability.CPB_C02:
					tmp_units = "ppm";
					break;
				case (ushort)ServiceNodeCapability.CPB_LINE_CURRENT:
					tmp_units = "mA rms";
					break;
			}
			return tmp_units;

		}


		public static string GetStatusAsString(byte status_val)
		{
			if (status_val == (byte)StatusValues.SUCCESS)
			{
				return "Success";
			}
			else if (status_val == (byte)StatusValues.DECRYPT_FAILED)
			{
				return "Decrypt Failed";
			}
			else if (status_val == (byte)StatusValues.INVALID_COMMAND)
			{
				return "Invalid Command";
			}
			else if (status_val == (byte)StatusValues.INVALID_RANGE)
			{
				return "Invalid Range";
			}
			else if (status_val == (byte)StatusValues.OTHER)
			{
				return "Other";
			}
			return "";
		}

		public static void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
		{
			// ExpandoObject supports IDictionary so we can extend it like this
			var expandoDict = expando as IDictionary<string, object>;
			if (expandoDict.ContainsKey(propertyName))
				expandoDict[propertyName] = propertyValue;
			else
				expandoDict.Add(propertyName, propertyValue);
		}

		private static bool TestForDictionaryEquality(Dictionary<string, string> dict, Dictionary<string, string> dict2)
		{
			// Test for equality.
			bool equal = false;
			if (dict.Count == dict2.Count) // Require equal count.
			{
				equal = true;
				foreach (var pair in dict)
				{
					string value;
					if (dict2.TryGetValue(pair.Key, out value))
					{
						// Require value be equal.
						if (value != pair.Value)
						{
							equal = false;
							break;
						}
					}
					else
					{
						// Require key be present.
						equal = false;
						break;
					}
				}
			}
			return equal;
		}


		// static ServiceNode()
		// {
		// 	LoadNodeParameters();
		// }
		#endregion //static_utils_func


		#region protocol_vars
		private ushort[] NODE_CAPABILITY_IDS =
		{
			0x8001, 0x8002, 0x8003, 0x0004,
			0x0005, 0x0006, 0x0007, 0x0008,
			0x0009, 0x000a, 0x000b, 0x000c,
			0x000d, 0x000e, 0x000f, 0x0010,
			0x8011, 0x0012, 0x0016
		};

		private ushort[] NODE_CAPABILITY_DATA_LENGTHS =
		{
			0x0001, 0x0002, 0x0003, 0x0004,
			0x0004, 0x0004, 0x0001, 0x0001,
			0x0004, 0x0004, 0x0004, 0x0004,
			0x0002, 0x0002, 0x0001, 0x000c,
			0x0004, 0x0004, 0x0001
		};

		private string[] NODE_CAPABILITY_NAMES =
		{
			"CPB_ONOFF", "CPB_INTENSITY", "CPB_RGB", "CPB_ACTIVE_POWER",
			"CPB_REACTIVE_POWER", "CPB_VOLTAGE", "CPB_MONITORING_STATUS", "CPB_PERSON_DETECT",
			"CPB_TEMPERATURE", "CPB_HUMIDITY", "CPB_PRESSURE", "CPB_AMBIENT_LIGHT",
			"CPB_VOLATILE_ORGANIC", "CPB_C02", "CPB_MOTION_DETECT", "CPB_TIME",
			"CPB_REPORT_INTERVAL", "CPB_LINE_CURRENT", "CPB_ASYNC_MSG_COUNT"

		};



		#endregion // protocol_vars

		#region vars
		private byte[] _node_ipv6addr = null;
		private string _node_ipv6addr_str = "";
		private byte[] _extended_addr = null;
		private CentralMaster.HostMessages _host_messaging = null;
		private ushort _current_state;
		CancellationTokenSource msg_recv = new CancellationTokenSource();
		private bool sentCommonNameRequest = false;
		private bool receivedCommonNameResponse = false;
		private bool _updateViewThisSerialNumber = true;
		private bool sentCapabilityRequest = false;
		//private bool receivedCapabilityResponse = false;
		private bool sentSyncTimeRequest = false;
		private bool receivedSyncTimeResponse = false;
		private bool receivedProtocolSetupResponse = false;
		private bool shutdownRequest = false;
		//private bool sentResetRequest = false;
		//private bool receivedResetResponse = false;
		private byte[] recv_b = null;
		private System.Timers.Timer main_timer = null;
		private int timer_count;
		private int required_timer_count;
		private double timer_interval;
		private const int TIMEOUT_SECS = 30; // tocheck and change
		private DateTime time_sent;
		private bool timer_started = false;

		private Random rnd_node = new Random(DateTime.Now.Millisecond);

		private string _common_name = "None";
		private string _serial_num = "None"; // default value if no serial number is present
		private ushort[] _capability = null;

		public dynamic _props;
		private Dictionary<ushort, string> id_name = new Dictionary<ushort, string>();
		private Dictionary<ushort, ushort> id_length = new Dictionary<ushort, ushort>();
		//private string _command_status="Status Messages here...";

		private bool _added_to_list = false;

		private bool _do_log = false;

		private bool _pause_state_machine = false;
		//public event PropertyChangedEventHandler PropertyChanged;

		private bool _exit_mode = false;

		private Communications.ITransmit _itransmit_comms = null;

		private ushort transmit_id;
		private ushort receive_id;

		private bool _tx_rx_resp = false;
		private Object _tx_rx_lock = new Object();

		private AutoResetEvent resp_recv_event = new AutoResetEvent(false);
		private AutoResetEvent confirmation_recv_event = new AutoResetEvent(false);
		private int rd_rx_timeout_milliseconds = 0;

		private int rd_confirmation_timeout_millisecs;

		private Object _state_machine_lock = new Object();
		private bool _state_machine_exec = false;
		private int link_qti = -1;
		private DateTime _attach_time;
		private DateTime _data_cmd_time;
		private bool _is_in_subnetDC = false;

		private NodeDevices node_devices = null;

		//private ConcurrentQueue<RRCommand> sn_rr_queue = new ConcurrentQueue<RRCommand>();
		//private ConcurrentDictionary<string, bool> configured = new ConcurrentDictionary<string, bool>();
		//private ConcurrentDictionary<string, bool> initialized = new ConcurrentDictionary<string, bool>();

		private bool send_to_cloud = false; //true;

		private int rd_rrtask_sleep_secs = 300;

		private bool config_sent;
		private bool init_sent;
		private List<RRCommand> config_cmd_list = new List<RRCommand>();
		private List<RRCommand> init_cmd_list = new List<RRCommand>();
		private List<RRCommand> rs232_cmd_list = new List<RRCommand>();
		private List<RRCommand> rs485_cmd_list = new List<RRCommand>();
		private List<RRCommand> dali_cmd_list = new List<RRCommand>();
		private List<RRCommand> analog_cmd_list = new List<RRCommand>();
		private List<RRCommand> digital_cmd_list = new List<RRCommand>();
		private List<RRCommand> powermeasurement_cmd_list = new List<RRCommand>();
		private List<RRCommand> i2c_cmd_list = new List<RRCommand>();
		private List<int> rr_cmds_list = new List<int>();
		private int curr_rr_cmd_index = -1;  // index into the rr_cmds_list
		private string cmd_event_id = "";
		private DataServiceCommunications.IDCDataServiceComms _dc_dataservice_comms = null;
		private List<TransferData> node_recvd_data = new List<TransferData>();
		private Dictionary<string, string> data_name_map = new Dictionary<string, string>();

		#endregion // vars

		#region property
		public string CommonName
		{
			get { return _common_name; }
		}

		public string SerialNumber
		{
			get { return _serial_num; }
		}

		public bool UpdateViewThisSerialNumber
		{
			get { return _updateViewThisSerialNumber; }
			set { _updateViewThisSerialNumber = value; }
		}

		public ushort[] Capability
		{
			get { return _capability; }
		}

		public dynamic Props
		{
			get { return _props; }
		}

		public bool AddedToList
		{
			get { return _added_to_list; }
			set
			{
				_added_to_list = value;
			}
		}

		public byte[] IPv6AddressBytes
		{
			get { return _node_ipv6addr; }
		}

		public string IPv6AddresString
		{
			get { return _node_ipv6addr_str; }
		}
		public byte[] ExtendedAddress
		{
			get { return _extended_addr; }
		}

		public ushort NodeState
		{
			get { return _current_state; }
		}

		public bool IsReadyState
		{
			get 
			{
				return (_current_state == (ushort)ServiceNodeStates.COMM_READY);
			}
		}

		public bool ExitMode
		{
			get
			{
				return _exit_mode;
			}
			set
			{
				_exit_mode = value;
			}
		}

		public bool StateMachinePause
		{
			get { return _pause_state_machine; }
			set 
			{ 
				_pause_state_machine = value; 
				if (_pause_state_machine == true) 
				{
					//RollingFileLogger.Verbose("SM: Pause property State machine");
					CheckExecuteStateMachine(false);
				}
			}
		}


		public bool RxResponseForTx 
		{
			get 
			{
				bool to_ret_bool;
				lock(_tx_rx_lock)
				{
					to_ret_bool = _tx_rx_resp;
				}
				return to_ret_bool;
			}
			set 
			{
				bool to_set_bool = value;
				lock(_tx_rx_lock)
				{
					_tx_rx_resp = to_set_bool;
				}
			}
		}

		public DateTime GetAttachTime
		{
			get {
				return _attach_time;
			}
		}

		public double TimeSecondsSinceAttach
		{
			get {
				DateTime dt_now = DateTime.Now;
				return ( (dt_now - _attach_time).TotalSeconds + 2.0); // adding 2 to support border line cases 			
			}
		}

		public double TimeSecondsSinceDataRequest
		{
			get {
				if (_data_cmd_time != DateTime.MinValue)
				{
					DateTime dt_now = DateTime.Now;
					return ( (dt_now - _attach_time).TotalSeconds + 2.0); // adding 2 to support border line cases 			
				}
				else 
				{
					return 0.0;
				}
			}
		}

		public bool IsPartOfSubnetDC
		{
			get {
				return _is_in_subnetDC;
			}
			set
			{ _is_in_subnetDC = value;}
		}

		public bool SendToCloud
		{
			get {
				return send_to_cloud;
			}
			set {
				send_to_cloud = value;
			}
		}

		#endregion // property

		public ServiceNode(byte[] node_ipv6addr, byte[] ext_addr, CentralMaster.HostMessages host_messaging,
						   System.Timers.Timer app_timer, Communications.ITransmit itransmit_comms, 
						   int rd_rx_timeout_ms, int rd_confirmation_timeout_secs,
						   int rd_round_robin_task_sleep_secs, 
						   DataServiceCommunications.IDCDataServiceComms dc_dataservice_comms)
		{
			// node is created when a new ATTACH message is received by the device
			_node_ipv6addr = new byte[node_ipv6addr.Length];
			Array.Copy(node_ipv6addr, _node_ipv6addr, node_ipv6addr.Length);
			_node_ipv6addr_str = BitConverter.ToString(node_ipv6addr);
			_extended_addr = new byte[ext_addr.Length];
			Array.Copy(ext_addr, _extended_addr, ext_addr.Length);
			_host_messaging = host_messaging;
			main_timer = app_timer;
			timer_interval = main_timer.Interval;
			_itransmit_comms = itransmit_comms;
			StartServiceNodeStateMachine();
			for (int ii = 0; ii < NODE_CAPABILITY_IDS.Length; ii++)
			{
				id_name.Add(NODE_CAPABILITY_IDS[ii], NODE_CAPABILITY_NAMES[ii]);
				id_length.Add(NODE_CAPABILITY_IDS[ii], NODE_CAPABILITY_DATA_LENGTHS[ii]);
			}

			rd_rx_timeout_milliseconds = rd_rx_timeout_ms;
			rd_confirmation_timeout_millisecs = rd_confirmation_timeout_secs * 1000; 
			_attach_time = DateTime.Now;
			rd_rrtask_sleep_secs = rd_round_robin_task_sleep_secs;
			_dc_dataservice_comms = dc_dataservice_comms;
		}


		#region state_machine
		private void StartServiceNodeStateMachine()
		{
			sentCommonNameRequest = false;
			receivedCommonNameResponse = false;
			sentCapabilityRequest = false;
			//receivedCapabilityResponse = false;
			receivedProtocolSetupResponse = false;
			// _current_state = (ushort)ServiceNodeStates.ATTACHED;
			// StateMachine(); // moves it to common name state
			_current_state = (ushort)ServiceNodeStates.PROTOCOL_SETUP;
			_pause_state_machine = true;
			//StateMachine();
		}

		public void ExtStateMachine()
		{
			//RollingFileLogger.Verbose("SM: EXT State machine");
			Task.Run( () => CheckExecuteStateMachine(false));
		}

		private void CheckExecuteStateMachine(bool wait_and_execute)
		{
			// if bool wait_and_exec is true, wait for current statemachine to complete and then execute
			// else if wait_and_exec is false, exit immediately without calling statemachine
			bool is_exec;
			int loop_max; // 300 ms it must finish
			lock(_state_machine_lock)
			{
				is_exec = _state_machine_exec;
			}
			if (!wait_and_execute)
			{
				if (!is_exec)
				{
					//RollingFileLogger.Verbose("CESM: Executing SM");
					StateMachine();
				}
			}
			else 
			{
				loop_max = 0;
				while ( (is_exec) && (loop_max < 10) ) 
				{
					System.Threading.Thread.Sleep(100);
					loop_max ++;
					// wait for current execution to complete
					lock(_state_machine_lock)
					{
						is_exec = _state_machine_exec;
					}
				}
				if (!is_exec) 
				{
					//RollingFileLogger.Debug("CESM: Executing SM");
					StateMachine();
				} 
				else 
				{
					DCLogger.Information("Wait to execute state machine failed. Current Exection of state machine did not complete in 1sed");
				}
			}
		}
		private void StateMachine()
		{
			lock(_state_machine_lock)
			{
				_state_machine_exec = true;
			}
			//RollingFileLogger.Debug(string.Format("executing state machine => {0}", _current_state));
			//RollingFileLogger.Debug(string.Format("state machine {2} => 0x{0:x} at {1}", _current_state, 
			//	DateTime.Now.TimeOfDay.ToString(), BitConverter.ToString(_node_ipv6addr)));

			if (_pause_state_machine)
			{
				lock(_state_machine_lock)
				{
					_state_machine_exec = false;
				}
				//RollingFileLogger.Debug("DONE executing state machine in pause.");
				return;
			}
			if (_current_state == (ushort)ServiceNodeStates.ATTACHED)
			{
				//Thread.Sleep(500);
				//_current_state = (ushort)(ServiceNodeStates.COMMON_NAME);
				_current_state = (ushort)(ServiceNodeStates.PROTOCOL_SETUP);
			}
			else if (_current_state == (ushort) ServiceNodeStates.PROTOCOL_SETUP)
			{
				DCLogger.Information("in protocol setup state machine");
				ProtocolSetupTask();
			}
			else if (_current_state == (ushort)ServiceNodeStates.COMM_READY)
			{
				CommReadyTask();
			}
			else if (_current_state == (ushort)ServiceNodeStates.SHUTDOWN)
			{
				// do nothing
				// cannot leave shutdown state.. will not process any data... need to dispose and create a new node
				_pause_state_machine = true;
			}
			lock(_state_machine_lock)
			{
				_state_machine_exec = false;
			}
			DCLogger.Information("DONE executing state machine");

		}

		private void StateMachineNextState(string trigger_str)
		{
			// bool state_changed = false;	
			DCLogger.Debug($"curr state: {_current_state}, trigger => {trigger_str}");
			if ((_current_state == (ushort) ServiceNodeStates.PROTOCOL_SETUP) &&
				(trigger_str.Equals("RECEIVED_PROTOCOLSETUP_RESPONSE"))
			)
			{
				// debug
				//RollingFileLogger.Debug("going to comm ready from proto setup");
				_current_state = (ushort)ServiceNodeStates.COMM_READY;
				// state_changed = true;
			}
			else if ( ( (_current_state == (ushort) ServiceNodeStates.PROTOCOL_SETUP) || 
						(_current_state == (ushort)ServiceNodeStates.COMM_READY) ) &&
					  (trigger_str.Equals("SHUTDOWN")
				    )
			)
			{
				_current_state = (ushort)ServiceNodeStates.SHUTDOWN;
				// state_changed = true;
			}

			// if ( (state_changed) && (trigger_str.Equals("SHUTDOWN") ) )
			// {
			// 	//RollingFileLogger.Information("in state changed statemachine call");
			// 	RollingFileLogger.Debug("SM: state changed State machine");
			// 	CheckExecuteStateMachine(true);
			// }
		}

		private void ProtocolSetupTask()
		{
			int index;
			byte[] tmp_b;
			int tmp_b_len;
			// if asked to shutdown go to the shutdown state
			if (shutdownRequest)
			{
				StateMachineNextState("SHUTDOWN");
				recv_b = null;
				return;
			}

			if (recv_b != null)
			{
				//RollingFileLogger.Debug(BitConverter.ToString(recv_b));
				index = 2;
				while (index < recv_b.Length)
				{
					//RollingFileLogger.Debug(string.Format("index protocol setup task : {0}, tot len {1}", index, recv_b.Length));
					ushort message_id = BitConverter.ToUInt16(recv_b, index+2);
					if (message_id == (ushort)ProtocolIdentifiers.COMMON_NAME_RESPONSE_ID)
					{
						tmp_b_len = BitConverter.ToUInt16(recv_b, index);
						tmp_b = new byte[tmp_b_len];
						Array.Copy(recv_b, index, tmp_b, 0, tmp_b_len);
						ProcessCommonNameData(tmp_b);
						receivedCommonNameResponse = true;
						_updateViewThisSerialNumber = false;
						index += tmp_b_len;
					}
					else if (message_id == (ushort)ProtocolIdentifiers.SYNC_TIME_RESPONSE_ID)
					{
						tmp_b_len = BitConverter.ToUInt16(recv_b, index);
						tmp_b = new byte[tmp_b_len];
						// process time data
						if (recv_b[index+4] == 0x00) // success
						{
							receivedSyncTimeResponse = true;
						}
						index += tmp_b_len;
					}
					else if ( (message_id == (ushort)ProtocolIdentifiers.PROTOCOL_CONFIRM_RESPONSE_ID) )
                    {
						DCLogger.Information("message id protocol confirm response id recvd.");
						receivedProtocolSetupResponse = true;
						index = recv_b.Length;
					}
					//else if (message_id == (ushort)ProtocolIdentifiers.CAPABILITY_RESPONSE_ID)
					//{
					//	tmp_b_len = BitConverter.ToUInt16(recv_b, index);
					//	tmp_b = new byte[tmp_b_len];
					//	Array.Copy(recv_b, index, tmp_b, 0, tmp_b_len);
					//	ProcessCapabilityData(tmp_b);
					//	receivedCapabilityResponse = true;
					//	index += tmp_b_len;
					//}
					else if (message_id == (ushort)ProtocolIdentifiers.DATA_RESPONSE_ID) {
						tmp_b_len = BitConverter.ToUInt16(recv_b, index);
						tmp_b = new byte[tmp_b_len];
						Array.Copy(recv_b, index, tmp_b, 0, tmp_b_len);
						//ProcessDataResponse(tmp_b);
						ProcessAsRRCommand(tmp_b);
						index += tmp_b_len;
					}
					//RollingFileLogger.Debug(string.Format("recv common: {0}, recv synctime {1}, recv capab {2}", receivedCommonNameResponse, receivedSyncTimeResponse, receivedCapabilityResponse));
					if (receivedCommonNameResponse && receivedSyncTimeResponse )
					{
						//receivedProtocolSetupResponse = true;
						//SendProtocolSetupConfirmEventWithTimeout();
					}
				}
				recv_b = null;
			}
			if (receivedProtocolSetupResponse)
			{
				// goto next state
				DCLogger.Information("recvd proto setup response");
				StateMachineNextState("RECEIVED_PROTOCOLSETUP_RESPONSE");
			}
		}


		private void CommReadyTask()
		{
			int recv_idx;
			ushort message_id;
			int message_len;
			// if asked to shutdown go to the shutdown state
			if (shutdownRequest)
			{
				StateMachineNextState("SHUTDOWN");
				recv_b = null;
				return;
			}
			//RollingFileLogger.Verbose("start of comm ready task");
			if (recv_b != null)
			{
				recv_idx = 2;
				while (recv_idx < recv_b.Length)
				{
					message_len = BitConverter.ToUInt16(recv_b, recv_idx);
					message_id = BitConverter.ToUInt16(recv_b, recv_idx+2);
					DCLogger.Debug(string.Format("idx {0}, total len {1}, message len {2}, message id {3}", recv_idx, recv_b.Length, message_len, message_id));
					byte[] tmp_pkt = new byte[message_len];
					Array.Copy(recv_b, recv_idx, tmp_pkt, 0, message_len);
					recv_idx += message_len;
					if (message_id == (ushort)ProtocolIdentifiers.DATA_RESPONSE_ID)
					{
						ProcessAsRRCommand(tmp_pkt);
						tmp_pkt = null;
					}
					else if (message_id == (ushort)ProtocolIdentifiers.COMMAND_RESPONSE_ID)
					{
						ProcessCommandResponse(tmp_pkt);
						tmp_pkt = null;
					}
					else if (message_id == (ushort) ProtocolIdentifiers.BRIDGENODE_DATA_RESPONSE_ID)
					{
						ProcessBridgeNodeDataResponse(tmp_pkt);
						tmp_pkt = null;
					}
					else if (message_id == (ushort) ProtocolIdentifiers.BRIDGENODE_COMMAND_RESPONSE_ID)
					{
						ProcessBridgeNodeDataResponse(tmp_pkt);
						tmp_pkt = null;
					}
					else if (message_id == (ushort) ProtocolIdentifiers.RS232_RESPONSE_ID )
					{
						ProcessAsRRCommand(tmp_pkt);
						//ProcessRs232Response(tmp_pkt);
						tmp_pkt = null;
					} 
					else
					{
						ProcessAsRRCommand(tmp_pkt);
						//ProcessGenericResponse(tmp_pkt);
						tmp_pkt = null;
					}
				}
			}
			//RollingFileLogger.Debug("END of comm ready task");
		}

		private void ProcessReceivedMessage(object[] msg_recv)
		{
			//byte[] msg_bytes = null;
			//ushort tmpu16;
			if ( ((byte)msg_recv[0] == (byte)CommandTypes.DATA_TRANSFER) && (msg_recv[1] is CentralMaster.DataTransferIndication) )
			{
				// processs data 
				//RollingFileLogger.Verbose("callling recv autoreset set ");
				CentralMaster.DataTransferIndication dti = (CentralMaster.DataTransferIndication)msg_recv[1];
				link_qti = (int)dti.link_quality; // this will be appended when sending data to GUI
				//RollingFileLogger.Debug(dti.ToString());
			// 	if ( (dti.data_recv[2+2] == 0x04) && (dti.data_recv[3+2] == 0x00) ) // +2 for addition for packaged messages
			// 	{
			// 		// need to add link quality indicator to message bytes ( 2 bytes for link quality id, and 2 bytes for teh value)
			// 		msg_bytes = new byte[dti.data_recv.Length + 4]; // for link quality indication (current reproted values in range 0x00-0xFF but using uint16 for future upgrades)
			// 		// it is data response, only then modify the capability
			// 		Array.Copy(dti.data_recv, msg_bytes, dti.data_recv.Length);
			// 		tmpu16 = BitConverter.ToUInt16(msg_bytes,0); // msg size at combined packet
			// 		tmpu16 += 4;
			// 		byte[] tmpb = BitConverter.GetBytes(tmpu16);
			// 		msg_bytes[0] = tmpb[0];
			// 		msg_bytes[1] = tmpb[1];
			// 		tmpu16 = BitConverter.ToUInt16(msg_bytes,2); // msg szie of first packet 
			// 		tmpu16 += 4;
			// 		byte[] tmpb2 = BitConverter.GetBytes(tmpu16);
			// 		msg_bytes[2] = tmpb2[0];
			// 		msg_bytes[3] = tmpb2[1];
			// 		tmpu16 = BitConverter.ToUInt16(msg_bytes,4+2); // number of capability ids in message
			// 		tmpu16 += 1;
			// 		tmpb = BitConverter.GetBytes(tmpu16);
			// 		msg_bytes[4+2] = tmpb[0];
			// 		msg_bytes[5+2] = tmpb[1];
			// 		// little endian format
			// 		msg_bytes[dti.data_recv.Length] = (byte)( (ushort)ServiceNodeCapability.CPB_LINK_QUALITY & 0x00FF );
			// 		msg_bytes[dti.data_recv.Length+1] = (byte)( ((ushort)ServiceNodeCapability.CPB_LINK_QUALITY & 0xFF00) >> 8 );
			// 		msg_bytes[dti.data_recv.Length+2] = dti.link_quality;
			// 		msg_bytes[dti.data_recv.Length+3] = 0x00;
			// 		//RollingFileLogger.Debug("finished modifying recv message data confirmation");
			// 	}
			// 	else 
			// 	{
			// 		msg_bytes = new byte[dti.data_recv.Length];
			// 		Array.Copy(dti.data_recv, msg_bytes, dti.data_recv.Length);
			// 	}

				if (dti.data_recv == null)
				{
					return;
				}
				// require a minimum of 6 bytes to process
				if (dti.data_recv.Length < 5)
				{
					// TODO: send message to console that INVALID DATA is received
					return;
				}
				receive_id = BitConverter.ToUInt16(dti.data_recv, 2+2);	
				DCLogger.Information(string.Format("tx id {0}, rx id {1}", transmit_id, receive_id));
				if ( ((transmit_id+1) == receive_id) || 
					 (transmit_id == ProtocolMessagesG3PLC.DATA_REQUEST_ID  && receive_id == ProtocolMessagesG3PLC.BRIDGENODE_DATA_RESPONSE_ID) ||
					 (transmit_id == ProtocolMessagesG3PLC.COMMAND_REQUEST_ID && receive_id == ProtocolMessagesG3PLC.DATA_RESPONSE_ID) ||
					 (transmit_id == ProtocolMessagesG3PLC.COMMAND_REQUEST_ID && receive_id == ProtocolMessagesG3PLC.BRIDGENODE_DATA_RESPONSE_ID)
				   )
				{
					lock(_tx_rx_lock)
					{
						_tx_rx_resp = true;
					}
				}
				// copy data to recv global buffer
				recv_b = new byte[dti.data_recv.Length];
				Array.Copy(dti.data_recv, recv_b, dti.data_recv.Length);
				//RollingFileLogger.Debug("SM: Process Recv Message State machine");
				CheckExecuteStateMachine(true);
				resp_recv_event.Set();
			}
			else if ( ((byte)msg_recv[0] == (byte)CommandTypes.DATA_TRANSFER) && (msg_recv[1] is CentralMaster.DataTransferConfirm) )
			{

				// if status is error, then call 
				CentralMaster.DataTransferConfirm dtc = (CentralMaster.DataTransferConfirm) msg_recv[1];
				DCLogger.Information(dtc.ToString());
				confirmation_recv_event.Set();
				if (dtc.status != 0x0000)
				{
					// an error occured, so relese the wait event, as we may not get a response
					resp_recv_event.Set();
					_exit_mode = true;
				}
			}
		}


		private void ProcessCommonNameData(byte[] msg_bytes)
		{
			ushort common_name_len = BitConverter.ToUInt16(msg_bytes, 4);
			string tmpstr = System.Text.Encoding.UTF8.GetString(msg_bytes, 6, common_name_len);
			string[] tmp_parts = tmpstr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			_common_name = tmp_parts[0];
			if (tmp_parts.Length > 1)
			{
				_serial_num = tmp_parts[1];
			}
		}

		private void ProcessAsRRCommand(byte[] msg_bytes)
		{
			if ((curr_rr_cmd_index < 0) || (curr_rr_cmd_index > rr_cmds_list.Count))
            {
				DCLogger.Information($"Unable to process current command index -> {curr_rr_cmd_index}, rr_cmd_list count = {rr_cmds_list.Count}");
				return;
			}

			List<RRCommand> loc_rr_cmd_list = null;
			switch (rr_cmds_list[curr_rr_cmd_index])
			{
				case (int)RRGetCommandTypes.RR_CMD_CONFIG:
					loc_rr_cmd_list = config_cmd_list;
					config_sent = true;
					break;
				case (int)RRGetCommandTypes.RR_CMD_INIT:
					loc_rr_cmd_list = init_cmd_list;
					init_sent = true;
					break;
				case (int)RRGetCommandTypes.RR_CMD_RS232:
					loc_rr_cmd_list = rs232_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_RS485:
					loc_rr_cmd_list = rs485_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_DALI:
					loc_rr_cmd_list = dali_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_ANALOG:
					loc_rr_cmd_list = analog_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_DIGITAL:
					loc_rr_cmd_list = digital_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_POWERMEASUREMENT:
					loc_rr_cmd_list = powermeasurement_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_I2C:
					loc_rr_cmd_list = i2c_cmd_list;
					break;
				default:
					DCLogger.Information($"unknown command enum {rr_cmds_list[curr_rr_cmd_index]}");
					break;
			}
			bool process_node_data = false;
			if (curr_rr_cmd_index == (rr_cmds_list.Count - 1) )
            {
				process_node_data = true;
            }
			curr_rr_cmd_index = -1;
			if (loc_rr_cmd_list == null)
			{
				DCLogger.Information($"Failed to compute response for command list for {curr_rr_cmd_index}");
				return;
			}
			string tmp_node_name = _common_name + "-" + _serial_num;
			int number_of_channels = msg_bytes[4];
			int index = 5; // 2 bytes pkt len; 2 bytes id; 1 byte num of channels
			int ch_num;
			int ch_data_len;
			int ch_start_idx;
			ushort pkt_id = BitConverter.ToUInt16(msg_bytes, 2);
			int ii;
			DCLogger.Debug($"procrrcmd: pkt_id {pkt_id}");

            try
            {
                switch (pkt_id)
                {
                    case ProtocolMessagesG3PLC.RS232_RESPONSE_ID:
                    case ProtocolMessagesG3PLC.RS485_RESPONSE_ID:
                    case ProtocolMessagesG3PLC.I2C_RESPONSE_ID:
                        while (index < msg_bytes.Length)
                        {
                            ch_num = msg_bytes[index];
                            ch_start_idx = index + 5; // channel number
                            ch_data_len = msg_bytes[index + 4];
                            if (msg_bytes[index + 3] == 0x00) // success
                            {
                                RRCommand tmp_rr_cmd = loc_rr_cmd_list.Find(x => x.channel == ch_num);
                                if (tmp_rr_cmd != null)
                                {
                                    List<ResponseData> list_rd = DeviceData.ComputeValues(tmp_rr_cmd.device_name, tmp_rr_cmd.command_name, msg_bytes, ch_start_idx, ch_data_len);
                                    if (list_rd != null)
                                    {
                                        string tmp_prefix = "i2c" + tmp_rr_cmd.channel.ToString("00_");
                                        if ((tmp_rr_cmd.data_names != null) && (tmp_rr_cmd.data_names.Length > 0))
                                        {
                                            string[] tmp_name_map = tmp_rr_cmd.data_names.Replace(" ", "").Replace(":", ",").Split(',');
                                            if (tmp_name_map.Length % 2 == 0)
                                            {
                                                for (ii = 0; ii < tmp_name_map.Length; ii += 2)
                                                {
                                                    data_name_map.Add(tmp_prefix + tmp_name_map[0].Trim(), tmp_name_map[1].Trim());
                                                }
                                            }
                                        }

                                        foreach (var tmprd in list_rd)
                                        {
                                            tmprd.Name = tmp_prefix + tmprd.Name;
                                            if (cmd_event_id.Length > 0)
                                            {
                                                tmprd.EventId = cmd_event_id;
                                            }
                                        }
                                        if (cmd_event_id.Length > 0)
                                        {
                                            cmd_event_id = "";
                                        }
                                        AddToTransferList(tmp_node_name, tmp_rr_cmd.uuid, list_rd);
                                        foreach (var tmp_rd in list_rd)
                                        {
                                            DCLogger.Debug(tmp_rd.ToString());
                                        }
                                    }
                                }
                            }
                            index += (ch_data_len + 5 + 1); // 5 => cmd type, address, status, command response count, number of bytes
                        }
                        break;
                    case ProtocolMessagesG3PLC.DALI_RESPONSE_ID:
                        DCLogger.Information($" Unimplemented response id recvd => {pkt_id}");
                        break;
                    case ProtocolMessagesG3PLC.ANALOG_INPUT_RESPONSE_ID:
                        while (index < msg_bytes.Length)
                        {
                            ch_num = msg_bytes[index];
                            ch_start_idx = index + 1;
                            ch_data_len = 4; // float 4 bytes
                            DCLogger.Debug($"procrrcmd: channel {ch_num} ch start idx {ch_start_idx}");
                            RRCommand tmp_rr_cmd = loc_rr_cmd_list.Find(x => x.channel == ch_num);
                            if (tmp_rr_cmd != null)
                            {
                                DCLogger.Debug($"procrrcmd: cmd name = {tmp_rr_cmd.command_name} dev name = {tmp_rr_cmd.device_name}");
                            }
                            else
                            {
                                DCLogger.Debug("procrrcmd: tmp_rr_cmd is NULL");
                            }
                            if (tmp_rr_cmd != null)
                            {
                                List<ResponseData> list_rd = DeviceData.ComputeValues(tmp_rr_cmd.device_name, tmp_rr_cmd.command_name, msg_bytes, ch_start_idx, ch_data_len);
                                if (list_rd != null)
                                {
                                    DCLogger.Debug($"computed {list_rd[0].ToString()}");
                                }
                                else
                                {
                                    DCLogger.Debug($"computed list was NULL");

                                }
                                if (list_rd != null)
                                {
                                    string tmp_prefix = "ana" + tmp_rr_cmd.channel.ToString("00_");

                                    if ((tmp_rr_cmd.data_names != null) && (tmp_rr_cmd.data_names.Length > 0))
                                    {
                                        string[] tmp_name_map = tmp_rr_cmd.data_names.Replace(" ", "").Replace(":", ",").Split(',');
                                        if (tmp_name_map.Length % 2 == 0)
                                        {
                                            for (ii = 0; ii < tmp_name_map.Length; ii += 2)
                                            {
                                                data_name_map.Add(tmp_prefix + tmp_name_map[0].Trim(), tmp_name_map[1].Trim());
                                            }
                                        }
                                    }
                                    foreach (var tmprd in list_rd)
                                    {
                                        tmprd.Name = tmp_prefix + tmprd.Name;
                                        if (cmd_event_id.Length > 0)
                                        {
                                            tmprd.EventId = cmd_event_id;
                                        }
                                    }
                                    if (cmd_event_id.Length > 0)
                                    {
                                        cmd_event_id = "";
                                    }
                                    AddToTransferList(tmp_node_name, tmp_rr_cmd.uuid, list_rd);
                                    foreach (var tmp_rd in list_rd)
                                    {
                                        DCLogger.Debug(tmp_rd.ToString());
                                    }
                                }
                            }
                            index += (ch_data_len + 1);
                        }
                        break;
                    case ProtocolMessagesG3PLC.DIGITAL_INPUT_RESPONSE_ID:
                        while (index < msg_bytes.Length)
                        {
                            ch_num = msg_bytes[index];
                            ch_start_idx = index + 1;
                            ch_data_len = 1; // float 4 bytes
                            RRCommand tmp_rr_cmd = loc_rr_cmd_list.Find(x => x.channel == ch_num);
                            if (tmp_rr_cmd != null)
                            {
                                List<ResponseData> list_rd = DeviceData.ComputeValues(tmp_rr_cmd.device_name, tmp_rr_cmd.command_name, msg_bytes, ch_start_idx, ch_data_len);
                                if (list_rd != null)
                                {
                                    foreach (var tmprd in list_rd)
                                    {
                                        tmprd.EventId = cmd_event_id;
                                    }
                                    if (cmd_event_id.Length > 0)
                                    {
                                        cmd_event_id = "";
                                    }
                                    AddToTransferList(tmp_node_name, tmp_rr_cmd.uuid, list_rd);
                                    foreach (var tmp_rd in list_rd)
                                    {
                                        DCLogger.Debug(tmp_rd.ToString());
                                    }
                                }
                            }
                            index += (ch_data_len + 1);
                        }
                        break;
                    case ProtocolMessagesG3PLC.POWER_MEASUREMENT_RESPONSE_ID:
						//DCLogger.Information($" Unimplemented response id recvd => {pkt_id}");
						while (index < msg_bytes.Length)
						{
							ch_num = msg_bytes[index];
							ch_start_idx = index + 1;
							ch_data_len = 22; // 5 float values: 4 bytes each + 2 uint8 values
							RRCommand tmp_rr_cmd = loc_rr_cmd_list.Find(x => x.channel == ch_num);
							if (tmp_rr_cmd != null)
							{
								List<ResponseData> list_rd = DeviceData.ComputeValues(tmp_rr_cmd.device_name, tmp_rr_cmd.command_name, msg_bytes, ch_start_idx, ch_data_len);
								if (list_rd != null)
								{
									foreach (var tmprd in list_rd)
									{
										tmprd.EventId = cmd_event_id;
									}
									if (cmd_event_id.Length > 0)
									{
										cmd_event_id = "";
									}
									AddToTransferList(tmp_node_name, tmp_rr_cmd.uuid, list_rd);
									foreach (var tmp_rd in list_rd)
									{
										DCLogger.Debug(tmp_rd.ToString());
									}
								}
							}
                            else
                            {
								DCLogger.Debug($"Failed to find channel {ch_num}");
								foreach (RRCommand rrcmd in loc_rr_cmd_list)
                                {
									DCLogger.Debug($"cmd: {rrcmd.command_name}, dev name {rrcmd.device_name}, channel {rrcmd.channel}");
                                }

                            }
							index += (ch_data_len + 1);
						}
						break;
                    default:
                        DCLogger.Information($" Unsupported response id recvd => {pkt_id}");
                        break;
                }
                if (process_node_data)
                {
                    List<ResponseData> node_rd = null;
                    DCLogger.Information("Process node data and send to DataServices Layer");
                    if (node_devices.computedData != null)
                    {
                        if (
                            (node_devices.computedData.name != null && node_devices.computedData.name.Length > 0) &&
                            (node_devices.computedData.compute != null && node_devices.computedData.compute.Length > 0) &&
                            (node_devices.computedData.compute_values != null))
                        {
                            var param_dict = DerivedParameters.GetParameters();
                            node_rd = (List<ResponseData>)node_devices.computedData.compute_values.Invoke(null,
                                                                new object[] { node_recvd_data, param_dict });
                        }
                    }
                    if (node_rd != null)
                    {
                        foreach (var tmp_rd in node_rd)
                        {
                            DCLogger.Debug(tmp_rd.ToString());
                        }

                        TransferData tmp_td = new TransferData();
                        tmp_td.NodeName = tmp_node_name;
                        tmp_td.UUID = node_devices.computedData.uuid;
                        tmp_td.Data = new ResponseData[node_rd.Count];
                        Array.Copy(node_rd.ToArray(), tmp_td.Data, node_rd.Count);
                        node_recvd_data.Add(tmp_td);
                        if ((node_devices.computedData.data_names != null) &&
                             (node_devices.computedData.data_names.Length > 0)
                            )
                        {
                            string[] tmp_name_map = node_devices.computedData.data_names.Replace(" ", "").Replace(":", ",").Split(',');
                            DCLogger.Debug($"node: map name length {tmp_name_map.Length}");
                            if (tmp_name_map.Length % 2 == 0)
                            {
                                for (int kk = 0; kk < tmp_name_map.Length; kk += 2)
                                {
                                    DCLogger.Verbose($"{kk}: {tmp_name_map[kk]} => {tmp_name_map[kk + 1]}");
                                    data_name_map.Add(tmp_name_map[kk].Trim(), tmp_name_map[kk + 1].Trim());
                                }
                            }
                        }
                    }
                    else
                    {
                        DCLogger.Information("NODE RD computed data is NULL NULL NULL");
                    }
                    // MODIFY THE NAMES ACCCORDING TO data_name_map variable before pushing it to DataService
                    DCLogger.Information($"datamap length = {data_name_map.Count}");
                    foreach (TransferData tmp_td in node_recvd_data)
                    {
                        foreach (var tmp_rd in tmp_td.Data)
                        {
                            if (data_name_map.ContainsKey(tmp_rd.Name))
                            {
                                tmp_rd.Name = data_name_map[tmp_rd.Name];
                                // debug
                                DCLogger.Debug(tmp_rd.ToString());

                            }
                        }
                    }

                    TransferDataList tdl = new TransferDataList();
                    tdl.Items = node_recvd_data;
                    string tmpstr = System.Text.Json.JsonSerializer.Serialize(tdl);
                    DCLogger.Information("Sending to data services layer");
                    _dc_dataservice_comms.TransmitToDataService(tmpstr);
                }

            }
            catch (Exception excp_prrcmd)
            {
				DCLogger.Error(excp_prrcmd, "ProcessASRRCommand");
				DCLogger.Error(excp_prrcmd.StackTrace);
            }			
			// keep the current behavior to send data to ui
			//SendToUserInterface(_node_ipv6addr, _common_name, _serial_num, msg_bytes);
		}

		private void AddToTransferList(string node_name, string uuid, List<ResponseData> list_rd)
        {
            TransferData tfd = new TransferData
            {
                NodeName = node_name,
                UUID = uuid,
                Data = list_rd.ToArray()
            };
            node_recvd_data.Add(tfd);
		}

		private void ProcessBridgeNodeDataResponse(byte[] msg_bytes)
		{
			UInt16 tmpu16_1;
			byte [] mod_bytes;
			
			DCLogger.Information(string.Format("Sending Data response to ui; length = {0}", msg_bytes.Length));
			tmpu16_1 = BitConverter.ToUInt16(msg_bytes, 0); // total len of msg_bytes
			mod_bytes = new byte[tmpu16_1-2];
			System.Array.Copy(msg_bytes, 2, mod_bytes, 0, tmpu16_1-2 );
			SendToUserInterface(_node_ipv6addr, _common_name, _serial_num, msg_bytes);

		}



		private void ProcessCommandResponse(byte[] msg_bytes)
		{
			SendToUserInterface(_node_ipv6addr, _common_name, _serial_num, msg_bytes);
			// TODO: is the rest of teh code necessary for dc in beagle bone; to check?

			// int num_status = BitConverter.ToUInt16(msg_bytes, 4);
			// ushort data_index = 6;
			// StringBuilder sb = new StringBuilder();
			// for (int ii = 0; ii < num_status; ii++)
			// {
			// 	ushort cmd_id = BitConverter.ToUInt16(msg_bytes, data_index);
			// 	data_index += 2;
			// 	//sb.AppendFormat("[{0}] : {1},  ", id_name[cmd_id].Replace("CPB_", ""), msg_bytes[data_index]);
			// 	sb.AppendFormat("[{0}] : {1},  ", ServiceNode.GetCapabilityName(cmd_id), ServiceNode.GetStatusAsString(msg_bytes[data_index]));
			// 	data_index++;
			// }
			// this._props.ServiceNodeStatus = sb.ToString().Trim();
		}

		private void ProcessGenericResponse(byte[] msg_bytes)
		{
			SendToUserInterface(_node_ipv6addr, _common_name, _serial_num, msg_bytes);
		}
		
		
		#endregion // state_machine

		#region timer_funcs
		private void StartCounting(int seconds)
		{
			timer_count = 0;
			required_timer_count = (int)((seconds * 1000) / timer_interval);
			required_timer_count++;
			if (timer_started)
			{
				return;
			}
			main_timer.Elapsed += this.OnTimerElapsed;
			timer_started = true;
		}

		private void StopCounting()
		{
			main_timer.Elapsed -= this.OnTimerElapsed;
			timer_started = false;
		}

		private void OnTimerElapsed(object source, ElapsedEventArgs e)
		{
			timer_count++;
			if (timer_count >= required_timer_count)
			{
				StopCounting();
				//RollingFileLogger.Debug("SM: On Timer elapsed State machine");
				Task.Run(
					() => CheckExecuteStateMachine(false)
				);
			}
		}

		#endregion //timer_funcs

		#region interface_funcs
		public void Shutdown()
		{
			confirmation_recv_event.Set();
			resp_recv_event.Set();

			shutdownRequest = true;
			//RollingFileLogger.Information("SM: Shutdown req State machine");

			StateMachine();
			msg_recv.Cancel();
			if (this.Props != null)
			{
				this.Props.ServiceNodeStatus = string.Format("Shutdown for node {0} called at {1} \n Cannot perform any more communications.",
					_node_ipv6addr_str,
					DateTime.Now.ToShortTimeString());
			}

		}

		public void PutMessage(object[] msg_obj)
		{
			Task.Run(
				() =>
				{
					//RollingFileLogger.Debug("in Put message: recv msg_obj");
					ProcessReceivedMessage(msg_obj);
				}
			);
		}

		public void SendDetachMessage()
		{
			_host_messaging.SendDetachRequest(_extended_addr);
		}

		//private void messageReceived()
		//{
		//    object [] got_msg = _node_msg.Take(msg_recv.Token);
		//    //return got_msg;
		//    Task.Run(
		//        () =>
		//        {
		//            ProcessReceivedMessage(got_msg);
		//        }
		//    );

		//}

		private bool TransmitDataToNode(byte[] pkt, byte[] _node_ipv6addr, bool isUDP)
		{
			transmit_id = BitConverter.ToUInt16(pkt, 2+2);
			DCLogger.Information(string.Format("tx id {0}", transmit_id));
			//return _host_messaging.SendDataToNode(pkt, _node_ipv6addr, true);			
			return _host_messaging.SendDataToNode(pkt, HostApp.CoordinatorIPv6Address, _node_ipv6addr, true);
		}

		#endregion // interface_funcs

		#region Inotifypropertychanged impl
		//private void NotifyPropertyChanged(String info)
		//{
		//    if (PropertyChanged != null)
		//    {
		//        PropertyChanged(this, new PropertyChangedEventArgs(info));
		//    }
		//}
		#endregion // Inotifypropertychanged impl

		#region update_funcs

		public bool SetSchedule(DateTime[] sch_times, byte[] sch_intensity_pc)
		{
			// assumed that the data in sch_times is all ordered. this simply sends the data to the node
			string tmpstr;
			if (_current_state >= (ushort)ServiceNodeStates.CAPABILITY)
			{
				// schedule can be sent
				// convert schedule to an array
				if (sch_times.Length != sch_intensity_pc.Length)
				{
					return false;
				}
				else
				{
					byte[] sch_row_bytes = new byte[sch_times.Length * 7];
					int jj = 0;
					for (int ii = 0; ii < sch_times.Length; ii++)
					{
						tmpstr = sch_times[ii].ToString("HH:mm:ss").Replace(":", "");
						byte[] tmpb = ASCIIEncoding.ASCII.GetBytes(tmpstr);
						Array.Copy(tmpb, 0, sch_row_bytes, jj, tmpb.Length);
						jj += 6;
						sch_row_bytes[jj] = sch_intensity_pc[ii];
						jj += 1;
					}
					ushort pkt_len = ProtocolMessagesG3PLC.ScheduleCommandPacketLen((ushort)sch_times.Length);
					byte[] pkt = new byte[pkt_len];
					ProtocolMessagesG3PLC.ScheduleCommandPacket(pkt, pkt_len, sch_row_bytes, (ushort)sch_row_bytes.Length, (ushort)sch_times.Length);
					byte[] combo_pkt = new byte[pkt_len + 2];
					ushort combo_pkt_len = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, (ushort)(pkt_len+2), false, pkt, (ushort)pkt_len);
					TransmitDataToNode(pkt, _node_ipv6addr, true);
					return true;
				}
			}
			else
			{
				return false;
			}
		}

		private void LoadDevicesFromJsonFile()
        {
			string node_cmd_filename;
			bool product_dev;
			if (AppOptions.GetAppOptions.ProductNames.Contains(_common_name))
            {
				node_cmd_filename = _common_name + ".json";
				product_dev = true;
            }
			else
            {
				node_cmd_filename = _common_name + "_" + _serial_num + ".json";
				product_dev = false;
			}
			string node_fname = System.IO.Path.Combine("NodesInfo", node_cmd_filename);
			if (product_dev)
            {
				DCLogger.Information($"Loading product -> {_common_name} with serial num -> {_serial_num}, devices from {node_fname}");
			}
			else
            {
				DCLogger.Information($"Loading devices for node -> {node_fname}");
			}
			if (System.IO.File.Exists(node_fname))
			{
				string json_str = System.IO.File.ReadAllText(node_fname);
				try
				{
					NodeDevices ndevs = JsonSerializer.Deserialize<NodeDevices>(json_str);
					//NodeDevices ndevs = JsonConvert.DeserializeObject<NodeDevices>(json_str);
					if ((ndevs.computedData != null) && (ndevs.computedData.compute != null) && (ndevs.computedData.compute.Length > 0))
					{
						ndevs.computedData.compute_values = JsonUtils.LoadMethodFromAssembly("NodesInfo", ndevs.computedData.name, ndevs.computedData.compute);
                    }
					node_devices = ndevs;
				}
				catch (System.Exception e)
				{
					DCLogger.Error(e, $"reading node file {node_fname}");
					node_devices = null;
				}
			}
			else
			{
				DCLogger.Error("node json not found");
				node_devices = null;
			}

		}

		public int NumberOfCommands()
		{
			//RRCommand rr_cmd;

			if (node_devices == null )
			{
				LoadDevicesFromJsonFile();
			}
			//else
			//{
			//	// TODO: has it changed? restart app (to be done at a higher layer; when an update was received)
			//}
			node_recvd_data.Clear();
			data_name_map.Clear();
			if ( node_devices != null ) 
			{
				if (rr_cmds_list.Count == 0)
                {
					foreach (DeviceInfo di in node_devices.devices)
					{
						if (di.config_command.Length != 0)
						{
                            RRCommand tmp_rr_cmd = new RRCommand
                            {
                                uuid = di.uuid,
                                device_name = di.device,
                                command_name = di.config_command,
                                command_type = "config",
                                channel = di.channel
                            };
                            config_cmd_list.Add(tmp_rr_cmd);
						}

						if (di.init_command.Length != 0)
						{
                            RRCommand tmp_rr_cmd = new RRCommand
                            {
                                uuid = di.uuid,
                                device_name = di.device,
                                command_name = di.init_command,
                                command_type = "init"
                            };
                            init_cmd_list.Add(tmp_rr_cmd);
						}

						// ignore order for now
						foreach (DataCommand dc in di.data_commands)
						{
                            RRCommand tmp_rr_cmd = new RRCommand
                            {
                                uuid = di.uuid,
                                device_name = di.device,
                                command_name = dc.name,
                                command_type = "data",
								channel = di.channel,
								data_names = dc.data_names
							};
                            string hw_interface = DeviceData.GetHWInterfaceForDevice(di.device);
							switch (hw_interface.ToLower())
                            {
								case "rs232":
									rs232_cmd_list.Add(tmp_rr_cmd);
									break;
								case "rs485":
									rs485_cmd_list.Add(tmp_rr_cmd);
									break;
								case "dali":
									dali_cmd_list.Add(tmp_rr_cmd);
									break;
								case "powermeasurement":
									powermeasurement_cmd_list.Add(tmp_rr_cmd);
									break;
								case "analog":
									analog_cmd_list.Add(tmp_rr_cmd);
									break;
								case "digital":
									digital_cmd_list.Add(tmp_rr_cmd);
									break;
								case "i2c":
									i2c_cmd_list.Add(tmp_rr_cmd);
									break;
								default:
									DCLogger.Information($"unknown hw itnerfadce type {hw_interface}. Unable to process");
									break;
                            }
						}
					}
					config_sent = false;
					init_sent = false;
					if (config_cmd_list.Count > 0)
                    {
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_CONFIG);
					}
					if (init_cmd_list.Count > 0)
                    {
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_INIT);
					}
					if (rs232_cmd_list.Count > 0)
                    {
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_RS232);
					}
					if (rs485_cmd_list.Count > 0)
					{
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_RS485);
					}
					if (dali_cmd_list.Count > 0)
					{
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_DALI);
					}
					if (analog_cmd_list.Count > 0)
					{
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_ANALOG);
					}
					if (digital_cmd_list.Count > 0)
					{
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_DIGITAL);
					}
					if (powermeasurement_cmd_list.Count > 0)
					{
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_POWERMEASUREMENT);
					}
					if (i2c_cmd_list.Count > 0)
                    {
						rr_cmds_list.Add((int)RRGetCommandTypes.RR_CMD_I2C);
                    }
				}
                else
                {
					if ( (config_sent) && (config_cmd_list.Count > 0) )
                    {
						rr_cmds_list.Remove((int)RRGetCommandTypes.RR_CMD_CONFIG);
                    }
					if ((init_sent) && (init_cmd_list.Count > 0))
					{
						rr_cmds_list.Remove((int)RRGetCommandTypes.RR_CMD_INIT);
					}
				}
				return rr_cmds_list.Count;
			}
			else
			{
				return 0;
			}
		}

		public void SendCommandWithTimeout(int command_index)
		{
			if (node_devices != null)
			{
				curr_rr_cmd_index = command_index;
				SendDCRequestEventWithTimeout(command_index);
			}
		}

		public bool SendDCRequestEventWithTimeout( int cmd_list_index)
		{
			bool sts;
			ushort pkt_len = 0;
			byte[] pkt = null;
			byte[] combo_pkt = null;
			List<RRCommand> loc_rr_cmd_list = null;
			switch (rr_cmds_list[cmd_list_index])
            {
				case (int)RRGetCommandTypes.RR_CMD_CONFIG:
					loc_rr_cmd_list = config_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_INIT:
					loc_rr_cmd_list = init_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_RS232:
					loc_rr_cmd_list = rs232_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_RS485:
					loc_rr_cmd_list = rs485_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_DALI:
					loc_rr_cmd_list = dali_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_ANALOG:
					loc_rr_cmd_list = analog_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_DIGITAL:
					loc_rr_cmd_list = digital_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_POWERMEASUREMENT:
					loc_rr_cmd_list = powermeasurement_cmd_list;
					break;
				case (int)RRGetCommandTypes.RR_CMD_I2C:
					loc_rr_cmd_list = i2c_cmd_list;
					break;
				default:
					DCLogger.Information($"unknown command enum {rr_cmds_list[cmd_list_index]}");
					break;
			}
			if (loc_rr_cmd_list == null)
            {
				DCLogger.Information($"Failed to parse command list for {cmd_list_index}");
				return false;
            }

			if (loc_rr_cmd_list.Count == 0)
            {
				DCLogger.Information($"List for {Enum.GetName(typeof(RRGetCommandTypes), rr_cmds_list[cmd_list_index])} is empty");
				return false;
			}

			// it is adequate to get hw interface from one item in list, all the others are the same
			string hw_interface_type = DeviceData.GetHWInterfaceForDevice(loc_rr_cmd_list[0].device_name);
			pkt = new byte[1024];
			if (hw_interface_type.ToLower().Equals("rs232"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.RS232_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					// possible types: config, init, data
					byte[] cmd_data = GetRs232Rs485CommandData(send_json_cmd);
					// TODO: is any error checking on address required
					byte address = DeviceData.GetAddress(rr_cmd.device_name);
					byte cmd_type_b = 0;
					if (rr_cmd.command_type.ToLower().Equals("config"))
					{
						cmd_type_b = 0;
					}
					else
					{
						cmd_type_b = 1;
					}
					pkt_len = ProtocolMessagesG3PLC.AddToPacketSerialCommand(ProtocolMessagesG3PLC.RS232_COMMAND_ID, ref pkt, pkt.Length,
						rr_cmd.channel, cmd_type_b, address, cmd_data, cmd_data.Length);
					if (pkt_len == 0xFFFF)
                    {
						DCLogger.Error($"RS232: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
                    }
				}
			}
			else if (hw_interface_type.ToLower().Equals("rs485"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.RS485_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					// possible types: config, init, data
					byte[] cmd_data = GetRs232Rs485CommandData(send_json_cmd);
					// TODO: is any error checking on address required
					byte address = DeviceData.GetAddress(rr_cmd.device_name);
					byte cmd_type_b = 0;
					if (rr_cmd.command_type.ToLower().Equals("config"))
					{
						cmd_type_b = 0;
					}
					else
					{
						cmd_type_b = 1;
					}
					pkt_len = ProtocolMessagesG3PLC.AddToPacketSerialCommand(ProtocolMessagesG3PLC.RS485_COMMAND_ID, ref pkt, pkt.Length,
						rr_cmd.channel, cmd_type_b, address, cmd_data, cmd_data.Length);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"RS485: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("powermeasurement"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.POWER_MEASUREMENT_REQUEST_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
				{
					//JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					pkt_len = ProtocolMessagesG3PLC.AddToPacketDigitalAnalogPowerMeasRequest(ProtocolMessagesG3PLC.POWER_MEASUREMENT_REQUEST_ID, ref pkt, pkt.Length,
						rr_cmd.channel);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"PowerMeasurement: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("dali"))
			{
				DCLogger.Information($"Unimplemented HW interface [{hw_interface_type}] found. Contact developer for more information.");
			}
			else if (hw_interface_type.ToLower().Equals("analog"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.ANALOG_INPUT_REQUEST_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
                {
					JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					pkt_len = ProtocolMessagesG3PLC.AddToPacketDigitalAnalogPowerMeasRequest(ProtocolMessagesG3PLC.ANALOG_INPUT_REQUEST_ID, ref pkt, pkt.Length,
						rr_cmd.channel);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"Analog input: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("digital"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.DIGITAL_INPUT_REQUEST_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					pkt_len = ProtocolMessagesG3PLC.AddToPacketDigitalAnalogPowerMeasRequest(ProtocolMessagesG3PLC.DIGITAL_INPUT_REQUEST_ID, ref pkt, pkt.Length,
						rr_cmd.channel);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"Digital input: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("i2c"))
            {
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.I2C_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var rr_cmd in loc_rr_cmd_list)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(rr_cmd.device_name, rr_cmd.command_name);
					// possible types: config, init, data
					byte[] cmd_data = GetRs232Rs485CommandData(send_json_cmd);
					// TODO: is any error checking on address required
					byte address = DeviceData.GetAddress(rr_cmd.device_name);
					byte cmd_type_b = 0;
					if (rr_cmd.command_type.ToLower().Equals("config"))
					{
						cmd_type_b = 0;
					}
					else
					{
						cmd_type_b = 1;
					}
					pkt_len = ProtocolMessagesG3PLC.AddToPacketSerialCommand(ProtocolMessagesG3PLC.I2C_COMMAND_ID, ref pkt, pkt.Length,
						rr_cmd.channel, cmd_type_b, address, cmd_data, cmd_data.Length);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"i2c: Unable to add pkt from {rr_cmd.device_name} : {rr_cmd.command_name}");
						return false;
					}
				}
			}
			else
			{
				DCLogger.Information($"Unsupported HW interface [{hw_interface_type}] found. Contact developer for more information.");
			}
			combo_pkt = new byte[pkt_len + 2];
			combo_pkt[0] = (byte)(pkt_len + 2);
			combo_pkt[1] = (byte)((pkt_len + 2) >> 8);
			ProtocolMessagesG3PLC.AppendPacket(combo_pkt, (ushort)(pkt_len + 2), false, pkt, pkt_len);

            if (pkt_len == 0)
			{
				DCLogger.Error("Packet length is 0. Should not happen consult developer.");
				return false; // cannot do any furhter processing => probably invalid command
			}
			_data_cmd_time = DateTime.Now;
			TransmitDataToNode(combo_pkt, _node_ipv6addr, true);
			DCLogger.Verbose("callling recv autoreset reset and waitone 3000 ");
			confirmation_recv_event.Reset();
			sts = confirmation_recv_event.WaitOne(rd_confirmation_timeout_millisecs);
			if (sts)
			{
				DCLogger.Information("Confirmation received. Wait for data request response");
				resp_recv_event.Reset();
				sts = resp_recv_event.WaitOne(rd_rx_timeout_milliseconds);
			}
			else 
			{
				DCLogger.Information(String.Format("Confirmation NOT received in {0} seconds", rd_confirmation_timeout_millisecs/ 1000));
			}
			return sts;
		}



		public byte[] GetRs232Rs485CommandData(JsonCommand send_json_cmd)
		{
			byte[] data_2_send = null;
			if (send_json_cmd.type.Equals("string[]", StringComparison.CurrentCultureIgnoreCase))
			{
				if (send_json_cmd.name.ToLower().Equals("config"))
				{
					// parse the send command
					// format "<speed>, <num bits>, <parity values: N [None], O[Odd], E[Even], <num stop bits>, delimiter byte, 0/1 (buffer length to wait if delimiter is 0x00)"
					//For Parity N => 0,  E => 1, O => 2
					string[] data_str = send_json_cmd.value.Split(',');
					if (data_str.Length != 6)
					{
						DCLogger.Information(string.Format("expected config format \"baud rate, num bits, parity(N/E/O), stopbits,delimiter char as int, 0/1 (disable/enable SW flow control xon_xoff\", got {0}", 
							send_json_cmd.value));
						DCLogger.Information(string.Format("Cannot parse and send config commands"));
						return null;
					}
					data_2_send = new byte[9];
					// config uint8 =0, address_uint8, 
					// Baud_uint32, parity_uint8, { 0 = none, 1 = even, 2 = odd }, 
					//     numberOfBits_uint8, numberOfStopBits_uint8, delimiter _uint8 { 0x10 or 0x13 typically}
					//data_2_send[0] = 0; // it is config data
					//data_2_send[1] = 0; // address_uint8
					byte[] tmpb = BitConverter.GetBytes( (Convert.ToUInt32(data_str[0])) );
					data_2_send[0] = tmpb[0];
					data_2_send[1] = tmpb[1];
					data_2_send[2] = tmpb[2];
					data_2_send[3] = tmpb[3];
					if (data_str[1].ToLower().StartsWith("n")) // parity
					{
						data_2_send[4] = 0;
					}
					else if (data_str[1].ToLower().StartsWith("e"))
					{
						data_2_send[4] = 1;
					}
					else if (data_str[1].ToLower().StartsWith("o"))
					{
						data_2_send[4] = 2;
					}
					else
					{
						// invalid parity setting
						DCLogger.Information("Invalid parity setting, expected N,E or O");
						return null;
					}
					data_2_send[5] = Convert.ToByte(data_str[2]); // num bits
					data_2_send[6] = Convert.ToByte(data_str[3]); // num stop bits
					data_2_send[7] = Convert.ToByte(data_str[4]); // delimiter
					data_2_send[8] = Convert.ToByte(data_str[5]); // enable / disable software flow control
				}
				else
				{
					DCLogger.Information($"Converting string[] {send_json_cmd.value} as ASCII byte array");
					//byte[] tmpb = System.Text.Encoding.ASCII.GetBytes(dev_rs232_cmds.commands[index].send_command);
					MemoryStream ms = new MemoryStream();
					foreach (char tmpch in send_json_cmd.value)
					{
						ms.WriteByte((byte)tmpch);
					}
					byte[] tmpb = ms.ToArray();
					data_2_send = new byte[tmpb.Length + 2];
					data_2_send[0] = 1; // 0=> config, 1 => command
					data_2_send[1] = 0; // addressa is always zero for rs232 commands
					System.Array.Copy(tmpb, 0, data_2_send, 2, tmpb.Length);
				}
			}
			else if (send_json_cmd.type.ToLower().Equals("string"))
            {
				DCLogger.Information($"Converting string {send_json_cmd.value} as ASCII byte array");
				//byte[] tmpb = System.Text.Encoding.ASCII.GetBytes(dev_rs232_cmds.commands[index].send_command);
				MemoryStream ms = new MemoryStream();
				foreach (char tmpch in send_json_cmd.value)
				{
					ms.WriteByte((byte)tmpch);
				}
				//byte[] tmpb = ms.ToArray();
				data_2_send = ms.ToArray();
				//data_2_send = new byte[tmpb.Length + 2];
				//data_2_send[0] = 1; // 0=> config, 1 => command
				//data_2_send[1] = 0; // addressa is always zero for rs232 commands
				//System.Array.Copy(tmpb, 0, data_2_send, 2, tmpb.Length);
			}
			else if (send_json_cmd.type.Equals("byte[]", StringComparison.CurrentCultureIgnoreCase))
			{
				data_2_send = ParseBinaryCommand(send_json_cmd.value);
				//data_2_send = new byte[tmpb.Length + 2];
				//data_2_send[0] = 1; // 0=> config, 1 => command
				//data_2_send[1] = 0; // addressa is always zero for rs232 commands
				//System.Array.Copy(tmpb, 0, data_2_send, 2, tmpb.Length);
			}
			else if (send_json_cmd.type.Equals("byte", StringComparison.CurrentCultureIgnoreCase))
            {
				data_2_send = ParseBinaryCommand(send_json_cmd.value);

            }
			return data_2_send;
		}

        private byte[] ParseBinaryCommand(string bin_cmd_str)
        {
            byte[] to_ret = null;
            int ii;
            bool parse_sts;
            byte tmpb;
            string[] bytes_str = bin_cmd_str.Split(new char[] { ',' });
            parse_sts = true;
            if (bytes_str.Length > 0)
            {
                to_ret = new byte[bytes_str.Length];
                ii = 0;
                parse_sts = true;
                while ((ii < bytes_str.Length) && (parse_sts))
                {
                    if ((bytes_str[ii].Trim().StartsWith("0x")) ||
                         (bytes_str[ii].Trim().StartsWith("0X"))
                        )
                    {
                        parse_sts = byte.TryParse(bytes_str[ii].Trim()[2..], System.Globalization.NumberStyles.HexNumber, null, out tmpb);
                        if (parse_sts)
                        {
                            to_ret[ii] = tmpb;
                        }
                    }
                    else
                    {
                        parse_sts = byte.TryParse(bytes_str[ii].Trim(), out tmpb);
                        if (parse_sts)
                        {
                            to_ret[ii] = tmpb;
                        }
                    }
                    ii++;
                }
            }
            if (parse_sts)
            {
                return to_ret;
            }
            else
            {
                return null;
            }
        }

		public bool SendExtCommandWithTimeout(CommandForNode cmd_for_node)
        {
			bool sts;
			ushort pkt_len = 0;
			byte[] pkt = null;
			byte[] combo_pkt = null;
			if (node_devices == null)
            {
				LoadDevicesFromJsonFile();
			}
			if (node_devices == null)
            {
				DCLogger.Error($"Failed to load node json from {_common_name}_{_serial_num}.json");
				return false;
            }
			// locate current device name
			DeviceInfo dev_info = Array.Find<DeviceInfo>(node_devices.devices, x => x.uuid == cmd_for_node.UUID);
			if (dev_info == null)
            {
				DCLogger.Error($"Failed to locate device with uuid {cmd_for_node.UUID} in Node {cmd_for_node.Node}");
				return false;
            }

			// it is adequate to get hw interface from one item in list, all the others are the same
			string hw_interface_type = DeviceData.GetHWInterfaceForDevice(dev_info.device);
			pkt = new byte[1024];
			if (hw_interface_type.ToLower().Equals("rs232"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.RS232_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var ext_cmd in cmd_for_node.Commands)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(dev_info.device, ext_cmd.Name);
					byte[] cmd_data = GetRs232Rs485CommandData(send_json_cmd);
					// TODO: is any error checking on address required
					byte address = DeviceData.GetAddress(dev_info.device);
					byte cmd_type_b = 1; // will not accept config commands here 1 => command
					
					pkt_len = ProtocolMessagesG3PLC.AddToPacketSerialCommand(ProtocolMessagesG3PLC.RS232_COMMAND_ID, ref pkt, pkt.Length,
						dev_info.channel, cmd_type_b, address, cmd_data, cmd_data.Length);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"RS232: Unable to add pkt from {dev_info.device} : {ext_cmd.Name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("rs485"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.RS485_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var ext_cmd in cmd_for_node.Commands)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(dev_info.device, ext_cmd.Name);
					byte[] cmd_data = GetRs232Rs485CommandData(send_json_cmd);
					// TODO: is any error checking on address required
					byte address = DeviceData.GetAddress(dev_info.device);
					byte cmd_type_b = 1; // will not accept config commands here 1 => command

					pkt_len = ProtocolMessagesG3PLC.AddToPacketSerialCommand(ProtocolMessagesG3PLC.RS485_COMMAND_ID, ref pkt, pkt.Length,
						dev_info.channel, cmd_type_b, address, cmd_data, cmd_data.Length);
					if (pkt_len == 0xFFFF)
					{
						DCLogger.Error($"RS232: Unable to add pkt from {dev_info.device} : {ext_cmd.Name}");
						return false;
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("powermeasurement"))
			{
				DCLogger.Information($"Unimplemented HW interface [{hw_interface_type}] found. Contact developer for more information.");
			}
			else if (hw_interface_type.ToLower().Equals("dali"))
			{
				DCLogger.Information($"Unimplemented HW interface [{hw_interface_type}] found. Contact developer for more information.");
			}
			else if (hw_interface_type.ToLower().Equals("analog"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.ANALOG_OUTPUT_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var ext_cmd in cmd_for_node.Commands)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(dev_info.device, ext_cmd.Name);
					float conv_float;
					sts = float.TryParse(ext_cmd.Value, out conv_float);
					if (sts)
                    {
						pkt_len = ProtocolMessagesG3PLC.AddToPacketAnalogOutputCommand(ref pkt, pkt.Length, dev_info.channel, conv_float);
						if (pkt_len == 0xFFFF)
						{
							DCLogger.Error($"Analog out: Unable to add pkt from {dev_info.device} : {ext_cmd.Name}, value => {ext_cmd.Value}");
							return false;
						}
					}
				}
			}
			else if (hw_interface_type.ToLower().Equals("digital"))
			{
				pkt_len = ProtocolMessagesG3PLC.GeneratePacketBase(ProtocolMessagesG3PLC.DIGITAL_OUTPUT_COMMAND_ID, ref pkt, pkt.Length);
				foreach (var ext_cmd in cmd_for_node.Commands)
				{
					JsonCommand send_json_cmd = DeviceData.GetCommand(dev_info.device, ext_cmd.Name);
					int conv_int;
					sts = int.TryParse(ext_cmd.Value, out conv_int);
					if (conv_int != 0)
                    {
						conv_int = 1;
                    }
					if (sts)
					{
						pkt_len = ProtocolMessagesG3PLC.AddToPacketDigitalOutputCommand(ref pkt, pkt.Length, dev_info.channel, (byte)conv_int);
						if (pkt_len == 0xFFFF)
						{
							DCLogger.Error($"Analog out: Unable to add pkt from {dev_info.device} : {ext_cmd.Name}, value => {ext_cmd.Value}");
							return false;
						}
					}
				}
			}
			else
			{
				DCLogger.Information($"Unsupported HW interface [{hw_interface_type}] found. Contact developer for more information.");
			}
			combo_pkt = new byte[pkt_len + 2];
			combo_pkt[0] = (byte)(pkt_len + 2);
			combo_pkt[1] = (byte)((pkt_len + 2) >> 8);
			ProtocolMessagesG3PLC.AppendPacket(combo_pkt, (ushort)(pkt_len + 2), false, pkt, pkt_len);

			if (pkt_len == 0)
			{
				DCLogger.Error("Packet length is 0. Should not happen consult developer.");
				return false; // cannot do any furhter processing => probably invalid command
			}
			cmd_event_id = cmd_for_node.EventId;
			_data_cmd_time = DateTime.Now;
			TransmitDataToNode(combo_pkt, _node_ipv6addr, true);
			//RollingFileLogger.Verbose("callling recv autoreset reset and waitone 3000 ");
			confirmation_recv_event.Reset();
			sts = confirmation_recv_event.WaitOne(rd_confirmation_timeout_millisecs);
			if (sts)
			{
				DCLogger.Information("Confirmation received. Wait for data request response");
				resp_recv_event.Reset();
				sts = resp_recv_event.WaitOne(rd_rx_timeout_milliseconds);
			}
			else
			{
				DCLogger.Information(String.Format("Confirmation NOT received in {0} seconds", rd_confirmation_timeout_millisecs / 1000));
			}
			return sts;
		}
		public void ProcessCommandFromDataService(CommandForNode cmd_for_node)
        {
			SendExtCommandWithTimeout(cmd_for_node);
        }



		public bool SendProtocolSetupRequestEventWithTimeout()
		{
			bool sts;
			if (_pause_state_machine == true)
			{
				_pause_state_machine = false;
			}
			if (_current_state != (ushort)ServiceNodeStates.PROTOCOL_SETUP)
			{
				_current_state = (ushort)ServiceNodeStates.PROTOCOL_SETUP;
			}

			if (receivedProtocolSetupResponse)
            {
				return true;
            }

			if ( (receivedCommonNameResponse) && (receivedSyncTimeResponse) )
            {
				return SendProtocolSetupConfirmEventWithTimeout();
            }

			ushort pkt_len1 = ProtocolMessagesG3PLC.CommonNameRequestPacketLen();
			ushort pkt_len2 = ProtocolMessagesG3PLC.SyncTimeRequestPacketLen();
			//ushort pkt_len3 = ProtocolMessagesG3PLC.CapabilityRequestPacketLen();
			//ushort combo_pkt_len = (ushort)(pkt_len1 + pkt_len2 + pkt_len3+2);
			ushort combo_pkt_len = (ushort)(pkt_len1 + pkt_len2 + 2);
			byte[] combo_pkt = new byte[combo_pkt_len];

			byte[] pkt1 = new byte[pkt_len1];
			ProtocolMessagesG3PLC.CommonNameRequestPacket(pkt1, pkt_len1);
			ushort tmpu2 = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, combo_pkt_len, false, pkt1, pkt_len1);

			byte[] pkt2 = new byte[pkt_len2];
			byte[] time_b;
			StringBuilder sb = new StringBuilder();
			DateTime curr_time = DateTime.Now;
			sb.Append(string.Format("{0:d2}{1:d2}{2:d2}", curr_time.Month, curr_time.Day, curr_time.Year - 2000));
			sb.Append(string.Format("{0:d2}{1:d2}{2:d2}", curr_time.Hour, curr_time.Minute, curr_time.Second));
			sb.Append(string.Format("{0:d2}", (int)curr_time.DayOfWeek ) );
			time_b = ASCIIEncoding.ASCII.GetBytes(sb.ToString());
			ProtocolMessagesG3PLC.SyncTimeRequestPacket(pkt2, pkt_len2, time_b, (ushort)time_b.Length, (uint)rd_rrtask_sleep_secs);
			tmpu2 = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, combo_pkt_len, true, pkt2, pkt_len2);

			//byte[] pkt3 = new byte[pkt_len3];
			//ProtocolMessagesG3PLC.CapabilityRequestPacket(pkt3, pkt_len3);
			//tmpu2 = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, combo_pkt_len, true, pkt3, pkt_len3);
			_data_cmd_time = DateTime.Now; // we get a data response with proto setup too; so it could also be considered a command

			TransmitDataToNode(combo_pkt, _node_ipv6addr, true);
			//RollingFileLogger.Verbose("callling recv autoreset reset and waitone 3000 ");
			confirmation_recv_event.Reset();
			sts = confirmation_recv_event.WaitOne(rd_confirmation_timeout_millisecs);
			if (sts)
			{
				DCLogger.Information("Confirmation received. Wait for Protocol request response");
				resp_recv_event.Reset();
				sts = resp_recv_event.WaitOne(rd_rx_timeout_milliseconds);
			}
			else 
			{
				DCLogger.Information(String.Format("Confirmation NOT received in {0} seconds.", rd_confirmation_timeout_millisecs/1000));
			}
			return sts;
		}


		public bool SendProtocolSetupConfirmEventWithTimeout()
		{
			bool sts;
			if (_pause_state_machine == true)
			{
				_pause_state_machine = false;
			}
			if (_current_state != (ushort)ServiceNodeStates.PROTOCOL_SETUP)
			{
				_current_state = (ushort)ServiceNodeStates.PROTOCOL_SETUP;
			}
			ushort pkt_len1 = ProtocolMessagesG3PLC.ProtocolConfirmCommandPacketLen();
			ushort combo_pkt_len = (ushort)(pkt_len1 + 2);
			byte[] combo_pkt = new byte[combo_pkt_len];

			byte[] pkt1 = new byte[pkt_len1];
			ProtocolMessagesG3PLC.ProtocolConfirmCommandPacket(pkt1, pkt_len1);
			ushort tmpu2 = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, combo_pkt_len, false, pkt1, pkt_len1);

			_data_cmd_time = DateTime.Now; // we get a data response with proto setup too; so it could also be considered a command

			TransmitDataToNode(combo_pkt, _node_ipv6addr, true);
			//RollingFileLogger.Information("callling recv autoreset reset and waitone 3000 ");
			confirmation_recv_event.Reset();
			sts = confirmation_recv_event.WaitOne(rd_confirmation_timeout_millisecs);
			if (sts)
			{
				DCLogger.Information("Confirmation received. Wait for Protocol confirm response");
				resp_recv_event.Reset();
				sts = resp_recv_event.WaitOne(rd_rx_timeout_milliseconds);
			}
			else
			{
				DCLogger.Information(String.Format("Confirmation NOT received in {0} seconds.", rd_confirmation_timeout_millisecs / 1000));
			}
			return sts;
		}



		#endregion // update_funcs

		#region ui_funcs
		private void SendToUserInterface(byte[] ipv6_address, string common_name, string serial_num, byte[] data_to_send)
		{
			if (_itransmit_comms == null)
			{
				// cannot send any data to ui; display in console
				DCLogger.Information(String.Format("ipv6 [{0}], name -> {1}, data ->{2}",
					_node_ipv6addr_str,
					common_name + ";" + serial_num,
				                                BitConverter.ToString(data_to_send, 0, data_to_send.Length)));
				return;
			}

			//RollingFileLogger.Information("in SentToUserInterface function");
			// data format: ipv6_address bytes, length => 2 bytes
			//              common_name + ";" + serial_num (corresponding to length bytes)
			//              length of data => 2 bytes, data (as per length specified in the prev 2 bytes)
			try
			{
				int tmp_b_len = 2 + ASCIIEncoding.ASCII.GetByteCount(common_name) + // note 2 is for length of common_name and serial_number that follows
								ASCIIEncoding.ASCII.GetByteCount(serial_num) + 1;// note: 1 is for ";" that separates common name and serial number
				int data_b_len = ipv6_address.Length + tmp_b_len + data_to_send.Length; 
				byte[] data_b = new byte[data_b_len];
				System.Array.Copy(ipv6_address, 0, data_b, 0, ipv6_address.Length);
				DCLogger.Verbose("Finished copy ipv6 address");
				data_b[ipv6_address.Length] = (byte)(tmp_b_len & 0xFF);
				data_b[ipv6_address.Length + 1] = (byte)(tmp_b_len >> 8);
				byte[] tmp_b = new byte[tmp_b_len];
				tmp_b = ASCIIEncoding.ASCII.GetBytes(common_name + ';' + serial_num);
				System.Array.Copy(tmp_b, 0, data_b, ipv6_address.Length + 2, tmp_b_len-2);
				DCLogger.Verbose("Finished copy common name and serial num");
				//data_b[ipv6_address.Length + tmp_b_len] = (byte)(data_to_send.Length & 0xFF);
				//data_b[ipv6_address.Length + tmp_b_len + 1] = (byte)(data_to_send.Length >> 8);

				System.Array.Copy(data_to_send, 0, data_b, ipv6_address.Length + tmp_b_len, data_to_send.Length);
				DCLogger.Verbose("send data to UI ");
				_itransmit_comms.transmit(data_b);
			}
			catch (Exception e)
			{
				DCLogger.Error(e, "Send To User Interface");
				//RollingFileLogger.Error(e.StackTrace);
			}
 		}



		public void ReceivedFromUserInterface(byte[] data_recv)
		{
			// TODO: // add code here to process data
			// data recv contains ipv6 address; <2byte length>, common_name+ ";" + serialnumber, data_recv
			bool ipv6_verified;
			bool name_verified;
			int ii, jj;
			//if (data_recv.Length < _node_ipv6addr.Length)
			//{
			//	ipv6_verified = false;
			//}
			//else
			//{
			//	ii = 0;
			//	ipv6_verified = true;
			//	while ((ii < _node_ipv6addr.Length) && ipv6_verified == true)
			//	{
			//		if (_node_ipv6addr[ii] != data_recv[ii])
			//		{
			//			ipv6_verified = false;
			//		}
			//		ii++;
			//	}	
			//}
			ipv6_verified = true; // done at the main layer
			name_verified = false;
			//ii = 2 + ASCIIEncoding.ASCII.GetByteCount(_common_name) + 1 + ASCIIEncoding.ASCII.GetByteCount(_serial_num);
			ii = BitConverter.ToUInt16(data_recv, _node_ipv6addr.Length);
			string tmpstr = ASCIIEncoding.ASCII.GetString(data_recv, _node_ipv6addr.Length+2, ii-2);
			string[] tmp_parts = tmpstr.Split(new char[] {';'});
			if (ipv6_verified)
			{
				/*
				two formats of data can be recievedhere
				<ipv6 address><length: uint16><common name>;<serial number><Single SingleDataPacket>
						OR
				<ipv6 address><length: uint16><common name;serial number><BridgeNodeSingleDataPacket>
				*/
				if ( (tmp_parts[0].Equals(_common_name)) && (tmp_parts[1].Equals(_serial_num)) )
				{
					name_verified = true;
				}
				else
				{
					name_verified = false;
				}
			}

			DCLogger.Information(string.Format("ipv6 verified => {0}, name_verified = {1}", ipv6_verified, name_verified));
			if (ipv6_verified && name_verified)
			{
				jj = _node_ipv6addr.Length + ii; // + 4;
				byte[] tmp_recv_b = new byte[data_recv.Length - jj];
				System.Array.Copy(data_recv, jj, tmp_recv_b, 0, tmp_recv_b.Length);
				byte[]  combo_pkt = new byte[tmp_recv_b.Length + 2];
				byte[] tmp_b = BitConverter.GetBytes((ushort)(combo_pkt.Length));
				combo_pkt[0] = tmp_b[0];
				combo_pkt[1] = tmp_b[1];
				System.Array.Copy(tmp_recv_b, 0, combo_pkt, 2, tmp_recv_b.Length);
				//ushort combo_pkt_len = ProtocolMessagesG3PLC.AppendPacket(combo_pkt, (ushort)(tmp_recv_b.Length + 2), false, tmp_recv_b, (ushort)tmp_recv_b.Length);
				TransmitDataToNode(combo_pkt, _node_ipv6addr, true);
				confirmation_recv_event.Reset();
				confirmation_recv_event.WaitOne();
				DCLogger.Information("Confirmation received. Wait for data request response");

				resp_recv_event.Reset();
				resp_recv_event.WaitOne(rd_rx_timeout_milliseconds);

			}
		}

        #endregion //ui_funcs


        #region dataservice_funcs
		private void SentToDataServiceInterface(TransferData[] send_data)
        {
			if (_dc_dataservice_comms == null)
            {
				DCLogger.Information($"Unable to send {send_data.Length} items to data service as endpoint is null.");
				return;
            }
			foreach (var tmp_item in send_data)
            {
				string tmp_str = System.Text.Json.JsonSerializer.Serialize<TransferData>(tmp_item);
				_dc_dataservice_comms.TransmitToDataService(tmp_str);
            }
		}
        #endregion // dataservice_funcs

    }
}

