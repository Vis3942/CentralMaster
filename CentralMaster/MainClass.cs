using System;

namespace CentralMaster
{
	class MainClass
	{
		#region threadsafe_set
		private static void WriteMessage(string message)
		{
			//Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
			//	new Action(
			//		delegate()
			//		{
			Console.WriteLine(message);
			//		}
			//	)
			//);

		}
		#endregion //threadsafe_set

		private static void ProcessMessages(byte command_type, ushort command_sub_type, G3.MessageHeader message)
		{
			if (command_type == (byte)CommandTypes.GET_SYSTEM_INFO)
			{
				WriteMessage(((G3.GetSystemInfoResponse)message).ToString());
			}

		}

		private static void ReceivedMessage(byte command_type, ushort command_sub_type, G3.MessageHeader message)
		{
			ProcessMessages(command_type, command_sub_type, message);
		}

		private static void TestSerial(string com_port_name)
		{
			int sent_message = 0;
			Console.WriteLine("Serial Comm Test");
			G3.HostMessages host_messaging = new G3.HostMessages();
			Communications.SerialComm serial_port;
			serial_port = new Communications.SerialComm(com_port_name);
			serial_port.BaudRate = 57600;
			serial_port.DataBits = 8;
			serial_port.StopBitValue = System.IO.Ports.StopBits.One;
			serial_port.ParityValue = System.IO.Ports.Parity.None;
			if (serial_port.Open())
			{
				host_messaging.SerialConnection = serial_port;
				host_messaging.receivedMessage += ReceivedMessage;
				host_messaging.SendGetSystemInfoRequest();
			}
			else
			{
				Console.WriteLine("Unable to open serial port");
			}

			//while (true) {
			//	System.Threading.Thread.Sleep (10000);
			while (sent_message < 5)
			{
				System.Threading.Thread.Sleep(10000);
				sent_message++;
				host_messaging.SendGetSystemInfoRequest();
			}
			//}
		}

		private static void Usage()
		{
			Console.WriteLine("Usage program.exe <PLC port name> <xbee:serial_port_name/websocket/socket>");
			// second parameter could be
			// xbee:/dev/ttyO2 
			// websocket
			// socket
		}

		public static void Main(string[] args)
		{
            //// arg[0] is portname for example /dev/ttyUSB0
            //if (args.Length < 2)
            //{
            //	Usage();
            //	return;
            //}

            //if ((args[1].Equals("websocket", StringComparison.CurrentCultureIgnoreCase)) ||
            //	(args[1].Equals("socket", StringComparison.CurrentCultureIgnoreCase)) ||
            //	(args[1].StartsWith("xbee:", StringComparison.CurrentCultureIgnoreCase)) ||
            //	(args[1].StartsWith("localwebsocket", StringComparison.CurrentCultureIgnoreCase))
            //	)
            //{
            //	HostApp happ = new HostApp(args[0], args[1]);
            //	happ.StartApplication();
            //}
            HostApp happ = new HostApp();
            happ.StartApplication();
            //TestSerial(args[0]);

        }
	}
}
