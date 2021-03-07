using System;

namespace CentralMaster.json
{
    public class RRCommand
    {
        public string uuid { get; set; }
        public string device_name { get; set; }
        public int channel { get; set; }
        public string data_names { get; set; }
        public string command_name { get; set; }
        public string command_type { get; set; }  // values "config"; "init"; "data"
    }
}
