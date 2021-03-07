using CentralMaster.json;
using System;
using System.Reflection;

namespace CentralMaster
{

    public class NodeDevices
    {
        public DeviceInfo [] devices {get; set; }

        public ComputedData computedData { get; set; }
    }

    public class DeviceInfo
    {

        public string uuid { get; set; }
        public string device { get; set; }
        public int channel { get; set; }
        public string config_command { get; set; }

        public string init_command {get; set;}

        public DataCommand[] data_commands { get; set; }


    }

    public class DataCommand 
    {
        public string name {get; set;}
        public string data_names { get; set; }
        public int order { get; set; }
    }

    public class ComputedData
    {
        public string name { get; set; }
        public string uuid { get; set; }
        public string description { get; set; }
        public string compute { get; set; }
        public string data_names { get; set; }
        public MethodInfo compute_values { get; set; }
        public JsonData[] data { get; set; }

    }

}

