using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster.Communications
{
    public class AdminServer
    {
        #region vars
        private TcpListener adminServer = null;
        private string ipv4Addr;
        private int adminServerPort = -1;
        private X509Certificate serverCertificate = null;
        CancellationTokenSource cancelAdminServer = new CancellationTokenSource();
        private TcpClient currentTCPClient = null;
        readonly string DEFAULT_USER_FILE = "default_admin_cred.bin";
        readonly string SET_USER_FILE = "user_admin_cred.bin";
        bool userLoggedIn = false;
        //readonly byte[] eof_arr = { 0x1A };
        //readonly string end_of_cmd = "<EOC>";
        byte[] end_cmd_b = System.Text.ASCIIEncoding.ASCII.GetBytes("<EOC>");

        #endregion //vars

        #region property
        public string IpAddressStr
        {
            get { return ipv4Addr; }
            set { ipv4Addr = value; }
        }

        #endregion // property

        public AdminServer()
        {
            this.IpAddressStr = IPAddress.Any.ToString();
            LoadConfig();
        }

        public AdminServer(string ipaddr)
        {
            this.IpAddressStr = ipaddr;
            LoadConfig();
        }

        public void LoadConfig()
        {
            adminServerPort = AppOptions.GetAppOptions.AdminPort;
            string cert_file_name = AppOptions.GetAppOptions.CertificateName;
            // load certificate if file found
            string withpath_cert_file_name = System.IO.Path.Combine(AppOptions.ParameterDirectory, 
                "certs", cert_file_name + ".pfx");
            DCLogger.Verbose($"certificate file at {withpath_cert_file_name}");
            if (File.Exists(withpath_cert_file_name))
            {
                loadCertificate(withpath_cert_file_name);
            }
            else
            {
                DCLogger.Error(string.Format("Looking for server certificate =>[{0}]", cert_file_name));
                //RollingFileLogger.Error("Server certificate not found, exiting application.");
                //System.Environment.Exit(2);
                serverCertificate = null;
            }
        }

        private void loadCertificate(string cert_name)
        {
            try
            {
                serverCertificate = X509Certificate.CreateFromCertFile(cert_name);
            }
            catch (Exception excp)
            {
                DCLogger.Error(excp, "Loading certificate:");
                //Console.WriteLine(string.Format("Error loading certificate => {0}", cert_name));
                //Console.WriteLine(excp.Message);
                //RollingFileLogger.Error("Exiting application.");
                //System.Environment.Exit(1);
                serverCertificate = null;
            }
        }

        public void StartServer()
        {
            CancellationTokenSource cancelationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() =>  StartAdminServerAsync(), cancelationTokenSource.Token, 
                TaskCreationOptions.AttachedToParent, TaskScheduler.Current);

        }

        private void StartAdminServerAsync()
        {
            adminServer = new TcpListener(IPAddress.Parse(ipv4Addr), adminServerPort);
            adminServer.Start();
            try
            {
                while (true)
                {
                    try
                    {
                        var clientTask = adminServer.AcceptTcpClientAsync();
                        clientTask.Wait();
                        if (!clientTask.IsFaulted)
                        {
                            currentTCPClient = clientTask.Result;
                            userLoggedIn = false;
                            var task_proto = Task.Factory.StartNew(() => AdminProtocolAsync(currentTCPClient), cancelAdminServer.Token, 
                                TaskCreationOptions.AttachedToParent, TaskScheduler.Current);
                            task_proto.Wait();
                        }
                        else
                        {
                            DCLogger.Error("Client accept error at Admin Server Task. ");
                        }
                    }
                    catch (Exception eaccp)
                    {
                        DCLogger.Error(eaccp, "Performing Admin Server accept and protocol.");
                    }
                }
            }
            finally
            {
            }
        }

        private void AdminProtocolAsync(TcpClient currentTCPClient)
        {
            var ip = currentTCPClient.Client.RemoteEndPoint.ToString();
            DCLogger.Information($"UI client ({ip}) connected");
            var buf_rd = new byte[4096];
            var buf_wr = new byte[4096];

            var stream = currentTCPClient.GetStream();
            SslStream uiSslStream = new SslStream(stream, false);
            try
            {
                uiSslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                while (true)
                {
                    var command = ReadMessage(uiSslStream);
                    if ( (command == null) || (command.Length <= 0) )
                    {
                        break;
                    }
                    var use_cmd = command.ToLower();
                    if (use_cmd.StartsWith("login"))
                    {
                        DCLogger.Verbose("Received Login command");
                        PerformLogin(command);
                        if (userLoggedIn)
                        {
                            uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("login:success"));
                            uiSslStream.Write(end_cmd_b);
                        }
                        else
                        {
                            uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("login:failed"));
                            uiSslStream.Write(end_cmd_b);
                        }
                    }
                    else
                    {
                        if (userLoggedIn)
                        {
                            // only then accept other commands
                            if (use_cmd.StartsWith("updateadminlogin"))
                            {
                                DCLogger.Verbose("Received update Admin Login command");
                                var update_sts = UpdateAdminPassword(command);
                                if (update_sts)
                                {
                                    uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("updateadminlogin:success"));
                                    uiSslStream.Write(end_cmd_b);
                                }
                                else
                                {
                                    uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("updateadminlogin:failed"));
                                    uiSslStream.Write(end_cmd_b);
                                }
                            }
                            else if (use_cmd.StartsWith("logout"))
                            {
                                userLoggedIn = false;
                                DCLogger.Verbose($"user login {userLoggedIn}");
                                // close connection and return
                            }
                            else if (use_cmd.StartsWith("getparameters"))
                            {
                                DCLogger.Verbose(command);
                                string[] app_parameters = File.ReadAllLines("plcdc_params.txt");
                                StringBuilder sb = new StringBuilder();
                                foreach (var line in app_parameters)
                                {
                                    if (line.StartsWith("roundrobinwaitsecs"))
                                    {
                                        sb.Append(line.Trim());
                                        sb.Append(",");
                                    }
                                }
                                // remove last char it is always ","
                                var param_str = sb.ToString().Substring(0, sb.Length - 1);
                                uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes($"getparameters:{param_str}"));
                                uiSslStream.Write(end_cmd_b);
                            }
                            else if (use_cmd.StartsWith("setparameters"))
                            {
                                DCLogger.Verbose(command);
                                bool update_param_sts = UpdateParameters(use_cmd);
                                if (update_param_sts)
                                {
                                    uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("setparameters:success"));
                                    uiSslStream.Write(end_cmd_b);
                                    Environment.Exit(0);
                                }
                                else
                                {
                                    uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("setparameters:failed"));
                                    uiSslStream.Write(end_cmd_b);
                                }
                            }
                            else if (use_cmd.StartsWith("getlogfiles"))
                            {
                                DCLogger.Verbose(command);
                                TransmitLogFiles(use_cmd, uiSslStream);
                            }
                            else if (use_cmd.StartsWith("updatefirmware"))
                            {
                                DCLogger.Verbose(command);
                                UpdateFirmware(command, uiSslStream);
                            }
                            else
                            {
                                DCLogger.Information($"Invalid command received {command}");
                            }
                        }
                        else
                        {
                            uiSslStream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("Not logged in."));
                            uiSslStream.Write(end_cmd_b);
                        }
                    }
                }
            }
            catch (AuthenticationException ae)
            {
                DCLogger.Error(ae, "in admin client authentication");
                if (ae.InnerException != null)
                {
                    DCLogger.Error($"Inner exception: {ae.InnerException.Message}");
                }
                DCLogger.Error("Authentication failed - closing the connection.");
            }
            catch (Exception e)
            {
                DCLogger.Error(e, "During client authentication");
            }
            finally
            {
                uiSslStream.Close();
                DCLogger.Information("Closing Connection to Admin Client");
                currentTCPClient.Close();
                currentTCPClient.Client.Close();
                currentTCPClient.Dispose();
                currentTCPClient = null;
            }


        }

        private void TransmitLogFiles(string use_cmd, SslStream send_logs_stream)
        {
            // expected getlogfiles:cm, getlogfiles:ds, getlogfiles:ss
            // write number of files
            // for (i = 1 to number of files); do; read all lines, write filename, write one by one line, then send EOF
            var log_4_app = use_cmd.Replace("getlogfiles:", "").Trim();
            string[] logFileList = null;
            try
            {
                if (log_4_app.Equals("cm"))
                {
                    var wildLogName = "cm_app_log*txt";
                    var path_1 = AppOptions.GetAppOptions.CmLogPath;
                    logFileList = Directory.GetFiles(path_1, wildLogName, SearchOption.TopDirectoryOnly);
                    DCLogger.Verbose($"cm log path {path_1}, num logs {logFileList.Length}");
                }
                else if (log_4_app.Equals("ds"))
                {
                    var wildLogName = "ds_app_log*txt";
                    var path_2 = AppOptions.GetAppOptions.DsLogPath;
                    logFileList = Directory.GetFiles(path_2, wildLogName, SearchOption.TopDirectoryOnly);
                    DCLogger.Verbose($"ds log path {path_2}, num logs {logFileList.Length}");
                }
                else if (log_4_app.Equals("ss"))
                {
                    var wildLogName = "ss_app_log*txt";
                    var path_3 = AppOptions.GetAppOptions.SsLogPath;
                    logFileList = Directory.GetFiles(path_3, wildLogName, SearchOption.TopDirectoryOnly);
                    DCLogger.Verbose($"ss log path {path_3}, num logs {logFileList.Length}");
                }
            }
            catch (Exception e)
            {
                DCLogger.Error(e, $"locating log files for {log_4_app}");
                send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("getlogfiles:failed"));
                send_logs_stream.Write(end_cmd_b);
                return;
            }
            if (logFileList != null)
            {
                DCLogger.Verbose($"logFileList count {logFileList.Length}");
            }
            else
            {
                DCLogger.Verbose($"logFileList IS NULL");
            }
            if ( (logFileList == null) || (logFileList.Length <= 0) )
            {
                send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("filecount:0"));
                send_logs_stream.Write(end_cmd_b);

                DCLogger.Information($" No Log files in {log_4_app}");
            }
            else
            {
                send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes($"filecount:{logFileList.Length}"));
                send_logs_stream.Write(end_cmd_b);
                foreach (var tmpfile in logFileList)
                {
                    var tosend_fname = Path.GetFileName(tmpfile);
                    send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(tosend_fname));
                    string[] tmp_lines = File.ReadAllLines(tmpfile);
                    foreach (var tmp_line in tmp_lines)
                    {
                        send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(tmp_line));
                        send_logs_stream.Write(end_cmd_b);
                    }
                    send_logs_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("<EOF>")); // end of file
                    send_logs_stream.Write(end_cmd_b);
                    DCLogger.Verbose($"Completed Upload {tmpfile}");
                }
            }
        }

        private bool UpdateParameters(string use_cmd)
        {
            // expect "setparameters:paramname=value, paramname2=value2"
            string params_str = use_cmd.Replace("setparameters:", "");
            if (params_str.Length == 0)
            {
                return false;
            }
            string [] param_info = params_str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] app_parameters = File.ReadAllLines("plcdc_params.txt");
            for (int ii=0; ii < app_parameters.Length; ii++)
            {
                foreach (var tmp_param in param_info)
                {
                    string[] up_param_name = tmp_param.Split(new char[] { '=' });
                    //Console.WriteLine($"len {up_param_name.Length}: var name {up_param_name[0]}");
                    if (up_param_name.Length == 2)
                    {
                        if (app_parameters[ii].Trim().ToLower().StartsWith(up_param_name[0].Trim()))
                        {
                            DCLogger.Verbose($"Updating param {tmp_param}");
                            app_parameters[ii] = tmp_param.Trim();
                            break;
                        }
                    }
                }
            }
            File.WriteAllLines("plcdc_params.txt", app_parameters);
            DCLogger.Debug("Param file plcdc_params.txt written");
            return true;
        }

        private bool UpdateAdminPassword(string use_cmd)
        {
            // expect "updateadminlogin:username, orig_password, new_password"
            //string[] user_info = use_cmd.Replace("updateadminlogin:", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] user_info = use_cmd.Substring(17).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (user_info.Length != 3)
            {
                return false;
            }
            if (!user_info[0].Trim().ToLower().Equals("admin"))
            {
                return false;
            }
            byte[] comp_h_b = SHA256.HashData(Encoding.UTF8.GetBytes(user_info[1].Trim()));
            byte[] admin_h_b = GetAdmihHash();
            DCLogger.Verbose($"ua: computed {BitConverter.ToString(comp_h_b)}");
            DCLogger.Verbose($"ua: read     {BitConverter.ToString(admin_h_b)}");
            if (Enumerable.SequenceEqual(comp_h_b, admin_h_b))
            {
                // update new password
                byte[] new_pwd_b = SHA256.HashData(Encoding.UTF8.GetBytes(user_info[2].Trim()));
                var user_admin_file = System.IO.Path.Combine(AppOptions.ParameterDirectory, 
                    "certs", SET_USER_FILE);
                File.WriteAllBytes(user_admin_file, new_pwd_b);
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte[] GetAdmihHash()
        {
            var user_admin_file = System.IO.Path.Combine(AppOptions.ParameterDirectory, "certs", SET_USER_FILE);
            var default_admin_file = System.IO.Path.Combine(AppOptions.ParameterDirectory, "certs", DEFAULT_USER_FILE);
            if (System.IO.File.Exists(user_admin_file))
            {
                byte[] set_user_b = File.ReadAllBytes(user_admin_file);
                return set_user_b;
            }
            else
            {
                byte[] def_user_b = File.ReadAllBytes(default_admin_file);
                return def_user_b;
            }
        }

        private void PerformLogin(string command)
        {
            // expect login: username, password
            string[] user_info = command.Substring(6).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (user_info.Length != 2)
            {
                userLoggedIn = false;
                return;
            }
            byte[] comp_h_b =  SHA256.HashData(Encoding.UTF8.GetBytes(user_info[1].Trim()));
            byte[] admin_h_b = GetAdmihHash();
            DCLogger.Verbose("computed hash");
            DCLogger.Verbose(BitConverter.ToString(comp_h_b));
            DCLogger.Verbose("loaded hash");
            DCLogger.Verbose(BitConverter.ToString(admin_h_b));
            if (Enumerable.SequenceEqual(comp_h_b, admin_h_b))
            {
                userLoggedIn = true;
            }
            else
            {
                userLoggedIn = false;
            }
            DCLogger.Verbose($"user login {userLoggedIn}");
        }

        private void UpdateFirmware(string use_cmd, SslStream update_stream)
        {
            // expected updatefirmware:cm, updatefirmware:ds, updatefirmware:ss
            // write "startupdate" to ui
            // read file:filename: length of file in bytes as string
            //    1. read bytes: each packet will contain "<pkt size of bytes as int:4 bytes, pkt_size includes this 4 bytes>, followed by byte array"
            //    2. write bytes to a file
            //    3. write "updatebytes:success" to ui
            //    repeat 1..3 till packet received is 0x00 0x00 0x00 0x00 (length is 0) end of file
            // write "endupdate:success" to ui
            var update_app_type = use_cmd.Substring("updatefirmware:".Length).Trim();
            var update_app_filename = "notset";
            update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("startupdate:success"));
            update_stream.Write(end_cmd_b);
            var file_info_str = ReadMessage(update_stream);
            DCLogger.Information(file_info_str);
            if (file_info_str.ToLower().StartsWith("file:"))
            {
                var tmpstr = file_info_str.Substring(5);
                var tmpidx = tmpstr.IndexOf(':');
                if (tmpidx <= 0)
                {
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("endupdate:invalidfileinfo"));
                    update_stream.Write(end_cmd_b);
                    return;
                }
                var wr_fname = tmpstr.Substring(0, tmpidx);
                bool sts = false;
                Int64 wr_filesize;
                sts = Int64.TryParse(tmpstr.Substring(tmpidx + 1), out wr_filesize);
                if (!sts)
                {
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("endupdate:invalidfilesize"));
                    update_stream.Write(end_cmd_b);
                    return;
                }
                Int64 recv_count = 0;
                bool done_recv = false;
                DCLogger.Information($"updatefile: {wr_fname}, size: {wr_filesize.ToString()}");
                var path_to_file = System.IO.Path.Combine("/tmp", wr_fname);
                try
                {
                    using (FileStream wr_stream = new FileStream(path_to_file, FileMode.Create, FileAccess.Write))
                    {
                        while (!done_recv)
                        {
                            byte[] read_data_b = ReadDataBytes(update_stream);
                            if (read_data_b == null) 
                            {
                                done_recv = true;
                            }
                            else
                            {
                                if (read_data_b.Length > 0)
                                {
                                    recv_count += ((Int64)read_data_b.Length);
                                    wr_stream.Write(read_data_b);
                                }
                                else
                                {
                                    done_recv = true;
                                }
                                update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("updatebytes:success"));
                                update_stream.Write(end_cmd_b);
                            }
                        }
                    }
                    if (recv_count != wr_filesize)
                    {
                        throw new Exception($"Invalid file {wr_fname} received, expected {wr_filesize}, recvd {recv_count}");
                    }
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("startupdate:file received"));
                    update_stream.Write(end_cmd_b);
                    update_app_filename = wr_fname;
                }
                catch (Exception e)
                {
                    DCLogger.Error(e, "Write Update Firmware to file");
                    if (System.IO.File.Exists(path_to_file))
                    {
                        System.IO.File.Delete(path_to_file);
                    }
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("endupdate:exception"));
                    update_stream.Write(end_cmd_b);
                }
            }
            else
            {
                update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("endupdate:protocolerror"));
                update_stream.Write(end_cmd_b);
            }

            DCLogger.Information($"finished firmware upload for {update_app_type}, file {update_app_filename}");
            // TODO: if var_app_name is "cm", "ds" or "ss

            int check_fw_exitcode = UpdateUtils.CheckFirmware(update_app_filename);
            bool check_sts = false;
            if (check_fw_exitcode == 0)
            {
                check_sts = true;
            }
            else
            {
                check_sts = false;
            }
            DCLogger.Information($"Check Firmware Succ => {check_sts}");
            update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("interupdate:check done"));
            update_stream.Write(end_cmd_b);
            if (check_sts)
            {
                var upgrade_filename = update_app_filename.Replace("_fw", "");
                int upgrade_fw_exitcode = UpdateUtils.UpgradeFirmware(update_app_type, upgrade_filename);
                if (upgrade_fw_exitcode == 0)
                {
                    check_sts = true;
                }
                else
                {
                    check_sts = false;
                }
                DCLogger.Information($"Upgrade Firmware Succ => {check_sts}");
                if (check_sts)
                {
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes("endupdate:success"));
                    update_stream.Write(end_cmd_b);
                }
                else
                {
                    update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes($"endupdate:upgradeerror code{upgrade_fw_exitcode}"));
                    update_stream.Write(end_cmd_b);
                }
            }
            else
            {
                update_stream.Write(System.Text.ASCIIEncoding.ASCII.GetBytes($"endupdate:checkerror code {check_fw_exitcode}"));
                update_stream.Write(end_cmd_b);
            }

        }

        private string ReadMessage(SslStream rd_sslStream)
        {
            // Read the  message sent by the client.
            // The client signals the end of the message using the
            // "<EOF>" marker.
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                // Read the client's test message.
                bytes = rd_sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                // Check for EOF or an empty message.
                if (messageData.ToString().IndexOf("<EOC>") != -1)
                {
                    break;
                }
            } while (bytes != 0);

            return messageData.ToString().Replace("<EOC>", "");

        }

        private byte[] ReadDataBytes(SslStream rd_sslStream)
        {
            // Read the  bytes array sent by the client.
            // The client sends data byte array as [int:4 bytes len] + [actual len bytes] 
            // return only the actual bytes: drop the first 4 length bytes
            // if length of data is zero, return null;
            byte[] buffer = new byte[100*1024 + 4];
            int bytes_rd = -1;
            int current_len = 0;
            do
            {
                // Read the client's test message.
                bytes_rd = rd_sslStream.Read(buffer, current_len, buffer.Length-current_len);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                // Check for length of bytes or empty message.
                if (bytes_rd != -1)
                {
                    if ((current_len + bytes_rd) >= 4)
                    {
                        var len_check = BitConverter.ToInt32(buffer, 0);
                        current_len += bytes_rd;
                        if (current_len  >= len_check)
                        {
                            break;
                        }
                        else if (current_len == 0)
                        {
                            // end of file
                            break;
                        }
                    }
                }
            } while (bytes_rd != 0);
            byte[] ret_b = null;
            if (bytes_rd != 0)
            {
                if (current_len != 0)
                {
                    // discard the length bytes
                    ret_b = new byte[current_len - 4];
                    Array.Copy(buffer, 4, ret_b, 0, current_len - 4);
                }
                else
                {
                    ret_b = new byte[0];
                }
            }
            return ret_b;

        }


        private void AcceptClientAsync(TcpListener adminServer)
        {

        }
    }
}
