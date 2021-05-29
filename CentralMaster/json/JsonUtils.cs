using System;
//using System.Collections.Generic;
using System.IO;
//using System.Linq;
//using System.Runtime.Loader;
//using System.Text;
//using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster.json
{
    public class JsonUtils
    {
        public static System.Reflection.MethodInfo LoadMethodFromAssembly(string ass_dir, string ass_name, string func_name)
        {
            // func_name format "classname.methodname"
            // Load the assembly
            string dll_path;
            if ((ass_dir == null) || (ass_dir.Length == 0))
            {
                dll_path = Path.Combine(Directory.GetCurrentDirectory(), ass_name + ".dll");
            }
            else
            {
                dll_path = Path.Combine(Directory.GetCurrentDirectory(), ass_dir, ass_name + ".dll");
            }
            if (!System.IO.File.Exists(dll_path))
            {
                DCLogger.Error($"Failed to locate assembly {dll_path}");
                return null;
            }
            string[] tmp_str = func_name.Split('.');
            if (tmp_str.Length != 2)
            {
                return null;
            }
            byte[] assm_bytes = File.ReadAllBytes(dll_path);
            System.Reflection.Assembly tmp_loc_asm = System.Reflection.Assembly.Load(assm_bytes);
            //System.Reflection.Assembly tmp_loc_asm = System.Reflection.Assembly.LoadFrom(dll_path); // AssemblyLoadContext.Default.LoadFromAssemblyPath(dll_path);
            //string tmpstr = ass_name + ".ConvertData";
            //int tmpcount = tmp_loc_asm.GetType(tmpstr).GetMethods().Length;
            //RollingFileLogger.Verbose($"asm getmethods count {tmpcount}");
            //string tmp_asm_name = tmp_loc_asm.GetName().FullName;
            //RollingFileLogger.Verbose($"assembly name = {tmp_asm_name}");
            //for (int ii = 0; ii < tmpcount; ii ++)
            //{
            //    RollingFileLogger.Verbose($"method {ii} name => {tmp_loc_asm.GetType($"{ ass_name }.{tmp_str[0]}").GetMethods()[ii].Name}");
            //}
            System.Reflection.MethodInfo result = tmp_loc_asm.GetType($"{ ass_name }.{tmp_str[0]}").GetMethod(tmp_str[1]);
            if (result == null)
            {
                DCLogger.Error($"Failed to load method {func_name} from assembly {ass_name}");
            }
            return result;
        }
    }
}
