using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.json
{
    public class JsonCommand
    {
        public string name { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public string value { get; set; }
        public string response { get; set; }
        public MethodInfo compute_values { get; set; }
        public JsonData[] data { get; set; }
    }
}
