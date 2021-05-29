using LogUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.Communications
{
    public class UpdateUtils
    {
        public static int CheckFirmware(string fw_filename)
        {
            var process = new Process();
            process.EnableRaisingEvents = true; // to avoid [defunct] sh processes
            //process.StartInfo.FileName = "/home/debian/scripts/check_firmware.sh";
            string check_fw_filename = System.IO.Path.Combine(Environment.CurrentDirectory, "scripts", "check_firmware.sh");
            if (!System.IO.File.Exists(check_fw_filename))
            {
                DCLogger.Error("Firmware check file not found");
                return -10001;
            }
            process.StartInfo.FileName = check_fw_filename;
            string cert_path = System.IO.Path.Combine(Environment.CurrentDirectory, "certs", "update-qyeidymn-public-key.pem");
            //process.StartInfo.Arguments = $"{fw_filename}  /home/debian/csharp/centralmaster/current/certs/update-qyeidymn-public-key.pem";
            if (!System.IO.File.Exists(cert_path))
            {
                DCLogger.Error("upgrade certificate not found");
                return -10002;
            }
            process.StartInfo.Arguments = $"{fw_filename}  {cert_path}";
            DCLogger.Verbose($"process cmd {check_fw_filename}, args: {process.StartInfo.Arguments}");
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit(300 * 1000);
            if (process.HasExited)
            {
                DCLogger.Information("Check FW Exit code: " + process.ExitCode);
                return process.ExitCode;
            }
            else
            {
                DCLogger.Error("Check Firmware process did not finish in 5 minutes");
                return -110;
            }
        }

        public static int UpgradeFirmware(string app_type, string upgrade_file )
        {
            var process2 = new Process();
            process2.EnableRaisingEvents = true; // to avoid [defunct] sh processes
            //process2.StartInfo.FileName = "/home/debian/scripts/upgrade_firmware.sh";
            string upgrade_fw_filename = System.IO.Path.Combine(Environment.CurrentDirectory, "scripts", "upgrade_firmware.sh");
            if (!System.IO.File.Exists(upgrade_fw_filename))
            {
                DCLogger.Error("Firmware upgrade file not found");
                return -10001;
            }
            process2.StartInfo.FileName = upgrade_fw_filename;
            //process2.StartInfo.Arguments = "/home/debian/csharp/statusservices  /tmp/StatusServices.tar.gz  StatusServices";
            string main_app_name = "";
            string app_path = "";
            if (app_type.ToLower().Equals("ss"))
            {
                main_app_name = "StatusServices";
                app_path = AppOptions.GetAppOptions.SsLogPath;
            }
            else if (app_type.ToLower().Equals("ds"))
            {
                main_app_name = "DataServices";
                app_path = AppOptions.GetAppOptions.DsLogPath;
            }
            else if (app_type.ToLower().Equals("cm"))
            {
                main_app_name = "CentralMaster";
                app_path = AppOptions.GetAppOptions.CmLogPath;
            }
            else
            {
                DCLogger.Error($"Unknown application type {app_type}. Failed upgrade.");
                return -10002;
            }
            if (app_path.EndsWith(@"/current"))
            {
                app_path = app_path.Substring(0, (app_path.Length - @"/current".Length));
            }
            DCLogger.Verbose($"App path set to {app_path}");
            process2.StartInfo.Arguments = $"{app_path} {upgrade_file} {main_app_name}";
            process2.StartInfo.UseShellExecute = true;
            process2.StartInfo.CreateNoWindow = true;
            process2.Start();
            process2.WaitForExit(300 * 1000);
            if (process2.HasExited)
            {
                DCLogger.Information("Upgrade FW Exit code: " + process2.ExitCode);
                return process2.ExitCode;
            }
            else
            {
                DCLogger.Error("Upgrade firmware did not finish in 5 minutes");
                return -110;
            }
        }

    }
}
