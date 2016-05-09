using System;
using System.Linq;
using System.Collections.Generic;

namespace Hjemat
{
    public class DeviceManager
    {
        public static Dictionary<byte, Device> devices;
        static List<byte> avaliableDeviceIDs;

        public static void BeginPairing()
        {
            var toWrite = Message.CreatePairAllow();
            toWrite.Send();

            var gotMessage = false;
            var expectedHeader = Message.CreateHeader(0, CommandIDPair.Ask);
            Message message = new Message(0, CommandID.Error, new byte?[] { 0, 0, 0 });
            while (true)
            {
                try
                {
                    message = Message.Read();
                    gotMessage = true;
                }
                catch (System.Exception)
                {

                }

                if (gotMessage)
                {
                    if (message.GetHeader() == expectedHeader)
                    {
                        Device newDevice = new Device();
                        newDevice.deviceID = avaliableDeviceIDs.First();
                        newDevice.productID = message.GetProductID();
                        var retMessage = Message.CreatePairReturn(newDevice.deviceID);
                        retMessage.Send();
                    }
                }
            }
        }
    }
}