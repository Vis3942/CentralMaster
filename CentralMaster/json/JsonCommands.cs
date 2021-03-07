using CentralMaster.json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster
{

    public class JsonCommands
    {
        public string hwinterface { get; set; }
        public int address { get; set; }
        public JsonCommand[] get_commands { get; set; }
        public JsonCommand[] set_commands { get; set; }
        public JsonCommand[] setup_commands { get; set; }
    }
}
