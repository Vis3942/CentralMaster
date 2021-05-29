using System;
using System.Text;
using LogUtils;

enum ATRESPONSESTATUS : byte
{
	OK = 0x00,
	ERROR = 0x01,
	INVALID_COMMAND = 0x02,
	INVALID_PARAM = 0x03
}


enum TRANSMITSTATUS : ushort
{
	SUCCESSFUL_TRANSMIT = 0x00,
	FAILED_CELL_NETWORK = 0x21,
	NOTREGISTERED_CELL_NETWORK = 0x22,
	INVALID_FRAME_VALUES = 0x2c,
	INTERNAL_ERROR = 0x31,
	RESOURCE_ERROR = 0x32,
	MESSAGE_TOO_LONG = 0x74,
	INVALID_UDP_PORT = 0x78,
	INVALID_TCP_PORT = 0x79,
	INVALID_HOST_ADDRESS = 0x7A,
	INVALID_DATA_MODE = 0x7B,
	CONNECTION_REFUSED = 0x80,
	SOCKET_CONNECTION_LOST = 0x81,
	NO_SERVER = 0x82,
	SOCKET_CLOSED = 0x83,
	UNKNOWN_SERVER = 0x84,
	UNKNOWN_ERROR = 0x85,
	APP_NO_ADDRESS = 0x0100,
	APP_NO_PORT = 0x0200,
	APP_NO_APIMODE = 0x0300,
	APP_TIMEOUT_TXSTS_RESPONSE = 0x0400
}

enum ModemStatus : byte
{
	HARDWARE_RESET = 0x00, // or powerup
	WATCHDOG_RESET = 0x01,
	REGISTERED_WITH_CELLULAR = 0x02,
	UNREGISTERED_WITH_CELLULAR = 0x03,
	DEVICE_CLOUD_CONNECTED = 0x0E,
	DEVICE_CLOUD_DISCONNECTED = 0x0F
}


namespace CentralMaster.Communications
{
	public class XbeeAPIFrames
	{

		public static byte command_id = 0;
		public static byte GetCurrentCommandID()
		{
			return command_id;
		}
		public static byte GetNextCommandID()
		{
			if (command_id == 0xFF)
			{
				command_id = 0;
			}
			else
			{
				command_id++;
			}
			return command_id;
		}


		public static byte[] tmp_buffer = new byte[2048];
		public static int tmp_buffer_length = 0; // used length

		public static byte ComputeChecksum(byte[] data, int start_index, int end_index)
		{
			byte chksum = 0;
			for (int ii = start_index; ii <= end_index; ii++)
			{
				chksum = (byte)(chksum + data[ii]);
			}
			return (byte)(0xFF - chksum);
		}

		public static bool VerifyChecksum(byte[] data_frame)
		{
			// the parameter to this function is the actual complete data frame rx'ed
			byte chksum = 0;
			for (int ii = 3; ii < data_frame.Length; ii++)
			{
				chksum = (byte)(chksum + data_frame[ii]);
			}
			if (((byte)chksum) == 0xFF)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public XbeeAPIFrames()
		{
		}
	}

	public class XbeeAPIHeader
	{
		public byte _frame_type;
		public byte _frame_id;
		public byte _checksum;
		public byte[] _bytes;
		public bool _byte_arr_computed = false;
		public bool _isQueued = false;

		public XbeeAPIHeader()
		{
			_checksum = 0;
			_byte_arr_computed = false;
		}

	}

	public class XbeeATCommandFrame : XbeeAPIHeader
	{
		public string _cmd;
		public string _param; // can be string.Empty

		public XbeeATCommandFrame(string cmd, string param, bool isQueued)
		{
			if (cmd.Length != 2)
			{
				throw new Exception(string.Format("Invalid command {0}, expected only 2 chars", cmd));
			}
			_checksum = 0;
			_byte_arr_computed = false;
			_cmd = cmd;
			_param = param;
			_frame_id = XbeeAPIFrames.GetNextCommandID();
			_isQueued = isQueued;
			if (_isQueued)
			{
				_frame_type = 0x09;
			}
			else
			{
				_frame_type = 0x08;
			}
		}

		public byte[] ComputeBytes()
		{
			if (_byte_arr_computed)
			{
				return _bytes;
			}
			else
			{
				int tmp_len;
				int ii;
				if (_param == string.Empty)
				{
					tmp_len = 1 + 2 + 1 + 1 + 2 + 1;
					// start delimiter + length (msb, lsb) + frame type + frame id + 2 char at command + checksum

				}
				else
				{
					tmp_len = 1 + 2 + 1 + 1 + 2 + System.Text.Encoding.ASCII.GetByteCount(_param) + 1;
					// start delimiter + length (msb, lsb) + frame type + frame id + 2 char at command param length + checksum
				}
				_bytes = new byte[tmp_len];
				ii = 0;
				_bytes[ii++] = 0x7E;
				_bytes[ii++] = (byte)(((tmp_len - 4) >> 8) & 0xFF); // msb
				_bytes[ii++] = (byte)((tmp_len - 4) & 0xFF); // lsb
				_bytes[ii++] = _frame_type;
				_bytes[ii++] = _frame_id;
				_bytes[ii++] = (byte)(_cmd[0]); // TODO: check value
				_bytes[ii++] = (byte)(_cmd[1]);
				if (_param != string.Empty)
				{
					byte[] tmp_arr = System.Text.Encoding.ASCII.GetBytes(_param);
					System.Array.Copy(tmp_arr, 0, _bytes, ii, tmp_arr.Length);
					ii += tmp_arr.Length;
				}
				_bytes[ii] = XbeeAPIFrames.ComputeChecksum(_bytes, 3, tmp_len - 2);
				DCLogger.Debug(string.Format("verify checksum comp {0} ", XbeeAPIFrames.VerifyChecksum(_bytes)));
				_byte_arr_computed = true;
				return _bytes;
			}
		}

		public override string ToString()
		{
			if (!_byte_arr_computed)
			{
				ComputeBytes();
			}
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			if (_isQueued)
			{
				sb.AppendFormat("AT Cmd Queue[{2}]: {0}, param = {1}\n", _cmd, _param, _frame_id);
			}
			else
			{
				sb.AppendFormat("AT Cmd [{2}]: {0}, param = {1}\n", _cmd, _param, _frame_id);
			}
			sb.AppendLine(BitConverter.ToString(_bytes).Replace("-", " "));
			return sb.ToString();
		}
	}

	public class XbeeTxIPv4Frame : XbeeAPIHeader
	{
		byte[] _to_addr;
		ushort _dest_port;
		ushort _src_port;
		byte[] _payload;
		byte _protocol; // 4 for ssl over tcp
		bool _close_socket;
		public XbeeTxIPv4Frame(byte[] to_addr, ushort dest_port, ushort src_port,
								   byte protocol, bool close_socket, byte[] payload)
		{
			_frame_type = 0x20;
			_frame_id = XbeeAPIFrames.GetNextCommandID();
			_to_addr = new byte[to_addr.Length];
			System.Array.Copy(to_addr, _to_addr, to_addr.Length);
			_dest_port = dest_port;
			_src_port = src_port;
			_protocol = protocol;
			_close_socket = close_socket;
			_payload = new byte[payload.Length];
			System.Array.Copy(payload, _payload, payload.Length);

		}

		public XbeeTxIPv4Frame(byte[] to_addr, ushort dest_port, ushort src_port,
							   byte protocol, bool close_socket, string payload_str) :
		this(to_addr, dest_port, src_port, protocol,
				 close_socket, System.Text.Encoding.ASCII.GetBytes(payload_str))
		{
		}

		public byte[] ComputeBytes()
		{
			if (_byte_arr_computed)
			{
				return _bytes;
			}
			else
			{
				int tmp_len;
				int ii;
				tmp_len = 1 + 2 + 1 + 1 + 4 + 2 + 2 + 1 + 1 + _payload.Length + 1;
				// start delimiter + length (msb, lsb) + frame type + frame id + dest addr 32-bit bigendian addr
				// dest port + source port + protocol + transmit options + payload len + checksum
				_bytes = new byte[tmp_len];
				ii = 0;
				_bytes[ii++] = 0x7E;
				_bytes[ii++] = (byte)(((tmp_len - 4) >> 8) & 0xFF); // msb
				_bytes[ii++] = (byte)((tmp_len - 4) & 0xFF); // lsb
				_bytes[ii++] = _frame_type;
				_bytes[ii++] = _frame_id;
				_bytes[ii++] = _to_addr[0];
				_bytes[ii++] = _to_addr[1];
				_bytes[ii++] = _to_addr[2];
				_bytes[ii++] = _to_addr[3];
				_bytes[ii++] = (byte)((_dest_port >> 8) & 0xFF); // msb
				_bytes[ii++] = (byte)(_dest_port & 0xFF); // lsb
				_bytes[ii++] = (byte)((_src_port >> 8) & 0xFF); // msb
				_bytes[ii++] = (byte)(_src_port & 0xFF); // lsb
				_bytes[ii++] = _protocol;
				_bytes[ii++] = 0; // transmit options : Bit 1: When set close TCP socket after transmit
				System.Array.Copy(_payload, 0, _bytes, ii, _payload.Length);
				ii += _payload.Length;
				_bytes[ii] = XbeeAPIFrames.ComputeChecksum(_bytes, 3, tmp_len - 2);
				DCLogger.Debug(string.Format("verify checksum comp {0} ", XbeeAPIFrames.VerifyChecksum(_bytes)));
				_byte_arr_computed = true;
				return _bytes;
			}
		}

		public override string ToString()
		{
			if (!_byte_arr_computed)
			{
				ComputeBytes();
			}
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("XbeeTxIPv4Frame: {0}.{1}.{2}.{3}: {4} \n",
							_to_addr[0], _to_addr[1], _to_addr[2], _to_addr[3], _dest_port);
			sb.AppendFormat(" Data => {0} \n", BitConverter.ToString(_payload));
			return sb.ToString();
		}

	}

	public class XbeeATCommandResponse : XbeeAPIHeader
	{
		public string _cmd;
		public byte[] _param;
		public byte _status;
		public bool _checksum_verify;

		public XbeeATCommandResponse(byte[] message_bytes)
		{
			FromBytes(message_bytes);
		}

		private void FromBytes(byte[] msg_bytes)
		{
			int ii;
			int tmp_len;
			_bytes = new byte[msg_bytes.Length];
			System.Array.Copy(msg_bytes, 0, _bytes, 0, msg_bytes.Length);
			tmp_len = ((msg_bytes[1] << 8) & (0xFF00));
			tmp_len += (msg_bytes[2]);
			ii = 3;
			_frame_type = msg_bytes[ii++];
			_frame_id = msg_bytes[ii++];
			_cmd = System.Text.Encoding.ASCII.GetString(msg_bytes, ii, 2);
			ii += 2;
			_status = msg_bytes[ii++];
			if (ii >= tmp_len + 3)
			{
				_param = null;
			}
			else
			{
				_param = new byte[msg_bytes.Length - ii - 1];
				System.Array.Copy(msg_bytes, ii, _param, 0, msg_bytes.Length - ii - 1);
				ii += (msg_bytes.Length - ii + 1);
			}
			_checksum_verify = XbeeAPIFrames.VerifyChecksum(msg_bytes);
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("AT Command Response for {0}: status: {1}, checksum => {2} \n",
							_cmd, Utils.GetAPCommandResponseStatusString(_status), _checksum_verify);
			if (_param != null)
			{
				sb.AppendFormat("Resp param => {0} \n", BitConverter.ToString(_param).Replace("-", " "));
			}
			return sb.ToString();
		}

	}

	public class XbeeTransmitStatus : XbeeAPIHeader
	{
		public byte _status;
		public bool _checksum_verify;

		public XbeeTransmitStatus(byte[] message_bytes)
		{
			FromBytes(message_bytes);
		}

		private void FromBytes(byte[] msg_bytes)
		{
			int ii;
			int tmp_len;
			_bytes = new byte[msg_bytes.Length];
			System.Array.Copy(msg_bytes, 0, _bytes, 0, msg_bytes.Length);
			tmp_len = ((msg_bytes[1] << 8) & (0xFF00));
			tmp_len += (msg_bytes[2]);
			ii = 3;
			_frame_type = msg_bytes[ii++];
			_frame_id = msg_bytes[ii++];
			_status = msg_bytes[ii++];
			_checksum_verify = XbeeAPIFrames.VerifyChecksum(msg_bytes);

		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("Transmit status: {0}, checksum => {1} \n",
							Utils.GetTransmitStatusString(_status), _checksum_verify);
			return sb.ToString();
		}

	}

	public class XbeeModemStatus : XbeeAPIHeader
	{
		public byte _status;
		public bool _checksum_verify;
		public XbeeModemStatus(byte[] message_bytes)
		{
			FromBytes(message_bytes);
		}

		private void FromBytes(byte[] msg_bytes)
		{
			int ii;
			_bytes = new byte[msg_bytes.Length];
			System.Array.Copy(msg_bytes, 0, _bytes, 0, msg_bytes.Length);
			ii = 3;
			_frame_type = msg_bytes[ii++];
			_status = msg_bytes[ii++];
			_checksum_verify = XbeeAPIFrames.VerifyChecksum(msg_bytes);

		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("Modem status: {0}, checksum => {1} \n",
							Utils.GetModemStatusString(_status), _checksum_verify);
			return sb.ToString();
		}

	}

	public class XbeeRxIpv4Frame : XbeeAPIHeader
	{
		byte[] _from_addr;
		ushort _dest_port;
		ushort _src_port;
		public byte[] _payload;
		byte _protocol; // 4 for ssl over tcp
		byte _status;
		bool _checksum_verify;

		public XbeeRxIpv4Frame(byte[] message_bytes)
		{
			FromBytes(message_bytes);
		}

		private void FromBytes(byte[] msg_bytes)
		{
			int ii;
			int tmp_len;
			byte[] tmpb = new byte[2];
			_bytes = new byte[msg_bytes.Length];
			System.Array.Copy(msg_bytes, 0, _bytes, 0, msg_bytes.Length);
			tmp_len = ((msg_bytes[1] << 8) & (0xFF00));
			tmp_len += (msg_bytes[2]);
			ii = 3;
			_frame_type = msg_bytes[ii++];
			_from_addr = new byte[4];
			_from_addr[0] = msg_bytes[ii++];
			_from_addr[1] = msg_bytes[ii++];
			_from_addr[2] = msg_bytes[ii++];
			_from_addr[3] = msg_bytes[ii++];
			tmpb[1] = msg_bytes[ii++];
			tmpb[0] = msg_bytes[ii++];
			_dest_port = BitConverter.ToUInt16(tmpb, 0);
			tmpb[1] = msg_bytes[ii++];
			tmpb[0] = msg_bytes[ii++];
			_src_port = BitConverter.ToUInt16(tmpb, 0);
			_protocol = msg_bytes[ii++];
			_status = msg_bytes[ii++];
			DCLogger.Debug(string.Format("src port {0}, dest port {1}", _src_port, _dest_port));
			DCLogger.Debug(string.Format("protocol {0}, status {1}", _protocol, _status));
			if (ii >= tmp_len + 3)
			{
				_payload = null;
			}
			else
			{
				_payload = new byte[msg_bytes.Length - ii - 1];
				System.Array.Copy(msg_bytes, ii, _payload, 0, msg_bytes.Length - ii - 1);
				ii += (msg_bytes.Length - ii + 1);
			}
			_checksum_verify = XbeeAPIFrames.VerifyChecksum(msg_bytes);
		}

		public override string ToString()
		{

			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("Rx Frame: status: {0}, checksum => {1} \n",
							_status, _checksum_verify);
			if (_payload != null)
			{
				sb.AppendFormat("Data => {0} \n", BitConverter.ToString(_payload).Replace("-", " "));
			}
			return sb.ToString();
		}
	}

	public class Utils
	{
		public static string GetAPCommandResponseStatusString(byte val)
		{
			StringBuilder sb = new StringBuilder();
			switch (val)
			{
				case (byte)ATRESPONSESTATUS.OK:
					sb.Append("Ok");
					break;
				case (byte)ATRESPONSESTATUS.ERROR:
					sb.Append("Error");
					break;
				case (byte)ATRESPONSESTATUS.INVALID_COMMAND:
					sb.Append("Invalid Command");
					break;
				case (byte)ATRESPONSESTATUS.INVALID_PARAM:
					sb.Append("Invalid Parameter");
					break;

				default:
					sb.AppendFormat("unknow AP command response status code {0}", val);
					break;

			}
			return sb.ToString();
		}

		public static string GetTransmitStatusString(ushort val)
		{
			StringBuilder sb = new StringBuilder();
			switch (val)
			{
				case (ushort)TRANSMITSTATUS.SUCCESSFUL_TRANSMIT:
					sb.Append("Succ Transmit");
					break;
				case (ushort)TRANSMITSTATUS.FAILED_CELL_NETWORK:
					sb.Append("Failed Cell Network");
					break;
				case (ushort)TRANSMITSTATUS.NOTREGISTERED_CELL_NETWORK:
					sb.Append("Not Registered to Cell Network");
					break;
				case (ushort)TRANSMITSTATUS.INVALID_FRAME_VALUES:
					sb.Append("Invalid Frame Values");
					break;
				case (ushort)TRANSMITSTATUS.INTERNAL_ERROR:
					sb.Append("Internal Error");
					break;
				case (ushort)TRANSMITSTATUS.RESOURCE_ERROR:
					sb.Append("Resource Error");
					break;
				case (ushort)TRANSMITSTATUS.MESSAGE_TOO_LONG:
					sb.Append("Message Too Long");
					break;
				case (ushort)TRANSMITSTATUS.INVALID_UDP_PORT:
					sb.Append("Invalid UDP Port");
					break;
				case (byte)TRANSMITSTATUS.INVALID_TCP_PORT:
					sb.Append("Invalid TCP Port");
					break;
				case (ushort)TRANSMITSTATUS.INVALID_HOST_ADDRESS:
					sb.Append("Invalid Host Address");
					break;
				case (ushort)TRANSMITSTATUS.INVALID_DATA_MODE:
					sb.Append("Invalid Data Mode");
					break;
				case (ushort)TRANSMITSTATUS.CONNECTION_REFUSED:
					sb.Append("Connection Refused");
					break;
				case (ushort)TRANSMITSTATUS.SOCKET_CONNECTION_LOST:
					sb.Append("Socket Connection Lost");
					break;
				case (ushort)TRANSMITSTATUS.NO_SERVER:
					sb.Append("No Server");
					break;
				case (ushort)TRANSMITSTATUS.SOCKET_CLOSED:
					sb.Append("Socket Closed");
					break;
				case (ushort)TRANSMITSTATUS.UNKNOWN_SERVER:
					sb.Append("Unknown Server");
					break;
				case (ushort)TRANSMITSTATUS.UNKNOWN_ERROR:
					sb.Append("Unknown Error");
					break;
				case (ushort)TRANSMITSTATUS.APP_NO_ADDRESS:
					sb.Append("No destination IPv4 address specified. Setup IPv4 address using SetDestinationAddrPort(...)");
					break;
				case (ushort)TRANSMITSTATUS.APP_NO_PORT:
					sb.Append("No destination port specified. Setup port using SetDestinationAddrPort(...)");
					break;
				case (ushort)TRANSMITSTATUS.APP_NO_APIMODE:
					sb.Append("Tx Packets can only be sent in API Mode. Please call SetAPIMode() before sending Tx packets");
					break;
				case (ushort)TRANSMITSTATUS.APP_TIMEOUT_TXSTS_RESPONSE:
					sb.AppendFormat("Failed to get Transmit status message in {0} secs", XbeeMessages.RESPONSE_TIMEOUT_SECS);
					break;
				default:
					sb.AppendFormat("unknow transmit status code {0}", val);
					break;

			}
			return sb.ToString();
		}

		public static string GetModemStatusString(byte val)
		{
			StringBuilder sb = new StringBuilder();
			switch (val)
			{
				case (byte)ModemStatus.HARDWARE_RESET:
					sb.Append("Hardware Reset or Power up");
					break;
				case (byte)ModemStatus.WATCHDOG_RESET:
					sb.Append("Watchdog Reset");
					break;
				case (byte)ModemStatus.REGISTERED_WITH_CELLULAR:
					sb.Append("Registered with cellular");
					break;
				case (byte)ModemStatus.UNREGISTERED_WITH_CELLULAR:
					sb.Append("Unregistered from cellular");
					break;
				case (byte)ModemStatus.DEVICE_CLOUD_CONNECTED:
					sb.Append("Device cloud connected");
					break;
				case (byte)ModemStatus.DEVICE_CLOUD_DISCONNECTED:
					sb.Append("Disconnected from device cloud");
					break;
				default:
					sb.AppendFormat("unknow Modem status code {0}", val);
					break;

			}
			return sb.ToString();
		}
	}
}
