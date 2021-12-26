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
        public string[] ProductNames { get; set; }
        public string PanId { get; set; }
        public string XAddress { get; set; }
        public string PSK { get; set; }
        public int VendorId { get; set; }
        public string ProductId { get; set; }
        public string Modulation { get; set; }
        public string ToneMaskName { get; set; }
        public string ToneMaskValues { get; set; }
        public bool AGC { get; set; }
        public int Gain { get; set; }
        public int TransmitLevel { get; set; }
        public int DataReportingIntervalSecs { get; set; }
        public int PLCCommTimeoutSecs { get; set; }
        public int RxTimeoutMilliSecs { get; set; }
        public int NumberOfNodes { get; set; }
        public int TimerPerNodeSecs { get; set; }
        public int NodeResetTimeSecs { get; set; }
        public bool AttachHighPriority { get; set; }
        public int ConfirmationTimeoutSecs { get; set; }
        public int IsSubnetGateway { get; set; }
        public int UseEthernet { get; set; }
        public string SubnetDCIpAddress { get; set; }
        public int SubnetDCPort { get; set; }
        public string SubnetDCIdentifier { get; set; }
        public bool SendToCloud { get; set; }
        public string PipeConnectionName { get; set; }
    }
}
