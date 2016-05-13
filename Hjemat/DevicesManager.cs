using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using WebSocketSharp.Server;

namespace Hjemat
{
    public class DevicesManager
    {
        public static Dictionary<byte, Device> devices = new Dictionary<byte, Device>();
        static List<byte> avaliableDeviceIDs = new List<byte>() { 1, 2, 3, 4, 5 };

        static bool pairingDevices = false;
        static bool pairThreadEnded = false;

        static Thread pairingThread;

        public delegate void DevicePairedHandler(Device device);
        public static event DevicePairedHandler OnDevicePaired;

        public static void BeginPairingDevices()
        {
            var message = Message.CreatePairAllow();
            message.Send();

            pairingThread = new Thread(PairDevices);
            pairingThread.Start();

            while (!pairingThread.IsAlive) ;
        }

        public static void StopPairingDevices()
        {
            pairingThread.Abort();

            pairingThread.Join();

            RestManager.Instance.SynchronizeDevices(devices);

            var message = Message.CreatePairStop();
            message.Send();
        }

        public static void PairDevices()
        {
            Message message;
            var expectedHeader = Message.CreateHeader(0, CommandIDPair.Ask);

            Byte?[] emptyArray = { 0, 0, 0 };

            Console.WriteLine("Going on an adventure!");

            while (pairingThread.ThreadState != ThreadState.AbortRequested)
            {
                try
                {
                    message = new Message(0, CommandID.Error, emptyArray);
                    message = Message.Read();

                    Console.WriteLine("Yay, a message!");

                    if (message.GetHeader() == expectedHeader)
                    {
                        Device newDevice = new Device();
                        Console.WriteLine("Assigning Device id");
                        newDevice.deviceID = avaliableDeviceIDs[0];
                        avaliableDeviceIDs.RemoveAt(0);

                        Console.WriteLine("Getting product id");
                        newDevice.productID = message.GetProductID();

                        Console.WriteLine($"It's a device with id {newDevice.deviceID} and has product id {newDevice.productID}");

                        Thread.Sleep(1000);

                        var retMessage = Message.CreatePairReturn(newDevice.deviceID);
                        retMessage.Send();

                        try
                        {
                            var pingBack = Message.Read();
                        }
                        catch (System.TimeoutException)
                        {
                            Console.WriteLine("Device didn't respond to ping");
                            throw new SystemException("Sadness");
                        }


                        newDevice.SetupValues(ProductsManager.products[newDevice.productID]);
                        devices.Add(newDevice.deviceID, newDevice);

                        if (DevicesManager.OnDevicePaired != null)
                            DevicesManager.OnDevicePaired(newDevice);
                    }
                }
                catch (System.TimeoutException)
                {
                    Console.WriteLine("didn't get a message #ForeverAlone");
                }
            }
        }

        public static void ScanDevices()
        {
            for (byte i = 0x01; i < 0x1F; i++)
            {
                Console.Write($"\rScanning for devices... {i}/31");

                var ping = Message.CreatePing(i);
                ping.Send();

                try
                {
                    var response = Message.Read();

                    if (response.GetHeader() == Message.CreateHeader(i, CommandID.Pingback))
                    {
                        var productID = response.GetDataBytes();
                        productID = new byte[] { productID[2], productID[1], productID[0], 0 };

                        var device = new Device(i, BitConverter.ToInt32(productID, 0));
                        DevicesManager.devices.Add(device.deviceID, device);

                        Console.WriteLine($"\rFound device with ID {device.deviceID} and product ID {device.productID}");
                    }
                    else
                    {
                        Console.WriteLine($"Got message {response.GetHeader()}");
                    }
                }
                catch (System.TimeoutException)
                {
                    continue;
                }


            }

            Console.WriteLine("\n");

            foreach (var device in DevicesManager.devices.Values)
            {
                Console.WriteLine($"Found device with ID {device.deviceID} and product ID {device.productID}");
            }
        }
        
        public static void UpdateDevicesValues()
        {
            foreach (var device in DevicesManager.devices.Values)
            {
                Product product = null;

                if (ProductsManager.products.ContainsKey(device.productID))
                {
                    product = ProductsManager.products[device.productID];
                }

                if (product == null)
                {
                    //TODO: Update product list if not already done, and check for product again

                    Console.WriteLine($"Couldn't find product of device {device.deviceID}, product id: {device.productID}");
                }
                else
                {
                    device.SetupValues(product);

                    Console.WriteLine($"Device {device.deviceID}, a {product.name}, has been set up");
                }
            }
        }
    }
}