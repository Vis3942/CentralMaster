using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.json
{
    public class StartupSettings
    {
        public string plcPortName { get; set; }

        public string uiConnectionName { get; set; }
        public string StatusPipeConnectionName { get; set; }

        public string AppTrueState { get; set; }
        public string AppFalseState { get; set; }
        public int PingIntervalSeconds { get; set; }
        public string LogTo { get; set; }
        public string LogLevel { get; set; }
        public float MaxLogFileSizeMB { get; set; }
        public int MaxCountFiles { get; set; }
        public string LogFileName { get; set; }
        public int AdminPort { get; set; }
        public string CertificateName { get; set; }
        public string CmLogPath { get; set; }
        public string DsLogPath { get; set; }
        public string SsLogPath { get; set; }

        public string SupportingService { get; set; }
        public string ParametersDirectory { get; set; }
    }
}
