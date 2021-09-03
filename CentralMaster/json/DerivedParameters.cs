using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LogUtils;

namespace CentralMaster.json
{
    public class DerivedParameters
    {
        private static Dictionary<string, string> local_parameters = new Dictionary<string, string>();
        private static Dictionary<string, string> file_local_parameters = new Dictionary<string, string>();
        private const string AZURE_CONNECTION_INFO_FILENAME = "azure_connection_info.json";
        private const string PARAMETERS_DATA_FILENAME = "cm_params.txt";
        private static readonly HttpClient httpClient = new HttpClient();

        public static Dictionary<string, string> GetParameters()
        {
            if (local_parameters.Count == 0)
            {
                LoadParameterData();
            }
            if (local_parameters.Count > 0)
            {
                return local_parameters;
            }
            if (file_local_parameters.Count > 0) // resiliency: loaded data from file if it exists and have lost connection
            {
                return file_local_parameters;
            }
            return null;
        }

        private static void LoadParameterData()
        {
            if (AppOptions.GetAppOptions.SupportingService.ToLower().Equals("azuretable"))
            {
                LoadDataFromAzureURL();
                if (local_parameters.Count <= 0)
                {
                    // no parameters were loaded; get data from file if it exists
                    LoadParametersFromFile();
                }
            }
            //else
            //{
            //    // do nothing; more support required if adding additional services
            //}
        }

        private static async void LoadDataFromAzureURL()
        {
            if (System.IO.File.Exists(AZURE_CONNECTION_INFO_FILENAME))
            {
                string json_str_from_file = System.IO.File.ReadAllText(AZURE_CONNECTION_INFO_FILENAME);
                var jsonDoc1 = System.Text.Json.JsonDocument.Parse(json_str_from_file);
                var rootElement1 = jsonDoc1.RootElement;
                var param_url = rootElement1.GetProperty("Url").ToString();

                try
                {
                    //Task< HttpResponseMessage> t = httpClient.GetAsync(param_url);
                    //t.Wait();
                    //if (t.Exception != null)
                    //{
                    //    throw t.Exception;
                    //}
                    //HttpResponseMessage response = t.Result;
                    HttpResponseMessage response = await httpClient.GetAsync(param_url);
                    response.EnsureSuccessStatusCode();
                    Task <string> t2 = response.Content.ReadAsStringAsync();
                    t2.Wait();
                    if (t2.Exception != null)
                    {
                        throw t2.Exception;
                    }
                    string responseBody = t2.Result;
                    //string responseBody = await response.Content.ReadAsStringAsync();
                    DCLogger.Debug(responseBody);
                    WriteParametersToFile(responseBody);
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                    var rootElement = jsonDoc.RootElement;
                    var param_names = rootElement.GetProperty("params").ToString();
                    string[] param_arr = param_names.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tmp_param in param_arr)
                    {
                        var val_str = rootElement.GetProperty(tmp_param.Trim()).ToString();
                        local_parameters.Add(tmp_param.Trim(), val_str);
                    }
                }
                catch (HttpRequestException e)
                {
                    DCLogger.Error(e, "LoadDataFromAzureURL work");
                    //local_parameters.Clear();
                }
            }

        }

        private static void WriteParametersToFile(string params_str)
        {
            if (System.IO.Directory.Exists(AppOptions.GetAppOptions.ParametersDirectory))
            {
                try
                {
                    string wrfile_with_path = System.IO.Path.Combine(AppOptions.GetAppOptions.ParametersDirectory, PARAMETERS_DATA_FILENAME);
                    using (System.IO.StreamWriter file_wr = new System.IO.StreamWriter(wrfile_with_path))
                    {
                        file_wr.WriteLine(params_str);
                    }
                }
                catch (Exception wr_excp)
                {
                    LogUtils.DCLogger.Error(wr_excp, "Writing Params to File.");
                    LogUtils.DCLogger.Error(params_str);
                }            
            }
        }


        private static void LoadParametersFromFile()
        {
            file_local_parameters.Clear();
            if (AppOptions.GetAppOptions.SupportingService.ToLower().Equals("azuretable"))
            {
                string params_file_loc = System.IO.Path.Combine(AppOptions.GetAppOptions.ParametersDirectory, PARAMETERS_DATA_FILENAME);
                if (System.IO.File.Exists(params_file_loc))
                {
                    string json_str_from_file = System.IO.File.ReadAllText(params_file_loc);
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(json_str_from_file);
                    var rootElement = jsonDoc.RootElement;
                    var param_names = rootElement.GetProperty("params").ToString();
                    string[] param_arr = param_names.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tmp_param in param_arr)
                    {
                        var val_str = rootElement.GetProperty(tmp_param.Trim()).ToString();
                        file_local_parameters.Add(tmp_param.Trim(), val_str);
                    }
                    LogUtils.DCLogger.Information($"Loaded parameters from file {params_file_loc}");
                }
            }
        }


    }
}
