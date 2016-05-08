using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using RestSharp;
using WebSocketSharp.Net;

namespace Hjemat
{
    public class RestServer
    {
        public static RestServer Instance
        {
            get {
                return instance;
            }
        }
        
        static RestServer instance;

        private RestClient _client;

        public RestServer(Uri url)
        {
            _client = new RestClient(url);
            instance = this;
        }


        public void SendDevice(Device device)
        {
            var request = new RestRequest($"devices/{device.deviceID}", Method.GET);
            var response = _client.Execute(request);

            if ((int)response.StatusCode == (int)HttpStatusCode.NotFound)
            {
                PostDevice(device);
            }
            else
            {
                PutDevice(device);
            }
        }

        public void PostDevice(Device device)
        {
            var request = new RestRequest($"devices/", Method.POST);
            request.AddHeader("Accept", "application/json");
            request.Parameters.Clear();
            request.AddParameter("application/json", JsonConvert.SerializeObject(device), ParameterType.RequestBody);

            Console.WriteLine(_client.Execute(request).StatusCode);
        }

        public void PutDevice(Device device)
        {
            var putRequest = new RestRequest($"devices/{device.deviceID}", Method.PUT);
            putRequest.AddHeader("Accept", "application/json");
            putRequest.Parameters.Clear();
            putRequest.AddParameter("application/json", JsonConvert.SerializeObject(device), ParameterType.RequestBody);

            Console.WriteLine(_client.Execute(putRequest).StatusCode);
        }
    }
}