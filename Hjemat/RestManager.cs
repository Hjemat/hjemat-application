using System;
using System.Linq;
using System.Collections.Generic;
using RestSharp;
using Newtonsoft.Json;
using WebSocketSharp.Net;

namespace Hjemat
{
    public class RestManager
    {
        public static RestManager Instance
        {
            get {
                return instance;
            }
        }
        
        static RestManager instance;

        private RestClient _client;

        public RestManager(Uri url)
        {
            _client = new RestClient(url);
            instance = this;
        }
        
        public Device GetDevice(byte deviceID)
        {
            var request = new RestRequest($"devices/{deviceID}", Method.GET);
            var response = _client.Execute(request);
            
            if ((int)response.StatusCode == (int)HttpStatusCode.NotFound)
                return null;
            
            return JsonConvert.DeserializeObject<Device>(response.Content);
        }
        
        public List<Device> GetDevices()
        {
            var request = new RestRequest($"devices", Method.GET);
            var response = _client.Execute(request);
            
            if ((int)response.StatusCode == (int)HttpStatusCode.NotFound)
                return null;

            return JsonConvert.DeserializeObject<List<Device>>(response.Content);
        }
        
        public Dictionary<byte, Device> GetDevicesAsDictonary()
        {
            var devices = GetDevices();
            var devicesDict = new Dictionary<byte, Device>();

            if (devices == null)
                return null;

            foreach (var device in devices)
            {
                devicesDict.Add(device.deviceID, device);
            }

            return devicesDict;
        }


        public IRestResponse SendDevice(Device device)
        {
            var response = this.GetDevice(device.deviceID);

            if (response == null)
            {
                Console.WriteLine("Posting device");
                return PostDevice(device);
            }
            else
            {
                Console.WriteLine("Putting Device");
                return PutDevice(device);
            }
        }

        public IRestResponse PostDevice(Device device)
        {
            var request = new RestRequest($"devices/", Method.POST);
            request.AddHeader("Accept", "application/json");
            request.Parameters.Clear();
            request.AddParameter("application/json", JsonConvert.SerializeObject(device), ParameterType.RequestBody);

            return _client.Execute(request);
        }

        public IRestResponse PutDevice(Device device)
        {
            var request = new RestRequest($"devices/{device.deviceID}", Method.PUT);
            request.AddHeader("Accept", "application/json");
            request.Parameters.Clear();
            request.AddParameter("application/json", JsonConvert.SerializeObject(device), ParameterType.RequestBody);

            return _client.Execute(request);
        }
        
        public IRestResponse DeleteDevice(byte deviceID)
        {
            var request = new RestRequest($"devices/{deviceID}", Method.DELETE);
            return _client.Execute(request);
        }
        
        public void SynchronizeDevices(Dictionary<byte, Device> devices)
        {
            var remoteDevices = this.GetDevicesAsDictonary();

            foreach (var device in devices.Values.ToList())
            {
                Console.WriteLine(JsonConvert.SerializeObject(device, Formatting.Indented));
            }

            for (byte i = 1; i < 0x20; i++)
            {
                var remoteHasDevice = remoteDevices.ContainsKey(i);
                var localHasDevice = devices.ContainsKey(i);
                
                if (localHasDevice)
                {
                    Console.WriteLine($"Sending device {i} to server");

                    SendDevice(devices[i]);
                }
                else if (remoteHasDevice)
                {
                    Console.WriteLine($"Device with id {i} on server but not locally. Deleting from server");

                    DeleteDevice(i);
                }
            }
        }
    }
}