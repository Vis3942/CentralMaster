using Microsoft.Extensions.Configuration;
//using Serilog;
//using SeriRollingFileLogger.Debugging;
//using SeriRollingFileLogger.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using LogUtils;
using CentralMaster.Communications;

namespace CentralMaster
{
	class MainClass
	{
		private static string AppConfigDirectory = "/home/debian/config/centralmaster";
		public static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				AppOptions.ParameterDirectory = args[0];
			}
			else
			{
				AppOptions.ParameterDirectory = AppConfigDirectory;
			}
			if (AppOptions.GetAppOptions != null)
            {
				DCLogger.InitLogger(
					log_to: AppOptions.GetAppOptions.LogTo,
					log_level: AppOptions.GetAppOptions.LogLevel,
					max_file_count: AppOptions.GetAppOptions.MaxCountFiles,
					max_file_size_mb: AppOptions.GetAppOptions.MaxLogFileSizeMB,
					log_file_name: AppOptions.GetAppOptions.LogFileName);

				try
				{
					AdminServer admin_server = new AdminServer();
					admin_server.StartServer();

					HostApp happ = new HostApp();
					happ.StartApplication();
				}
				catch (Exception ex)
				{
					DCLogger.Fatal(ex, "An unhandled exception occurred.");
				}
				finally
				{
					DCLogger.Information("Exiting main application");
					//RollingFileLogger.CloseAndFlush();
				}

				//HostApp happ = new HostApp();
				//happ.StartApplication();
			}
			else
            {
				// load default options and write error info to file and exit
				DCLogger.InitLogger(
					log_to: "file",
					log_level: "information",
					max_file_count: 10,
					max_file_size_mb: 5,
					log_file_name: "cm_app_log.txt"
				);
				DCLogger.Error($"Failed to load options from {AppConfigDirectory}");
			}

		}

	}
}
