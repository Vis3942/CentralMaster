using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
//using Newtonsoft.Json;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using UtilsLibrary;
using System.Text.Json;
using CentralMaster.json;
using System.IO;

namespace CentralMaster
{
    public class DeviceData
    {
        // device name, and relevant commands
        private static ConcurrentDictionary<string, JsonCommands> devices = new ConcurrentDictionary<string, JsonCommands>();

        private static ConcurrentDictionary<string, bool> reloaded = new ConcurrentDictionary<string, bool>();

        //public static System.Reflection.MethodInfo LoadMethodFromAssembly(string ass_dir, string ass_name, string func_name)
        //{
        //    // func_name format "classname.methodname"
        //    // Load the assembly
        //    string dll_path;
        //    if ((ass_dir == null) || (ass_dir.Length == 0))
        //    {
        //        dll_path = Path.Combine(Directory.GetCurrentDirectory(), ass_name + ".dll");
        //    }
        //    else
        //    {
        //        dll_path = Path.Combine(Directory.GetCurrentDirectory(), ass_dir, ass_name + ".dll");
        //    }
        //    if (!System.IO.File.Exists(dll_path))
        //    {
        //        Console.WriteLine($"Failed to locate assembly {dll_path}");
        //        return null;
        //    }
        //    string[] tmp_str = func_name.Split('.');
        //    if (tmp_str.Length != 2)
        //    {
        //        return null;
        //    }
        //    byte[] assm_bytes = File.ReadAllBytes(dll_path);
        //    System.Reflection.Assembly tmp_loc_asm = System.Reflection.Assembly.Load(assm_bytes);
        //    //System.Reflection.Assembly tmp_loc_asm = System.Reflection.Assembly.LoadFrom(dll_path); // AssemblyLoadContext.Default.LoadFromAssemblyPath(dll_path);
        //    //string tmpstr = ass_name + ".ConvertData";
        //    //int tmpcount = tmp_loc_asm.GetType(tmpstr).GetMethods().Length;
        //    //Console.WriteLine($"asm getmethods count {tmpcount}");
        //    //string tmp_asm_name = tmp_loc_asm.GetName().FullName;
        //    //Console.WriteLine($"assembly name = {tmp_asm_name}");
        //    //for (int ii = 0; ii < tmpcount; ii ++)
        //    //{
        //    //    Console.WriteLine($"method {ii} name => {tmp_loc_asm.GetType($"{ ass_name }.{tmp_str[0]}").GetMethods()[ii].Name}");
        //    //}
        //    System.Reflection.MethodInfo result = tmp_loc_asm.GetType($"{ ass_name }.{tmp_str[0]}").GetMethod(tmp_str[1]);
        //    if (result == null)
        //    {
        //        Console.WriteLine($"Failed to load method {func_name} from assembly {ass_name}");
        //    }
        //    return result;
        //}

        public static void LoadDevice(string dev_name)
        {
            if (!devices.ContainsKey(dev_name))
            {
                try
                {
                    string device_json_fname = System.IO.Path.Combine("Devices", dev_name+ ".json");
                    if (System.IO.File.Exists(device_json_fname))
                    {
                        Console.WriteLine(string.Format("Loading device info from file {0}", device_json_fname));
                        string json_str = System.IO.File.ReadAllText(device_json_fname);
                        try
                        {
                            JsonCommands dev_json = JsonSerializer.Deserialize<JsonCommands>(json_str);
                            //JsonCommands dev_json = JsonConvert.DeserializeObject<JsonCommands>(json_str);
                            // debug
                            if (dev_json != null)
                            {
                                Console.WriteLine($"dev_json hwinterfacee {dev_json.hwinterface}");
                                Console.WriteLine($"dev_json get_cmds count = {dev_json.get_commands.Length}");
                            }
                            else
                            {
                                Console.WriteLine("dev_json is null");
                            }
                            foreach (var json_cmd in dev_json.get_commands)
                            {
                                Console.WriteLine($"json_cmd name = {json_cmd.name}");
                                Console.WriteLine($"json_cmd response = {json_cmd.response}");

                                if (json_cmd.response.Length > 0)
                                {
                                    json_cmd.compute_values = json.JsonUtils.LoadMethodFromAssembly("Devices", dev_name, json_cmd.response);
                                }
                            }
                            bool sts = devices.TryAdd(dev_name, dev_json);
                            // debug
                            Console.WriteLine($"status adding {dev_name} to devices is {sts}");
                        }
                        catch (System.Exception jle)
                        {
                            Console.WriteLine($"LoadJson: {jle.Message}");
                            Console.WriteLine(jle.StackTrace);
                        }
                    }
                }
                catch (Exception)
                {
                    // failed to load device json "<dev_name>.json"
                }
            }

        }

        public static JsonCommands GetDeviceInfo(string dev_name)
        {
            if (!devices.ContainsKey(dev_name))
            {
                LoadDevice(dev_name);
            }

            if (devices.ContainsKey(dev_name))
            {
                return devices[dev_name];
            }
            else
            {
               return null;
            }
        }

        public static string GetHWInterfaceForDevice(string dev_name)
        {
            if (!devices.ContainsKey(dev_name))
            {
                LoadDevice(dev_name);
            }
            Console.WriteLine($"dev_name {dev_name}");
            foreach (var tmpdev in devices.Keys)
            {
                Console.WriteLine($"{tmpdev}");
            }
            if (devices.ContainsKey(dev_name))
            {
                //debug
                Console.WriteLine($"hw interface in GetHWInterfaceForDevice func is => {devices[dev_name].hwinterface}");
                return devices[dev_name].hwinterface;
            }
            else
            {
                return "";
            }
        }

        public static byte GetAddress(string dev_name)
        {
            byte toret_val;
            if (!devices.ContainsKey(dev_name))
            {
                LoadDevice(dev_name);
            }
            if (devices.ContainsKey(dev_name))
            {
                JsonCommands tmp_jcmds = devices[dev_name];
                toret_val = (byte)tmp_jcmds.address;
            }
            else
            {
                toret_val = 0;
            }
            return toret_val;
        }

        public static JsonCommand GetCommand(string dev_name, string cmd_name)
        {
            if (!devices.ContainsKey(dev_name))
            {
                LoadDevice(dev_name);
            }
            if (devices.ContainsKey(dev_name))
            {
                JsonCommands tmp_jcmds = devices[dev_name];
                JsonCommand ret_jcmd = null;
                foreach (JsonCommand tmp_jc in tmp_jcmds.get_commands)
                {
                    if (tmp_jc.name.Equals(cmd_name))
                    {
                        ret_jcmd = tmp_jc;
                        break;
                    }
                }
                return ret_jcmd;
            }
            else
            {
                return null;
            }
        }

        public static List<ResponseData> ComputeValues(string dev_name, string cmd_name, byte[] response_b, int proc_data_start_idx, int proc_data_len)
        {
            if (!devices.ContainsKey(dev_name))
            {
                LoadDevice(dev_name);
            }
            if (devices.ContainsKey(dev_name))
            {
                JsonCommands tmp_jcmds = devices[dev_name];
                JsonCommand ret_jcmd = null;
                foreach (JsonCommand tmp_jc in tmp_jcmds.get_commands)
                {
                    if (tmp_jc.name.Equals(cmd_name))
                    {
                        ret_jcmd = tmp_jc;
                        break;
                    }
                }
                if (ret_jcmd == null)
                {
                    return null;
                }
                else 
                {
                    // compute and return values
                    List<ResponseData> resp_sd = (List<ResponseData>)ret_jcmd.compute_values.Invoke(null, new object[] { response_b, proc_data_start_idx, proc_data_len });
                    return resp_sd;                    
                }
            }
            else
            {
                return null;
            }
        }
    }

}