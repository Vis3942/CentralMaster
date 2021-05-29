using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster
{


    /// <summary>
    /// Specifies the meaning and relative importance of a log event.
    /// </summary>
    public enum LogEventLevel
    {
        /// <summary>
        /// Anything and everything you might want to know about
        /// a running block of code.
        /// </summary>
        Verbose,

        /// <summary>
        /// Internal system events that aren't necessarily
        /// observable from the outside.
        /// </summary>
        Debug,

        /// <summary>
        /// normal information
        /// </summary>
        Information,

        /// <summary>
        /// Service is degraded or endangered.
        /// </summary>
        Warning,

        /// <summary>
        /// Functionality is unavailable, invariants are broken
        /// or data is lost.
        /// </summary>
        Error,

        /// <summary>
        /// includes notification.. not implemented yet
        /// </summary>
        Fatal
    }

    public class DCLogger
    {
        readonly static string LOG_FILE = @"cm_app_log.txt";
        private static int MaxRolledLogCount = 5;
        private static int MaxLogSize = 1*1024*1024; // 1 * 1024 * 1024;  1M-10M
        private static string logTo = "file";
        private static LogEventLevel minimumLevel = LogEventLevel.Debug;
        private static string currentExecDirectory = null;
        private static string currentFileWithPath = null;
        public static void InitLogger()
        {
            logTo = AppOptions.GetAppOptions.LogTo;
            var loglevel_set = AppOptions.GetAppOptions.LogLevel.ToLower();
            switch (loglevel_set)
            {
                case "verbose":
                    minimumLevel = LogEventLevel.Verbose;
                    break;
                case "debug":
                    minimumLevel = LogEventLevel.Debug;
                    break;
                case "information":
                    minimumLevel = LogEventLevel.Information;
                    break;
                case "warning":
                    minimumLevel = LogEventLevel.Warning;
                    break;
                case "error":
                    minimumLevel = LogEventLevel.Error;
                    break;
                case "fatal":
                    minimumLevel = LogEventLevel.Fatal;
                    break;
            }
            MaxRolledLogCount = AppOptions.GetAppOptions.MaxCountFiles;
            MaxLogSize = (int)(AppOptions.GetAppOptions.MaxLogFileSizeMB * 1024 * 1024);
        }

        private static bool IsEnabled(LogEventLevel curr_level)
        {
            if ((int) curr_level >= (int)minimumLevel)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static void Fatal(string msg)
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg);
        }
        public static void Fatal(Exception e, string msg)
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg + System.Environment.NewLine + e.Message);
        }

        public static void Error(string msg)
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            Write("[ERR]: ", msg);
        }

        public static void Error(Exception e, string msg)
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            if (e != null)
            {
                // Todo: Write additional exception info along with message
                Write("[ERR]: ", msg + System.Environment.NewLine + e.Message);
            }
            else
            {
                Write("[ERR]: ", msg);
            }
        }

        public static void warning(string msg)
        {
            if (!IsEnabled(LogEventLevel.Warning))
            {
                return;
            }
            Write("[WRN]: ", msg);
        }

        public static void Information(string msg)
        {
            if (!IsEnabled(LogEventLevel.Information))
            {
                return;
            }
            Write("[INF]: ", msg);
        }

        public static void Debug(string msg)
        {
            if (!IsEnabled(LogEventLevel.Debug))
            {
                return;
            }
            Write("[DBG]: ", msg);
        }

        public static void Verbose(string msg)
        {
            if (!IsEnabled(LogEventLevel.Verbose))
            {
                return;
            }
            Write("[VRB]: ", msg);
        }



        public static void Write(string loglevelstr, string msg)
        {
            string logDateTimeStr = DateTime.Now.ToString();
            if (logTo.ToLower().Equals("file"))
            {
                lock (LOG_FILE) // multiple thread access control to file
                {
                    RollLogs();
                    File.AppendAllText(LOG_FILE, logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            else
            {
                System.Console.Write(logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine);
            }
        }

        private static void RollLogs()
        {
            try
            {
                if (currentFileWithPath == null)
                {
                    if (currentExecDirectory == null)
                    {
                        currentExecDirectory = Environment.CurrentDirectory;
                    }
                    currentFileWithPath = System.IO.Path.Combine(currentExecDirectory, LOG_FILE);
                }
                if (!System.IO.File.Exists(currentFileWithPath))
                {
                    return;
                }
                var length = new FileInfo(currentFileWithPath).Length;
                if (length > MaxLogSize)
                {
                    var path = Path.GetDirectoryName(currentFileWithPath);
                    var wildLogName = Path.GetFileNameWithoutExtension(currentFileWithPath) + "*" + Path.GetExtension(currentFileWithPath);
                    var bareLogFilePath = Path.Combine(path, Path.GetFileNameWithoutExtension(currentFileWithPath));
                    string[] logFileList = Directory.GetFiles(path, wildLogName, SearchOption.TopDirectoryOnly);
                    if (logFileList.Length > 0)
                    {
                        // only take files like logfilename.log and logfilename.0.log, so there also can be a maximum of 10 additional rolled files (0..9)
                        var rolledLogFileList = logFileList.Where(fileName => fileName.Length == (currentFileWithPath.Length + 2)).ToArray();
                        Array.Sort(rolledLogFileList, 0, rolledLogFileList.Length);
                        if (rolledLogFileList.Length >= MaxRolledLogCount)
                        {
                            File.Delete(rolledLogFileList[MaxRolledLogCount - 1]);
                            var list = rolledLogFileList.ToList();
                            list.RemoveAt(MaxRolledLogCount - 1);
                            rolledLogFileList = list.ToArray();
                        }
                        // move remaining rolled files
                        for (int i = rolledLogFileList.Length; i > 0; --i)
                            File.Move(rolledLogFileList[i - 1], bareLogFilePath + "." + i + Path.GetExtension(currentFileWithPath));
                        var targetPath = bareLogFilePath + ".0" + Path.GetExtension(currentFileWithPath);
                        // move original file
                        File.Move(currentFileWithPath, targetPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
