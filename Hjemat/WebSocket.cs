using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hjemat
{
    public class WebSocket : WebSocketBehavior
    {
        public static WebSocketServer CreateServer(int port = 8010)
        {
            var wssv = new WebSocketServer(port);
            wssv.AddWebSocketService<WebSocket>("/");

            return wssv;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var message = JsonConvert.DeserializeObject<Dictionary<string, int>>(e.Data);

            Console.WriteLine(e.Data);

            if (message["commandType"] == 1)
            {
                var device = Program.Devices[(byte)message["deviceID"]];
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
                var device = Program.Devices[(byte)message["deviceID"]];
                var valueID = (byte)message["valueID"];

                device.SendMessage(CommandID.Get, valueID, 0);

                var response = Message.Read();

                var value = response.GetShortData();

                message.Add("value", value);

                message["commandType"] = 2;

                Send(JsonConvert.SerializeObject(message));

                device.values[valueID] = value;

                Console.WriteLine("Updating device on Rest server");
                RestServer.Instance.SendDevice(device);
            }
            else if (message["commandType"] == 4)
            {
                var confirmation = message["confirmation"];

                if (confirmation == 0)
                {
                    

                    message["confirmation"] = 1;

                    Send(JsonConvert.SerializeObject(message));

                    DeviceManager.BeginPairing();
                }
            }
            else if (message["commandType"] == 5)
            {
                var confirmation = message["confirmation"];

                if (confirmation == 0)
                {
                    var toWrite = Message.CreatePairStop();
                    toWrite.Send();

                    message["confirmation"] = 1;

                    Send(JsonConvert.SerializeObject(message));

                }
            }
        }
    }
}