using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Hjemat
{
    public class WebSocket : WebSocketBehavior
    {
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
        }
    }
}