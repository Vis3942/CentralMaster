using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster
{
    public class StatusServicePipeClient
    {
        #region vars
        private string _pipe_name = "";
        private NamedPipeClientStream clientPipe = null;
        //Task clientTask = null;
        private StreamWriter swriter = null;
        private StreamReader sreader = null;
        private CancellationTokenSource pipeClientTokenSrc = new CancellationTokenSource();
        private bool powerLineNetworkActive = false;
        private string appFalseStateString = "NotSet";
        private string appTrueStateString = "NotSet";
        private int pingIntervalSecs = 2;
        #endregion // vars

        #region property
        public string PipeName
        {
            get { return _pipe_name; }
            set { _pipe_name = value; }
        }

        public bool PowerLineActive
        {
            get { return powerLineNetworkActive; }
            set { powerLineNetworkActive = value; }
        }
        public bool IsConnected
        {
            get
            {
                if (clientPipe != null)
                {
                    return clientPipe.IsConnected;
                }
                else
                {
                    return false;
                }
            }
        }
        #endregion // property

        public void StartClient()
        {
            if (clientPipe == null)
            {
                appTrueStateString = AppOptions.GetAppOptions.AppTrueState;
                appFalseStateString = AppOptions.GetAppOptions.AppFalseState;
                pingIntervalSecs = AppOptions.GetAppOptions.PingIntervalSeconds;
                _pipe_name = AppOptions.GetAppOptions.StatusPipeConnectionName;
                //Task.Run(() => RunClientAsync(pipeClientTokenSrc.Token), pipeClientTokenSrc.Token);
                Task.Factory.StartNew(() => RunClientAsync(pipeClientTokenSrc.Token), pipeClientTokenSrc.Token, TaskCreationOptions.AttachedToParent, TaskScheduler.Current);
            }
            else
            {
                DCLogger.Information("Client already running.");
            }
        }


        public void StopClient()
        {
            if (clientPipe != null)
            {
                pipeClientTokenSrc.Cancel();
                if (clientPipe.IsConnected)
                {
                    clientPipe.Close();
                }
                clientPipe = null;
                System.Threading.Thread.Sleep(1000);
                pipeClientTokenSrc = new CancellationTokenSource();
            }
        }

        private async Task RunClientAsync(CancellationToken cts)
        {
            while (!cts.IsCancellationRequested)
            {
                clientPipe = new NamedPipeClientStream(".", _pipe_name, PipeDirection.InOut, PipeOptions.Asynchronous);
                DCLogger.Information("attempting to connect.");
                await clientPipe.ConnectAsync();
                DCLogger.Information("successful connection");

                swriter = new StreamWriter(clientPipe);
                swriter.AutoFlush = true;
                sreader = new StreamReader(clientPipe);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        if (powerLineNetworkActive)
                        {
                            WriteToPipe(appTrueStateString);
                        }
                        else
                        {
                            WriteToPipe(appFalseStateString);
                        }

                    }
                    catch (Exception e)
                    {
                        DCLogger.Error(e, "Write to Status Pipe");
                        break;
                    }                    
                    System.Threading.Thread.Sleep(pingIntervalSecs*1000);
                    //string line = await sreader.ReadLineAsync();
                    //RollingFileLogger.Information(string.Format("pipe client: Rd: {0}", line));
                    //if ((line == null) || (line.Equals("end")) || (line.Length == 0))
                    //{
                    //    break;
                    //    //throw new ApplicationException("Error");
                    //}
                }
            }

            //clientPipe.Close();
            //StopClient();
        }



        public void WriteToPipe(string message)
        {
            if (((clientPipe != null) && (clientPipe.IsConnected)) && (swriter != null))
            {
                swriter.WriteLine(message);
            }
            else
            {
                DCLogger.Information($"Not connected cannot write message {message} to pipe");
            }
        }

    }
}
