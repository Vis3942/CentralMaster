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
		private static readonly StreamWriter tw = new StreamWriter("serilog_errs.txt");
		public static void Main(string[] args)
		{
			//SetupStaticLogger();
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
				//RollingFileLogger.CloseAndFlush();
			}

			//HostApp happ = new HostApp();
			//happ.StartApplication();
		}

		//private static void SetupStaticLogger()
		//{
			
		//	//SelfRollingFileLogger.Enable(Console.Out);
		//	SelfLog.Enable(tw);

		//	if (AppOptions.GetAppOptions.LogTo.ToLower().Equals("console"))
		//	{
		//		//var configuration = new ConfigurationBuilder()
		//		//	.AddJsonFile("log_settings_console.json")
		//		//	.Build();

		//		//RollingFileLogger.Logger = new LoggerConfiguration()
		//		//	.ReadFrom.Configuration(configuration)
		//		//	.CreateLogger();

		//		//RollingFileLogger.Logger = new LoggerConfiguration()
		//		//	.WriteTo.Console(theme: ConsoleTheme.None)
		//		//	.CreateLogger();
		//	}
		//	else
		//	{
		//		//var configuration = new ConfigurationBuilder()
		//		//	.AddJsonFile("log_settings_file.json")
		//		//	.Build();

		//		//RollingFileLogger.Logger = new LoggerConfiguration()
		//		//	.ReadFrom.Configuration(configuration)
		//		//	.CreateLogger();
		//		//RollingFileLogger.Logger = new LoggerConfiguration()
		//		//	.WriteTo.Async(a => a.File("cm_app_RollingFileLogger.txt"))
		//		//	.CreateLogger();
		//	}

		//	//Console.WriteLine($"Main: Logger created is {RollingFileLogger.Logger.ToString()}");
		//}

	}
}
