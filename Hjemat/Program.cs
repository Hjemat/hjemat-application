using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using RestSharp;

namespace Hjemat
{
    enum Command
    {
        Error = 0,
        Ping = 1,
        Pingback = 2,
        Get = 3,
        Set = 4,
        Return = 5,
    }

    class Config
    {
        public class SerialConfig
        {
            public string portName = "COM3";
            public int baudRate = 9600;
            public Parity parity = Parity.Odd;
            public StopBits stopBits = StopBits.One;
            public int dataBits = 8;
            public Handshake handshake = Handshake.None;
            public int readTimeout = 2000;

            public SerialConfig(string portName)
            {
                this.portName = portName;
            }
        }

        public Uri serverUrl;
        public SerialConfig serialConfig;

        public Config(Uri serverUrl, SerialConfig serialConfig)
        {
            this.serverUrl = serverUrl;
            this.serialConfig = serialConfig;
        }

    }

    class Program
    {
        int updateInterval = 1;
        static SerialPort serialPort = new SerialPort();
        static Config config = new Config(new Uri("http://127.0.0.1/api/"), new Config.SerialConfig("COM3"));

        static Dictionary<byte, Device> devices = new Dictionary<byte, Device>();
        static Dictionary<int, Product> products = new Dictionary<int, Product>();
        
        static string configFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "hjemat-app");

        static void SetupSerialPort(Config.SerialConfig serialConfig)
        {
            serialPort.PortName = serialConfig.portName;
            serialPort.BaudRate = serialConfig.baudRate;
            serialPort.Parity = serialConfig.parity;
            serialPort.StopBits = serialConfig.stopBits;
            serialPort.DataBits = serialConfig.dataBits;
            serialPort.Handshake = serialConfig.handshake;
            serialPort.ReadTimeout = serialConfig.readTimeout;

            Message.serialPort = serialPort;
            Device.serialPort = serialPort;
        }

        static Config LoadSettings()
        {
            Config config = new Config(new Uri("http://127.0.0.1/api/"), new Config.SerialConfig("COM3"));

            var filePath = Path.Combine(
                configFolderPath,
                "settings.json");

            if (File.Exists(filePath))
            {
                Console.WriteLine("Reading settings file...");
                var settingsFile = File.ReadAllText(filePath);

                Console.WriteLine("Setting up according to settings.json...");
                config = JsonConvert.DeserializeObject<Config>(settingsFile);

            }
            else
            {
                Console.WriteLine("Settings file not found. Creating standard settings file");
                File.WriteAllText(filePath, JsonConvert.SerializeObject(config, Formatting.Indented));

                Console.WriteLine($"Settings file created, needs configuration before using program.\nFile path: {filePath}");

                throw new System.Exception("Halt program to let user edit settings");
            }

            return config;
        }
        
        static Dictionary<int, Product> GetProductsDict()
        {
            var products = new Dictionary<int, Product>();
            var productList = new List<Product>();

            var filePath = Path.Combine(
                configFolderPath,
                "products.json");
                

            if (File.Exists(filePath))
            {
                Console.WriteLine("Reading products file...");
                var settingsFile = File.ReadAllText(filePath);

                Console.WriteLine("Setting up according to products.json...");
                productList = JsonConvert.DeserializeObject<List<Product>>(settingsFile);
                
                foreach(var product in productList)
                {
                    products.Add(product.productID, product);
                }

            }
            
            return products;
        }

        static void ScanForDevices()
        {
            for (byte i = 0x01; i <= 0x20; i++)
            {
                Console.Write($"\rScanning for devices... {i}/32");

                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                var ping = Message.CreatePing(i);
                ping.Send();

                try
                {
                    var response = Message.Read();

                    if (response.GetHeader() == Message.CreateHeader(i, Command.Pingback))
                    {
                        var productID = response.GetDataBytes();
                        productID = new byte[] { productID[2], productID[1], productID[0], 0 };

                        var device = new Device(i, BitConverter.ToInt32(productID, 0));
                        devices.Add(device.deviceID, device);

                        Console.WriteLine($"\rFound device with ID {device.deviceID} and product ID {device.productID}");
                    }
                }
                catch (System.TimeoutException)
                {
                    continue;
                }


            }

            Console.WriteLine("\n");

            foreach (var device in devices.Values)
            {
                Console.WriteLine($"Found device with ID {device.deviceID} and product ID {device.productID}");
            }
        }
        
        static void SetupDevices()
        {
            foreach(var device in devices.Values)
            {
                Product product = null;

                if (products.ContainsKey(device.productID))
                {
                    product = products[device.productID];
                }
                
                if (product == null)
                {
                    //TODO: Update product list if not already done, and check for product again

                    Console.WriteLine($"Couldn't find product of device {device.deviceID}, product id: {device.productID}");
                }
                else
                {
                    device.values = new Dictionary<byte, short>();
                    
                    foreach(var productValueID in product.values.Keys)
                    {
                        device.values.Add(productValueID, device.GetValue(productValueID));
                    }
                    
                    Console.WriteLine($"Device {device.deviceID}, a {product.name}, has been set up");
                }
            }
            
        }

        

        static void Main(string[] args)
        {   
            try
            {
                Directory.CreateDirectory(configFolderPath);
                config = LoadSettings();
            }
            catch (System.Exception)
            {
                return;
            }

            products = GetProductsDict();
            Console.WriteLine(JsonConvert.SerializeObject(products, Formatting.Indented));

            if (config == null)
            {
                Console.WriteLine("Failed to load settings file");
                return;
            }

            if (config.serialConfig == null)
            {
                Console.WriteLine("Error getting SerialConfig from settings");
                return;
            }

            SetupSerialPort(config.serialConfig);

            try
            {
                serialPort.Open();
            }
            catch (System.Exception)
            {
                Console.WriteLine($"Error opening serial port. Make sure device is connected and that {serialPort.PortName} is the correct port");

                return;
            }

            Console.WriteLine("Giving port time to open...");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < 3000)
            {
                continue;
            }
            stopwatch.Stop();

            Console.WriteLine("");

            ScanForDevices();

            SetupDevices();

            var client = new RestClient();
            client.BaseUrl = config.serverUrl;

            var request = new RestRequest();
            request.Resource = "devices";

           /* var response = client.Execute(request);

            if (response.ErrorException != null)
            {
                const string message = "Error retrieving response.  Check inner details for more info.";
                throw new ApplicationException(message, response.ErrorException);
            }

            var devicesFromServer = JsonConvert.DeserializeObject<List<Device>>(response.Content);

            Console.WriteLine(devicesFromServer.Count);

            foreach (var device in devices)
            {
                Console.WriteLine(JsonConvert.SerializeObject(device, Formatting.Indented));
            }
            
            for (int i = 1; i < 0x20; i++)
            {
                var remoteDevice = devicesFromServer.Find(x => x.deviceID == i);
                var localDevice = devices[i];

                if (remoteDevice == null && localDevice != null)
                {
                    // POST device to server
                }
                else if (remoteDevice != null && localDevice != null)
                {
                    // PUT device from server
                }
                else if (remoteDevice != null && localDevice == null)
                {
                    // DELETE device from server
                }
            } */

            serialPort.Close();

            Console.Write("Enter to end program...");
            Console.ReadLine();
        }
    }
}
