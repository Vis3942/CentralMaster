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
    }
}
