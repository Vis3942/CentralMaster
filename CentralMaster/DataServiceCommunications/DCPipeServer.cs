using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilsLibrary;
using LogUtils;
using System.Threading.Tasks.Dataflow;

namespace CentralMaster.DataServiceCommunications
{
    public class DCPipeServer: IDCDataServiceComms
    {
        #region vars
        private string _pipe_name = "";
        private NamedPipeServerStream serverPipe = null;
        Task serverTask = null;
        private StreamWriter swriter = null;
        private StreamReader sreader = null;
        private CancellationTokenSource pipeServerTokenSrc = new CancellationTokenSource();
        private Random srand = new Random(DateTime.Now.Millisecond);
        private BufferBlock<string> buffer = new BufferBlock<string>();
        private const int MAX_BUFFER_SIZE = 1024;
        #endregion // vars

        #region property
        public string PipeName
        {
            get { return _pipe_name; }
            set { _pipe_name = value; }
        }

        public bool IsConnected
        {
            get
            {
                if (serverPipe != null)
                {
                    return serverPipe.IsConnected;
                }
                else
                {
                    return false;
                }
            }
        }
        #endregion // property

        public DCPipeServer(string pipe_name)
        {
            _pipe_name = pipe_name;
        }


        public void StartServer()
        {
            if (serverPipe == null)
            {
                Task.Run(() => StartPipedServerAsync(), pipeServerTokenSrc.Token);
            }
        }


        public void StopServer()
        {
            if (serverPipe != null)
            {
                pipeServerTokenSrc.Cancel();
                System.Threading.Thread.Sleep(1000);
                if (serverPipe.IsConnected)
                {
                    serverPipe.Disconnect();
                }
                serverPipe = null;
                pipeServerTokenSrc = new CancellationTokenSource();
            }
        }

        private async Task StartPipedServerAsync()
        {
            while (!pipeServerTokenSrc.IsCancellationRequested)
            {
                DCLogger.Information("Setting up server pipe");
                serverPipe = new NamedPipeServerStream(_pipe_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await serverPipe.WaitForConnectionAsync(pipeServerTokenSrc.Token);
                DCLogger.Information("got connection...");
                if (serverPipe.IsConnected)
                {
                    swriter = new StreamWriter(serverPipe);
                    swriter.AutoFlush = true;
                    sreader = new StreamReader(serverPipe);
                    try
                    {
                        var task_wr = Task.Run(async () =>
                        {
                            while ((!pipeServerTokenSrc.IsCancellationRequested) && (await buffer.OutputAvailableAsync()))
                            {
                                string tmp_message = await buffer.ReceiveAsync<string>(pipeServerTokenSrc.Token);
                                await swriter.WriteLineAsync(tmp_message);
                            }
                        });
                        var task = Task.Run(() => ReadDataFromPipe(pipeServerTokenSrc.Token), pipeServerTokenSrc.Token);
                        if (task.IsFaulted)
                        {
                            break;
                        }
                        //task.Wait();
                        Task.WaitAny(task, task_wr);
                        if (task.IsCompleted)
                        {
                            DCLogger.Information("read task completed");
                        }
                        if (task_wr.IsCompleted)
                        {
                            DCLogger.Information("write task completed");
                        }

                        if ( (serverPipe != null) && (serverPipe.IsConnected) )
                        {
                            serverPipe.Disconnect();
                        }
                        serverPipe = null;
                    }
                    catch (Exception e)
                    {
                        DCLogger.Error(e, "Reading from DCDataservices pipe");
                    }
                    finally
                    {
                        if (swriter != null)
                        {
                            swriter = null;
                        }
                        sreader = null;
                    }
                }
            }
            DCLogger.Information("exit StartPipedServerAsync");
        }

        public void WriteSimulatedCentralMasterData()
        {
            string dt_str = DateTime.Now.ToUniversalTime().ToString("MM/dd/yy HH:mm:ss") + " +00:00";
            List<TransferData> node_recvd_data = new List<TransferData>();
            ResponseData rd1 = new ResponseData()
            {
                Name = "air_temperature",
                Value = ((float)(srand.NextDouble() * 40.0 + 45.0)).ToString("F", CultureInfo.InvariantCulture),
                Timestamp = dt_str,
                EventId = ""
            };
            TransferData td1 = new TransferData()
            {
                NodeName = "NCx_EB33CA",
                UUID = Guid.NewGuid().ToString(),
                Data = new ResponseData[] { rd1 }
            };
            node_recvd_data.Add(td1);

            ResponseData rd2 = new ResponseData()
            {
                Name = "rel_humidity",
                Value = ((float)(srand.NextDouble() * 90)).ToString("F", CultureInfo.InvariantCulture),
                Timestamp = dt_str,
                EventId = ""
            };
            TransferData td2 = new TransferData()
            {
                NodeName = "NCx_EB33CA",
                UUID = Guid.NewGuid().ToString(),
                Data = new ResponseData[] { rd2 }
            };
            node_recvd_data.Add(td2);

            ResponseData rd3 = new ResponseData()
            {
                Name = "co2_count",
                Value = ((float)(srand.NextDouble() * 1100)).ToString("F", CultureInfo.InvariantCulture),
                Timestamp = dt_str,
                EventId = ""
            };
            TransferData td3 = new TransferData()
            {
                NodeName = "NCx_EB33CA",
                UUID = Guid.NewGuid().ToString(),
                Data = new ResponseData[] { rd3 }
            };
            node_recvd_data.Add(td3);

            ResponseData rd4 = new ResponseData()
            {
                Name = "diff_pressure",
                Value = ((float)(srand.NextDouble())).ToString("F", CultureInfo.InvariantCulture),
                Timestamp = dt_str,
                EventId = ""
            };
            TransferData td4 = new TransferData()
            {
                NodeName = "NCx_EB33CA",
                UUID = Guid.NewGuid().ToString(),
                Data = new ResponseData[] { rd4 }
            };
            node_recvd_data.Add(td4);

            ResponseData rd5 = new ResponseData()
            {
                Name = "uv_intensity",
                Value = ((float)(srand.NextDouble() * 100)).ToString("F", CultureInfo.InvariantCulture),
                Timestamp = dt_str,
                EventId = ""
            };
            TransferData td5 = new TransferData()
            {
                NodeName = "NCx_EB33CA",
                UUID = Guid.NewGuid().ToString(),
                Data = new ResponseData[] { rd5 }
            };
            node_recvd_data.Add(td5);

            TransferDataList tdl = new TransferDataList();
            tdl.Items = node_recvd_data;
            string tmpstr = System.Text.Json.JsonSerializer.Serialize(tdl);
            WriteToPipe(tmpstr);
        }

        public void WriteToPipe(string message)
        {
            if (((serverPipe != null) && (serverPipe.IsConnected)) && (swriter != null))
            {
                while (buffer.Count > MAX_BUFFER_SIZE)
                {
                    _ = buffer.ReceiveAsync();
                }
                buffer.SendAsync(message);
                //swriter.WriteLine(message);
            }
            else
            {
                DCLogger.Information("Failed to send to Data Services layer");
                DCLogger.Debug($"serverPipe = {serverPipe}, swriter = {swriter}");
                if (serverPipe != null)
                {
                    DCLogger.Debug($"is connected = {serverPipe.IsConnected}");
                }
            }
        }

        public void ReadDataFromPipe(CancellationToken cts)
        {
            while ((!cts.IsCancellationRequested) && (sreader != null))
            {
                Task<string> t = sreader.ReadLineAsync();
                if (t.IsFaulted)
                {
                    break;
                }
                t.Wait();
                string recv_str = t.Result;
                if (recv_str == null)
                {
                    break;
                }
                DCLogger.Information(string.Format("recv: {0}", recv_str));
            }
            if (sreader != null)
            {
                sreader.Close();
            }
            DCLogger.Information("Read Data From Pipe: done.");
            // do you need to restart the connection
        }

        public void TransmitToDataService(string message)
        {
            WriteToPipe(message);
        }
    }
}
