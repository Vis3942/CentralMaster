using System.Text;
using System;

namespace CentralMaster 
{

    public class SensorData
    {
        //public string node {get; set;}
        //public string uuid {get; set;}
        //public string ts {get; set;}
        public string PartitionKey {get; set; } // is uuid
        public string RowKey { get; set; } // unix timestamp.to_s + (100*i).to_s
        //public string DataType {get; set;}
        public string Name {get; set;}
        public string Value {get;set;}

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("PartitionKey = {0}, ", PartitionKey);
            sb.AppendFormat("RowKey = {0}, ", RowKey);
            //sb.AppendFormat("DataType = {0}, ", DataType);
            sb.AppendFormat("Name = {0}, ", Name);
            sb.AppendFormat("Value = {0}, ", Value);
            return sb.ToString();
        }
    }

}