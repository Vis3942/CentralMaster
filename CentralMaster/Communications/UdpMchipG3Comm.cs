//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using LogUtils;
using System.Threading;

namespace CentralMaster.Communications
{
    public class UdpMchipG3Comm
    {
        #region variables
        private Socket sd;
        private bool sd_init = false;
        public const int BUF_SIZE = 4096; // Assuming BUF_SIZE is defined somewhere in the original C code
        private byte[] databuf = new byte[BUF_SIZE];
        private int datalen = BUF_SIZE; // databuf.Length;
        //private EndPoint dataRecvEP = null;
        private FileSystemWatcher watcher;
        public Action<string> writeToDisplay = null;
        public Action<byte[], int, EndPoint> receivedData = null;
        public Action<List<byte[]>, List<byte[]>> nodeUpdated = null;
        public Action<string> errorOccured = null;
        private Thread rxThread = null;
        private bool commInterfaceRunning = false;
        public List<byte[]> nodeIpv6List = new List<byte[]>();
        public List<byte[]> nodeMacAddrList = new List<byte[]>();
        private CancellationTokenSource cancelRead;
        private bool stopReading;
        private int wait_secs;
        private EndPoint readEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        private string err_str;
        private System.Timers.Timer restart_timer;
        private string nodeListFilename = "g3plc_node_list";
        private string nodeListFileFullPath = "/tmp/g3plc_node_list";

        #endregion // variables

        #region property
        public bool IsRxThreadRunning
        {
            get
            {
                return commInterfaceRunning;
            }
        }

        public int MessageTimeoutSecs
        {
            get
            {
                return wait_secs;
            }
            set
            {
                wait_secs = value;
            }
        }


        #endregion //property
        public UdpMchipG3Comm()
        {
            initSocket();
            startThreads();
            initWatch();
        }

        #region init_funcs
        public void initSocket()
        {
            try
            {
                sd = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                DCLogger.Information("Opening the datagram socket...OK.");

                // Enable SO_REUSEADDR to allow multiple instances of this
                // application to receive copies of the multicast datagrams.
                sd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                DCLogger.Information("Setting SO_REUSEADDR...OK.");

                //IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                //// TODO: SET PORT 61617 as option in settings
                //int selec_idx = -1;
                //for (int ii = 0; ii < hostEntry.AddressList.Length; ii++)
                //{
                //    if (hostEntry.AddressList[ii].AddressFamily == AddressFamily.InterNetworkV6)
                //    {
                //        selec_idx = ii;
                //        break;
                //    }
                //}
                //if (selec_idx > -1)
                //{
                //    IPEndPoint endPoint = new IPEndPoint(hostEntry.AddressList[selec_idx], 61617);

                //    sd.Bind(endPoint);
                //    DCLogger.Information($"Binding to {hostEntry.AddressList[selec_idx]} ...OK.");

                //    sd_init = true;
                //}
                // from vs 2019 central master code
                sd.Bind(new IPEndPoint(IPAddress.IPv6Any, 61617));
                sd_init = true;
            }
            catch (SocketException ex)
            {
                DCLogger.Error(string.Format("SocketException: {0}", ex.Message));
                DCLogger.Error(ex, ex.Message);
                sd_init = false;
                sd?.Close();
            }

        }

        public void initWatch()
        {
            watcher = new FileSystemWatcher(@"/tmp");

            //watcher.NotifyFilter = NotifyFilters.Attributes
            //                     | NotifyFilters.CreationTime
            //                     | NotifyFilters.DirectoryName
            //                     | NotifyFilters.FileName
            //                     | NotifyFilters.LastAccess
            //                     | NotifyFilters.LastWrite
            //                     | NotifyFilters.Security
            //                     | NotifyFilters.Size;
            watcher.NotifyFilter = NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size;

            watcher.Changed += OnCreated; // OnChanged;
            //watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            //watcher.Renamed += OnRenamed;
            //watcher.Error += OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;

        }

        private bool startThreads()
        {
            if (commInterfaceRunning == true)
                return true;
            rxThread = new Thread(new ThreadStart(this.messageRxThread));
            rxThread.Name = "";
            rxThread.Priority = ThreadPriority.AboveNormal;
            rxThread.Start();
            WriteMessage("Started UDP Receive Thread");
            commInterfaceRunning = true;
            return true;

        }

        private int ReadData(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return 0;

            Array.Clear(databuf, 0, datalen);
            //sd.ReceiveTimeout = wait_secs * 1000; // timeout is in millesecs
            int n = sd.ReceiveFrom(databuf, ref readEP);
            DCLogger.Information(string.Format("Value of n {0} after receivefrom call", n));
            if (n < 0)
            {
                DCLogger.Information("Error in read");
                return 0;
            }
            else
            {
                DCLogger.Information(string.Format("read from {0} ", readEP.ToString()));
                DCLogger.Information(string.Format("Read Okay len={0}", n));
                StringBuilder sb = new StringBuilder();
                for (int ii = 0; ii < n; ii++)
                {
                    sb.Append(string.Format("{0:x2} ", databuf[ii]));
                }
                sb.Append("\n");
                DCLogger.Information(sb.ToString());
                return n;
            }

        }

        private void messageRxThread()
        {
            byte[] buf = new byte[4096];
            int bytesRead;
            //startParsingData();
            cancelRead = new CancellationTokenSource();
            //DCLogger.Verbose("starting the recv loop");
            while (stopReading == false)
            {
                try
                {
                    //DCLogger.Verbose("before read async rxthread");
                    //bytesRead = await port.BaseStream.ReadAsync(buf, 0, buf.Length, cancelRead.Token);
                    bytesRead = ReadData(cancelRead.Token);
                    if (bytesRead > 0)
                    {
                        DCLogger.Verbose(string.Format("read {0} bytes ", bytesRead));

                        raiseAppDataEvent(databuf, bytesRead, readEP);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }
                catch (IOException ioe)
                {
                    // thread exit or application request to exit
                    DCLogger.Error(ioe, "Exception receiving data. Stopped receiving data.");
                    err_str = ioe.Message;
                    stopReading = true;
                    if (!cancelRead.Token.IsCancellationRequested)
                    {
                        restart_timer = new System.Timers.Timer(2000);
                        restart_timer.AutoReset = false;
                        restart_timer.Elapsed += Restart_timer_Elapsed;
                        restart_timer.Start();
                    }
                }
                catch (Exception e)
                {
                    DCLogger.Error(e, "Exception receiving data.");
                    WriteMessage(e.StackTrace);
                    WriteMessage(e.Message);
                    stopReading = true;
                }
            }
            //:$
            DCLogger.Verbose("Finished the recv loop");

        }

        private void Restart_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //restartComms();
            raiseErrorOccurred(err_str);
        }


        #endregion //init_funcs

        #region utils_funcs
        private void WriteMessage(string s)
        {
            if (writeToDisplay != null)
                writeToDisplay(s);
            else
                DCLogger.Information(s);
        }

        private void raiseAppDataEvent(byte[] received, int recv_length, EndPoint remoteEP)
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
                //WriteMessage(BitConverter.ToString(received));
                receivedData(received, recv_length, remoteEP);
            }
            else
            {
                string disp_str = BitConverter.ToString(received).Replace("-", " ");
                WriteMessage(string.Format("recv from {0} \n {1}", remoteEP.ToString(), disp_str));
            }
        }

        private void raiseErrorOccurred(string error_str)
        {
            errorOccured?.Invoke(error_str);
        }

        private void raiseUpdateNodeList(List<byte[]> node_ipv6, List<byte[]> node_ext_addr)
        {
            if (nodeUpdated != null)
            {
                DCLogger.Information("Node update list to Host app.");
                nodeUpdated(node_ipv6, node_ext_addr);
            }
            else
            {
                DCLogger.Information("uNABLE to update node list event. Event not setup.");
                for (int ii = 0; ii < node_ipv6.Count; ii++)
                {
                    DCLogger.Information($"attach indication ipv6 {BitConverter.ToString(node_ipv6[ii]).Replace("-", " ")} for node {BitConverter.ToString(node_ext_addr[ii]).Replace("-", " ")}");
                }
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (e != null)
            {
                string value = $"Deleted: {e.FullPath}";
                DCLogger.Information(value);
                if (e.FullPath.Equals(nodeListFileFullPath))
                {
                    // null values indicate that all current elements are to be removed
                    raiseUpdateNodeList(null, null);
                    DCLogger.Information("All nodes removed from CentralMaster due to reset of G3 coordinator");
                }

            }

        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (e != null)
            {
                string value = $"Created: {e.FullPath}";
                DCLogger.Information(value);
                if (e.FullPath.Equals(nodeListFileFullPath))
                {
                    if (System.IO.File.Exists(e.FullPath))
                    {
                        nodeIpv6List.Clear();
                        nodeMacAddrList.Clear();
                        DCLogger.Information("File exists. read file ");
                        using (var reader = new StreamReader(e.FullPath))
                        {
                            DCLogger.Information("reading file parse as csv");
                            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";", HasHeaderRecord = false };
                            DCLogger.Information("processing csv data");
                            using (var csv = new CsvReader(reader, config))
                            {
                                var records = csv.GetRecords<NodeClass>().ToList();
                                DCLogger.Information(records.Count.ToString());
                                DCLogger.Information(records.ToString());
                                foreach (NodeClass record in records)
                                {
                                    DCLogger.Information($"Processing node info ipv6 {record.ipv6_addr_str} and extended addr [{record.extended_addr_str}]");
                                    IPAddress tmp_ip_addr = IPAddress.Parse(record.ipv6_addr_str.Trim());
                                    nodeIpv6List.Add(tmp_ip_addr.GetAddressBytes());
                                    nodeMacAddrList.Add(Convert.FromHexString(record.extended_addr_str.Trim()));
                                }
                                DCLogger.Information($"num of node Ipv6 list {nodeIpv6List.Count}");
                                DCLogger.Information($"num of node ext addr list {nodeMacAddrList.Count}");
                                raiseUpdateNodeList(nodeIpv6List, nodeMacAddrList);
                            }
                        }
                    }
                    else
                    {
                        DCLogger.Information("File /tmp/g3plc_node_list does not exist");
                    }
                }

            }
        }


        public void CheckJoinedNodes()
        {
            if (System.IO.File.Exists(nodeListFileFullPath))
            {
                var args = new FileSystemEventArgs(WatcherChangeTypes.Created, @"/tmp", nodeListFilename);
                OnCreated(watcher, args);
            }
        }
        #endregion //utils_funcs

        public async void transmit(byte[] tosendb, byte[] ipv6_addr)
        {
            //await 
            if (!sd_init)
            {
                DCLogger.Information("Socket not initialized. Unable to send ");
            }
            // Initialize the group sockaddr structure with a
            //IPEndPoint groupEP = new IPEndPoint(IPAddress.Parse(ipv6_addr), 61617);
            IPAddress to_ip_addr = new IPAddress(ipv6_addr);
            IPEndPoint groupEP = new IPEndPoint(to_ip_addr, 61617);

            int result = await sd.SendToAsync(tosendb, SocketFlags.None, groupEP);

            if (result < 0)
            {
                DCLogger.Information(string.Format("Error in send to {0}", ipv6_addr));
            }
            else
            {
                DCLogger.Information(string.Format("Send to {0} Success", ipv6_addr));
            }
        }
    }

    public class NodeClass
    {
        public string ipv6_addr_str { get; set; }
        public string extended_addr_str { get; set; }
    }


}
