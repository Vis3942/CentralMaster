using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CentralMaster.json
{
    public class DerivedParameters
    {
        private static Dictionary<string, string> local_parameters = new Dictionary<string, string>();
        private const string AZURE_CONNECTION_INFO_FILENAME = "azure_connection_info.json";
        private static readonly HttpClient httpClient = new HttpClient();


        public static Dictionary<string, string> GetParameters()
        {
            if (local_parameters.Count == 0)
            {
                LoadDataFromAzureURL();
            }
            if (local_parameters.Count > 0)
            {
                return local_parameters;
            }

            return null;
        }

        private static void LoadDataFromAzureURL()
        {
            if (System.IO.File.Exists(AZURE_CONNECTION_INFO_FILENAME))
            {
                string json_str_from_file = System.IO.File.ReadAllText(AZURE_CONNECTION_INFO_FILENAME);
                var jsonDoc1 = System.Text.Json.JsonDocument.Parse(json_str_from_file);
                var rootElement1 = jsonDoc1.RootElement;
                var param_url = rootElement1.GetProperty("Url").ToString();

                try
                {
                    Task< HttpResponseMessage> t = httpClient.GetAsync(param_url);
                    t.Wait();
                    if (t.Exception != null)
                    {
                        throw t.Exception;
                    }
                    HttpResponseMessage response = t.Result;
                    //HttpResponseMessage response = await httpClient.GetAsync(param_url);
                    response.EnsureSuccessStatusCode();
                    Task <string> t2 = response.Content.ReadAsStringAsync();
                    t2.Wait();
                    if (t2.Exception != null)
                    {
                        throw t2.Exception;
                    }
                    string responseBody = t2.Result;
                    //string responseBody = await response.Content.ReadAsStringAsync();
                    //debug
                    Console.WriteLine(responseBody);
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
                    Console.WriteLine("\nException Caught!");
                    Console.WriteLine("Message :{0} ", e.Message);
                    //local_parameters.Clear();
                }
            }

        }
    }
}
