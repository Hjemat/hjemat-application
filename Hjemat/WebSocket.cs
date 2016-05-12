using System;
using System.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hjemat
{
    public class WebSocket : WebSocketBehavior
    {
        public static WebSocketServer serverInstance;
        
        public static WebSocketServer CreateServer(int port = 8010)
        {
            var wssv = new WebSocketServer(port);
            wssv.AddWebSocketService<WebSocket>("/");
            serverInstance = wssv;


            return wssv;
        }
        
        public static void SendDevice(Device device)
        {
            var deviceString = JsonConvert.SerializeObject(device);
            serverInstance.WebSocketServices.Broadcast("{ \"commandType\": 6, \"device\":" + deviceString + "}");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var message = JsonConvert.DeserializeObject<Dictionary<string, int>>(e.Data);

            Console.WriteLine(e.Data);

            if (message["commandType"] == 1)
            {
                var device = DevicesManager.devices[(byte)message["deviceID"]];
                var valueID = (byte)message["valueID"];
                var value = (short)message["value"];

                device.SendMessage(CommandID.Set, valueID, value);

                message["commandType"] = 2;

                Send(JsonConvert.SerializeObject(message));

                device.values[valueID] = value;

                Console.WriteLine("Updating device on Rest server");
                RestServer.Instance.SendDevice(device);
            }
            else if (message["commandType"] == 3)
            {
                var device = DevicesManager.devices[(byte)message["deviceID"]];
                var valueID = (byte)message["valueID"];

                device.SendMessage(CommandID.Get, valueID, 0);
                Message response;

                try
                {
                    response = Message.Read();
                }
                catch (System.TimeoutException)
                {
                    return;
                }
                

                var value = response.GetShortData();

                device.values[valueID] = value;

                Console.WriteLine("Updating device on Rest server");
                RestServer.Instance.SendDevice(device);
                
                message.Add("value", value);

                message["commandType"] = 2;
                
                Send(JsonConvert.SerializeObject(message));
            }
            else if (message["commandType"] == 4)
            {
                var confirmation = message["confirmation"];

                if (confirmation == 0)
                {
                    DevicesManager.BeginPairingDevices();

                    message["confirmation"] = 1;

                    Send(JsonConvert.SerializeObject(message));
                }
            }
            else if (message["commandType"] == 5)
            {
                var confirmation = message["confirmation"];

                if (confirmation == 0)
                {
                    DevicesManager.StopPairingDevices();

                    message["confirmation"] = 1;

                    Send(JsonConvert.SerializeObject(message));
                   

                }
            }
            else if (message["commandType"] == 6)
            {
                var confirmation = message["confirmation"];
                
                if (confirmation != 0) return;

                DevicesManager.ScanDevices();

                DevicesManager.UpdateDevicesValues();

                RestServer.Instance.SynchronizeDevices( DevicesManager.devices );

                message["confirmation"] = 1;
                Send(JsonConvert.SerializeObject(message));
            }
        }
    }
}