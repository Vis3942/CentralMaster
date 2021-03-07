using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

enum XbeeDeviceModes : byte
{
	TRANSPARENT = 0x00,
	API_MODE = 0x01,
	API_ESCAPED_MODE = 0x02,
	BYPASS_MODE = 0x05,
	COMMAND_MODE = 0xFF
}

enum FrameTypes : byte
{
	AT_COMMAND_REQUEST = 0x08,
	AT_COMMAND_QUEUE = 0x09,
	TX_SMS = 0x1F,
	IPV4_TX_PACKET = 0x20,
	AT_COMMAND_RESPONSE = 0x88,
	IPV4_TX_STATUS = 0x89,
	MODEM_STATUS = 0x8A,
	RX_SMS = 0x9F,
	IPV4_RX_PACKET = 0xB0
}


namespace CentralMaster.Communications
{
	public class XbeeMessages: ITransmit
	{
		public static int RESPONSE_TIMEOUT_SECS = 20;
		public static int RETRY_LIMIT = 3;


		#region vars
		private Communications.SerialComm _serial_conn = null;
		private bool serialInterfaceReady = false;

		// vars supporting property
		//private bool _checkResponse;

		private byte[] tmpQueueBuf = new byte[4096];
		private int tmpQueueBufUsedLength = 0;

		private CancellationTokenSource cancelParsingMessages = new CancellationTokenSource();
		private BlockingCollection<byte[]> receiveQueue = new BlockingCollection<byte[]>();
		//private BlockingCollection<MessageHeader> recvMsgs = new BlockingCollection<MessageHeader>();

		public Action<string> writeToDisplay = null;
		public Action<ushort> receivedErrorNotification = null;
		public Action<byte[]> receivedMessage = null;

		private BlockingCollection<byte[]> transmitQueue = new BlockingCollection<byte[]>();
		private CancellationTokenSource cancelTransmitQueue = new CancellationTokenSource();
		private BackgroundWorker transmitWorker = null;

		private byte deviceCurrentMode = (byte)XbeeDeviceModes.TRANSPARENT; // default mode
		private bool wait_for_response = false;
		private string response_str = "";
		private byte[] response_bytes = null;
		private const int RESPONSE_WAIT_SLEEP_MS = 500; // 500 milliseconds
		private DateTime command_mode_datetime;

		public byte rx_frame_type;
		public object rx_frame;
		public byte wait_frame_id;
		public byte wait_frame_type;

		private byte[] _ping_arr = new byte[] {
			0x70, 0x69, 0x6e, 0x67,
			0x70, 0x69, 0x6e, 0x67,
			0x70, 0x69, 0x6e, 0x67
		};

		private byte[] _pong_arr = new byte[] {
			0x70, 0x6f, 0x6e, 0x67,
			0x70, 0x6f, 0x6e, 0x67,
			0x70, 0x6f, 0x6e, 0x67
		};

		private byte[] _destination_addr = null;
		private ushort _destination_port = 8182;
		private byte _modem_status = (byte)ModemStatus.HARDWARE_RESET; // initial state on power up
		private string _channel_name;
		private ushort _transmit_ready_status = 0xFFFF;
		#endregion // vars

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

		public Communications.SerialComm SerialConnection
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

		public byte XbeeDeviceMode
		{
			get
			{
				return (byte)deviceCurrentMode;
			}
		}

		public ushort TransmitReadyStatus
		{
			get
			{
				return _transmit_ready_status;
			}
		}

		#endregion property

		public XbeeMessages(string channel_name)
		{
			_channel_name = channel_name;
		}

		private void serialDataReceivedEvent(byte[] received)
		{
			byte[] for_len = new byte[2];
			//Console.WriteLine(string.Format("recv -> {0}", received.Length));
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
				if (deviceCurrentMode == (byte)XbeeDeviceModes.TRANSPARENT)
				{
					// send data as recv
					byte[] forMsgQ = new byte[tmpQueueBufUsedLength];
					Array.Copy(tmpQueueBuf, 0, forMsgQ, 0, tmpQueueBufUsedLength);
					addToReceivedMessagesQueue(forMsgQ);
					tmpQueueBufUsedLength = 0;
					done = true;

				}
				else if (deviceCurrentMode == (byte)XbeeDeviceModes.COMMAND_MODE)
				{
					// send data as recv
					byte[] forMsgQ = new byte[tmpQueueBufUsedLength];
					Array.Copy(tmpQueueBuf, 0, forMsgQ, 0, tmpQueueBufUsedLength);
					addToReceivedMessagesQueue(forMsgQ);
					tmpQueueBufUsedLength = 0;
					done = true;
				}
				else if (deviceCurrentMode == (byte)XbeeDeviceModes.API_MODE)
				{
					if (tmpQueueBuf[0] == 0x7E)
					{
						// found valid message frame
						// is valid message length data available if yes copy it and delete it from tmpQuueuBuf
						if (tmpQueueBufUsedLength <= 3)
						{
							done = true;
							break;
						}
						for_len[0] = tmpQueueBuf[2];
						for_len[1] = tmpQueueBuf[1];
						msglen = (int)BitConverter.ToUInt16(for_len, 0);
						//msglen += 4; // 1 for delimiter byte, 2 for length and 1 for checksum
						System.Console.WriteLine(string.Format("data = {0}", BitConverter.ToString(tmpQueueBuf, 0, tmpQueueBufUsedLength)));
						System.Console.WriteLine(string.Format("expected msg len = {0}", msglen + 4));
						if (tmpQueueBufUsedLength >= (msglen + 4)) // start delimiter 0x7e, length 2 bytes, checksum 1 byte at end
						{
							// valid packat available
							byte[] forMsgQ = new byte[msglen + 4];
							Array.Copy(tmpQueueBuf, 0, forMsgQ, 0, msglen + 4);
							System.Console.WriteLine(string.Format("Adding to queue msg [{0}] => {1} ", msglen + 4, BitConverter.ToString(forMsgQ)));
							addToReceivedMessagesQueue(forMsgQ);
							// delete data from tmpQueueBuf
							int tocopylen = tmpQueueBufUsedLength - msglen - 4;
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
		}

		public void SetDestinationAddrPort(byte[] dest_arr, ushort dest_port)
		{
			_destination_addr = new byte[dest_arr.Length];
			System.Array.Copy(dest_arr, _destination_addr, dest_arr.Length);
			_destination_port = dest_port;
		}



		public void SerialInterfaceOpen()
		{
			if (serialInterfaceReady)
				return;

			if (_serial_conn == null)
				return;

			_serial_conn.receivedData += serialDataReceivedEvent;
			_serial_conn.errorOccured += NotifyErrorOccured;
			startParsingData();
			StartTransmitBackgroundProcess();
			serialInterfaceReady = true;
		}

		public void SerialInterfaceClose()
		{
			if (!serialInterfaceReady)
				return;

			_serial_conn.receivedData -= serialDataReceivedEvent;
			_serial_conn.errorOccured -= NotifyErrorOccured;
			StopTransmitBackgroundProcess();
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
						//Console.WriteLine(string.Format("rx Queue Frame Type = 0x{0:x2}, ", processDatab[3]));
						ProcessReceivedMessage(processDatab);
					}
					catch (Exception e)
					{
						WriteMessage(e.Message);
						// restart
					}
				}
			}, TaskCreationOptions.LongRunning);
		}

		private void ProcessReceivedMessage(byte[] message_bytes)
		{
			switch (deviceCurrentMode)
			{
				case (byte)XbeeDeviceModes.COMMAND_MODE:
					ProcessInCommandMode(message_bytes);
					break;

				case (byte)XbeeDeviceModes.API_MODE:
					ProcessInAPIMode(message_bytes);
					break;

				default:
					WriteMessage(string.Format("mode {0}, data -> {1}", (byte)deviceCurrentMode, BitConverter.ToString(message_bytes).Replace("-", " ")));
					break;
			}
		}

		private void ProcessInCommandMode(byte[] message_bytes)
		{
			if (wait_for_response)
			{
				wait_for_response = false;
			}
			response_str = System.Text.Encoding.ASCII.GetString(message_bytes);
			//debug
			System.Console.WriteLine(string.Format("ProcCmdMode: {0}", response_str));
		}

		private void ProcessInAPIMode(byte[] message_bytes)
		{
			if (wait_for_response)
			{
				if ((message_bytes[3] == wait_frame_type) &&
					(message_bytes[4] == wait_frame_id)
				   )
				{
					wait_for_response = false;
				}
			}
			response_bytes = new byte[message_bytes.Length];
			System.Array.Copy(message_bytes, response_bytes, message_bytes.Length);
			// // debug
			//System.Console.WriteLine(string.Format(" in process api : {0}", BitConverter.ToString(message_bytes)));
			if (message_bytes[3] == (byte)FrameTypes.AT_COMMAND_RESPONSE)
			{
				// it is a at command response
				XbeeATCommandResponse xacr = new XbeeATCommandResponse(message_bytes);
				WriteMessage(string.Format(" recv AT Resp -> {0} ", xacr.ToString()));
				rx_frame_type = message_bytes[3];
				rx_frame = xacr;
			}
			else if (message_bytes[3] == (byte)FrameTypes.IPV4_TX_STATUS)
			{
				// it is a tx status response
				XbeeTransmitStatus xts = new XbeeTransmitStatus(message_bytes);
				WriteMessage(string.Format(" Transmit Status -> {0} ", xts.ToString()));
				rx_frame_type = message_bytes[3];
				rx_frame = xts;
			}
			else if (message_bytes[3] == (byte)FrameTypes.MODEM_STATUS)
			{
				// it is a modem status response
				XbeeModemStatus xms = new XbeeModemStatus(message_bytes);
				WriteMessage(string.Format(" Modem Status -> {0} ", xms.ToString()));
				rx_frame_type = message_bytes[3];
				rx_frame = xms;
				SetModemStatus(xms._status);
			}
			else if (message_bytes[3] == (byte)FrameTypes.IPV4_RX_PACKET)
			{
				// rx data frame
				System.Console.WriteLine("Recvd IPV4 frame");
				XbeeRxIpv4Frame xrif = new XbeeRxIpv4Frame(message_bytes);
				WriteMessage(string.Format(" Rx frame: {0} ", xrif.ToString()));
				rx_frame_type = message_bytes[3];
				rx_frame = xrif;
				if (Enumerable.SequenceEqual(_ping_arr, xrif._payload))
				{
					// respond with _pong_arr
					SendIPv4TxPacket(_pong_arr);
				}
				else
				{
					NotifyMessageReceived(xrif._payload);
				}
			}
			else
			{
				// unused or unsupported frame receeived
				WriteMessage(string.Format("rx frame type [{0}] => {1}", message_bytes[3], BitConverter.ToString(message_bytes).Replace("-", " ")));
			}
		}

		#region concurrent_funcs
		private void TransmitDataTask(object sender, DoWorkEventArgs e)
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

		private void StartTransmitBackgroundProcess()
		{
			if (transmitWorker != null)
			{
				// need to stop it
				StopTransmitBackgroundProcess();
			}
			cancelTransmitQueue = new CancellationTokenSource();
			transmitWorker = new BackgroundWorker();
			transmitWorker.DoWork += TransmitDataTask;
			transmitWorker.WorkerSupportsCancellation = true;
			transmitWorker.RunWorkerAsync();

		}

		private void StopTransmitBackgroundProcess()
		{
			cancelTransmitQueue.Cancel(true);
			Thread.Sleep(450); // give time for loop to quit
			transmitWorker.DoWork -= TransmitDataTask;
			transmitWorker = null;
		}

		#endregion // concurrent_funcs


		#region utility_funcs

		public void transmit(byte[] data_to_send)
		{
			SafeSendIPv4TxPacketReadStatus(data_to_send);
		}

		private void SetModemStatus(byte new_modem_status)
		{
			_modem_status = new_modem_status;
		}

		private void WriteMessage(string s)
		{
			if (writeToDisplay != null)
				writeToDisplay(s);
			else
				Console.WriteLine(s);
		}

		private void NotifyMessageReceived(byte[] msg_bytes)
		{
			if (receivedMessage != null)
			{
				receivedMessage(msg_bytes);
			}
			else
			{
				Console.WriteLine(string.Format("XbeeMsg: Received Message -> {0}", BitConverter.ToString(msg_bytes)));
			}
		}


		private void NotifyErrorOccured(ushort serial_error)
		{
			receivedErrorNotification?.Invoke(serial_error);
		}


		public void ToCommandMode()
		{
			byte[] tosend = new byte[3];
			Thread.Sleep(1000);
			tosend[0] = 0x2B; // + char
			tosend[1] = 0x2B; // + char
			tosend[2] = 0x2B; // + char
			sendMessageOnSerial(tosend);
			Thread.Sleep(1250);
			deviceCurrentMode = (byte)XbeeDeviceModes.COMMAND_MODE;
			command_mode_datetime = DateTime.UtcNow;
		}

		public bool VerifyCommandMode()
		{
			if (deviceCurrentMode == (byte)XbeeDeviceModes.COMMAND_MODE)
			{
				DateTime now = DateTime.UtcNow;
				// debug
				System.Console.WriteLine(string.Format(" time since command mode setup {0} secs", (now - command_mode_datetime).TotalSeconds));
				if ((now - command_mode_datetime).TotalSeconds > 10)
				{
					// system leaves command mode
					return false;
				}
				else
				{
					string resp_str = SendCommandReadResponseCommandMode("AI", string.Empty);
					if (resp_str == string.Empty)
					{
						return false;
					}
					else
					{
						// debug
						System.Console.WriteLine(string.Format("resp to AI command {0}", resp_str));
						return true;
					}
				}
			}
			else
			{
				return false;
			}

		}

		public bool SetAPIMode()
		{
			if (deviceCurrentMode == (byte)XbeeDeviceModes.API_MODE)
				return true;
			bool sts = false;
			bool sts_ip = false;
			bool sts_cn = false;
			string resp_str;
			byte mode_val = (byte)XbeeDeviceModes.API_MODE;
			if (deviceCurrentMode != (byte)XbeeDeviceModes.COMMAND_MODE)
			{
				ToCommandMode();
				sts = VerifyCommandMode();
			}
			if (sts) // successfully setup and verified command mode
			{
				resp_str = SendCommandReadResponseCommandMode("AP", mode_val.ToString("X"));
				System.Console.WriteLine(string.Format("response for AP command {0}", resp_str));
				if (resp_str.Contains("OK"))
				{
					sts = true;
				}
				else
				{
					sts = false;
				}
				System.Threading.Thread.Sleep(500);
				resp_str = SendCommandReadResponseCommandMode("IP", "4"); // setup for SSL over tcp
				System.Console.WriteLine(string.Format("response for IP command {0}", resp_str));
				if (resp_str.Contains("OK"))
				{
					sts_ip = true;
				}
				else
				{
					sts_ip = false;
				}
				System.Threading.Thread.Sleep(500);
				resp_str = SendCommandReadResponseCommandMode("CN", string.Empty);
				System.Console.WriteLine(string.Format("response for CN command {0}", resp_str));
				if (resp_str != string.Empty)
				{
					sts_cn = true;
				}
				else
				{
					sts_cn = false;
				}
			}
			deviceCurrentMode = (byte)XbeeDeviceModes.API_MODE;
			return (sts && sts_ip && sts_cn);
		}

		public bool DataConnectionReadyAPIMode()
		{
			// we assume that device is in API mode
			if (deviceCurrentMode != (byte)XbeeDeviceModes.API_MODE)
			{
				return false;
			}
			object[] ret_objs = SendAPICommandFrameReadResponse("AI", string.Empty);
			if (ret_objs != null)
			{
				XbeeATCommandResponse xacr = null;
				if ((byte)ret_objs[0] == (byte)FrameTypes.AT_COMMAND_RESPONSE)
				{
					xacr = (XbeeATCommandResponse)ret_objs[1];
					//System.Console.WriteLine(string.Format("resp for AI api cmd => {0}", xacr.ToString()));
					if (xacr._param[0] == 0)
					{
						return true;
					}
					else
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}
		//public bool VerifyAPIMode()
		//{
		//	
		//}

		public string SendCommandReadResponseCommandMode(string command, string param)
		{
			if (deviceCurrentMode != (byte)XbeeDeviceModes.COMMAND_MODE)
			{
				return string.Empty;
			}
			// command is a 2 char command eg AI, CI etc...
			string tosend_str;
			int time_elapsed_ms = 0;
			if (param == string.Empty)
			{
				tosend_str = "AT" + command + "\r";
			}
			else
			{
				tosend_str = "AT" + command + " " + param + "\r";
			}
			wait_for_response = true;
			sendMessageOnSerial(tosend_str);
			while ((wait_for_response == true) && (time_elapsed_ms < XbeeMessages.RESPONSE_TIMEOUT_SECS * 1000))
			{
				Thread.Sleep(RESPONSE_WAIT_SLEEP_MS);
				time_elapsed_ms += RESPONSE_WAIT_SLEEP_MS;
			}
			if (wait_for_response == true)
			{
				WriteMessage(string.Format("Command Mode: Failed to receive response for {0} command", command));
				return string.Empty;
			}
			else
			{
				//WriteMessage(string.Format("Command Mode: Resp for command {0} => {1}",command, response_str));
				return response_str;
			}
		}

		public object[] SendAPICommandFrameReadResponse(string command, string param)
		{
			// command is a 2 char command eg AI, CI etc...
			if (deviceCurrentMode != (byte)XbeeDeviceModes.API_MODE)
			{
				return null;
			}
			int time_elapsed_ms = 0;
			wait_for_response = true;
			XbeeATCommandFrame xatcf = new XbeeATCommandFrame(command, param, false);
			wait_frame_id = XbeeAPIFrames.GetCurrentCommandID();
			wait_frame_type = (byte)FrameTypes.AT_COMMAND_RESPONSE;
			sendMessageOnSerial(xatcf.ComputeBytes());
			while ((wait_for_response == true) && (time_elapsed_ms < XbeeMessages.RESPONSE_TIMEOUT_SECS * 1000))
			{
				Thread.Sleep(RESPONSE_WAIT_SLEEP_MS);
				time_elapsed_ms += RESPONSE_WAIT_SLEEP_MS;
			}
			if (wait_for_response == true)
			{
				WriteMessage(string.Format("API Mode: Failed to receive response for {0} command", command));
				return null;
			}
			else
			{
				WriteMessage(string.Format("API Mode: Resp for command {0}, Frame {1:x2} => {2}", command, rx_frame_type, rx_frame.ToString()));
				return new Object[] { rx_frame_type, rx_frame };
			}
		}

		public void SendIPv4TxPacket(byte[] to_addr, ushort to_port, byte[] payload)
		{
			XbeeTxIPv4Frame xtif = new XbeeTxIPv4Frame(to_addr, to_port, 0, 4, false, payload);
			SafeSendMessageOnSerial(xtif.ComputeBytes());
		}

		public void SendIPv4TxPacket(byte[] payload)
		{
			if (_destination_addr != null)
			{
				XbeeTxIPv4Frame xtif = new XbeeTxIPv4Frame(_destination_addr, _destination_port, 0, 4, false, payload);
				SafeSendMessageOnSerial(xtif.ComputeBytes());
			}
			else
			{
				System.Console.WriteLine(string.Format("Failed to send {0}: no dest addr ", BitConverter.ToString(payload)));
			}
		}

		public ushort SendIPv4TxPacketReadStatus(byte[] payload)
		{
			// can only be sent in API mode
			if (deviceCurrentMode != (byte)XbeeDeviceModes.API_MODE)
			{
				return (ushort)TRANSMITSTATUS.APP_NO_APIMODE;
			}

			if (_destination_addr == null)
			{
				return (ushort)TRANSMITSTATUS.APP_NO_ADDRESS;
			}

			if (_destination_port == 0)
			{
				return (ushort)TRANSMITSTATUS.APP_NO_PORT;
			}

			int time_elapsed_ms = 0;
			wait_for_response = true;
			System.Console.WriteLine(string.Format(" modem status = {0:x2}", _modem_status));
			while (_modem_status == (byte)ModemStatus.UNREGISTERED_WITH_CELLULAR)
			{
				// need to do network reset before sending command
				object[] nr_resp = SendAPICommandFrameReadResponse("NR", string.Empty);
				if (nr_resp != null)
				{
					if ((byte)nr_resp[0] == (byte)FrameTypes.AT_COMMAND_RESPONSE)
					{
						XbeeATCommandResponse xacr = (XbeeATCommandResponse)nr_resp[1];
						System.Console.WriteLine(string.Format("Resp for NR {0}", Utils.GetAPCommandResponseStatusString(xacr._status)));
						if (xacr._status == (byte)ATRESPONSESTATUS.OK)
						{
							// wait for reset to happen before sending another command
							System.Threading.Thread.Sleep(60000);
							while (!DataConnectionReadyAPIMode())
							{
								System.Threading.Thread.Sleep(10000);
							}
						}

					}
				}
				System.Threading.Thread.Sleep(5000);
			}
			try
			{
				XbeeTxIPv4Frame xtif = new XbeeTxIPv4Frame(_destination_addr, _destination_port, 0, 4, false, payload);
				wait_frame_id = XbeeAPIFrames.GetCurrentCommandID();
				wait_frame_type = (byte)FrameTypes.IPV4_TX_STATUS;
				SafeSendMessageOnSerial(xtif.ComputeBytes());
				while ((wait_for_response == true) && (time_elapsed_ms < XbeeMessages.RESPONSE_TIMEOUT_SECS * 1000))
				{
					Thread.Sleep(RESPONSE_WAIT_SLEEP_MS);
					time_elapsed_ms += RESPONSE_WAIT_SLEEP_MS;
				}
				if (wait_for_response == true)
				{
					WriteMessage(string.Format("API Mode: Failed to receive response for transmit IPv4"));
					return (ushort)TRANSMITSTATUS.APP_TIMEOUT_TXSTS_RESPONSE;
				}
				else
				{
					WriteMessage(string.Format("API Mode: Tx Sts[ Frame {0:x2} => {1} ]", rx_frame_type, ((XbeeTransmitStatus)rx_frame)._status));
					return (ushort)(((XbeeTransmitStatus)rx_frame)._status);
				}

			}
			catch (Exception ee)
			{
				System.Console.WriteLine(ee.StackTrace);
				System.Console.WriteLine(ee.Message);
				return (ushort)TRANSMITSTATUS.APP_TIMEOUT_TXSTS_RESPONSE;
			}

		}

		public ushort SafeSendIPv4TxPacketReadStatus(byte[] payload)
		{
			ushort tx_sts;
			int repeat_count = 0;
			tx_sts = SendIPv4TxPacketReadStatus(payload);
			while ((tx_sts == (ushort)TRANSMITSTATUS.RESOURCE_ERROR) &&
					(repeat_count < XbeeMessages.RETRY_LIMIT)
				  )
			{
				tx_sts = SendIPv4TxPacketReadStatus(payload);
				repeat_count++;
			}
			return tx_sts;
		}

		public void SendIPv4Close(byte[] to_addr, ushort to_port)
		{
			SendIPv4TxPacket(to_addr, to_port, System.Text.Encoding.ASCII.GetBytes("closecloseclose"));
		}

		public void SendIPv4Close()
		{
			SendIPv4TxPacket(_destination_addr, _destination_port, System.Text.Encoding.ASCII.GetBytes("closecloseclose"));
		}

		public bool GetHostAddressSetIPv4AddressPort(string domain_name, ushort port_number)
		{
			object[] ret_objs = SendAPICommandFrameReadResponse("LA", domain_name);
			if (ret_objs != null)
			{
				XbeeATCommandResponse xacr = null;
				if ((byte)ret_objs[0] == (byte)FrameTypes.AT_COMMAND_RESPONSE)
				{
					xacr = (XbeeATCommandResponse)ret_objs[1];
					System.Console.WriteLine(string.Format("resp for LA api cmd => {0}", xacr.ToString()));
					// succ return
					if (xacr != null)
					{
						SetDestinationAddrPort(xacr._param, port_number);
						return true;
					}
					else
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}

		public ushort SendChannelInfo()
		{
			int number_attempts = 0;
			ushort channel_tx_sts = 0xFFFF;
			while ((channel_tx_sts != (ushort)TRANSMITSTATUS.SUCCESSFUL_TRANSMIT) &&
				   (number_attempts < 5)
				  )
			{
				channel_tx_sts = SafeSendIPv4TxPacketReadStatus(System.Text.Encoding.ASCII.GetBytes("mychannelname:" + _channel_name));
				if (channel_tx_sts == (ushort)TRANSMITSTATUS.SUCCESSFUL_TRANSMIT)
				{
					break;
				}
				number_attempts++;
				System.Threading.Thread.Sleep(5000);
			}
			return channel_tx_sts;
		}

		public bool TransmitReady(string domain_name, ushort to_port)
		{
			bool cmd_succ = GetHostAddressSetIPv4AddressPort(domain_name, to_port);
			ushort channel_tx_sts;
			if (cmd_succ)
			{
				channel_tx_sts = SendChannelInfo();
			}
			else
			{
				channel_tx_sts = 0xFFFF;
			}

			_transmit_ready_status = channel_tx_sts;
			if (channel_tx_sts == (ushort)TRANSMITSTATUS.SUCCESSFUL_TRANSMIT)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		#endregion // utility_funcs

		#region send_messages

		public int TransmitQueueLength()
		{
			return transmitQueue.Count;
		}

		private void SafeSendMessageOnSerial(string tosendstr)
		{
			byte[] tosendb = System.Text.Encoding.ASCII.GetBytes(tosendstr);
			SafeSendMessageOnSerial(tosendb);
		}

		private void SafeSendMessageOnSerial(byte[] tosendb)
		{
			if (tosendb != null)
			{
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

		private void sendMessageOnSerial(string tosendstr)
		{
			if (_serial_conn == null)
				return; //false;
			WriteMessage(tosendstr);
			byte[] tosendb = System.Text.Encoding.ASCII.GetBytes(tosendstr);
			_serial_conn.transmit(tosendb);
			//return true;
		}

		#endregion // send_messages


	}
}
