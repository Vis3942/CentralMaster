using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.json
{
    public class TransferCommandList
    {
        public List<TransferCommands> Commands { get; set; }

    }

    public class TransferCommands
    {
        public string NodeName { get; set; }
        public UICommands[] NodeCommands { get; set; }
    }

    public class UICommands
    {
        public string UUID { get; set; }
        public JsonCommandLite[] set_cmds { get; set; }
    }
}
