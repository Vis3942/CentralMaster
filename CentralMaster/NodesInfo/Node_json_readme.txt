For node "nc1:00000050" a file "nc1_000000050.json" will create the following json info
{
	"devices": [
		{
			"uuid": "9acc4e97-7b89-4cdb-955b-1c59a2731d93",
			"device": "SM-UART-04L",
			"channel": 0,
			"config_command": "Config",
			"init_command": "Initialize",
			"data_commands": [{
				"name": "Get PM2.5 Data",
		        "data_names": "orig_name:new_name, orig_name:new_name" // empty string the original names are retained, eg "pm2_5:IX, Brightness:X, ActivePower:XI",
				"order": 0
			}]
		},
		{
			"uuid": "4146c113-5251-48bf-be77-1edc9bb6aaee",
			"device": "ResponseOneWeatherTransmitter",
			"channel": 1,
			"config_command": "Config",
			"init_command": "",
			"data_commands": [{
				"name": "Get Weather Data",
				"data_names" : "",
				"order": 0
			}]
		}
	],
	"ComputedData":
    {
      "name": "ComputedAhDpHi", // name of dll
	  "uuid": "97141232-b4eb-427d-9d28-7c90e0065343",
      "description": "compute data based on sensors in this node",
      "compute": "ConvertData.ComputedData", // response function to use from dll (<computedData:name>.dll)
	  "data_names": "AbsoluteHumidity:X, DewPoint:XI, HeatIndex:XII", 
      "data": // metadata  about data returned by the response function
      [
        {
          "name": "AbsoluteHumidity",
          "description": "0-100%",
          "type": "byte",
          "units": "gm/m^3"
        },
        {
          "name": "DewPoint",
          "description": "value in range 32 to 122 deg F",
          "type": "float",
          "units": "deg F"
        },
        {
          "name": "HeatIndex",
          "description": "value in range 32 to 122 deg F",
          "type": "float",
          "units": "deg F"
        }
      ]
    }

}



Note: When commands are to be sent from the an external app to the DC (or Data Services Layer), use the following json
to be able to identify the value to set, read the set_commands in the device json.

{
    "Node" : "nc1_000000050",
	"UUID" : "9acc4e97-7b89-4cdb-955b-1c59a2731d93",
	"EventId" : "" // an optional string which the sender can track in the data response
	"Commands":
	[
      "Name": "Brightness",
      "Value": "50"
	]
}