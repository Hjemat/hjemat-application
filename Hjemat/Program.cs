using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using RestSharp;
using Raspberry.IO.GeneralPurpose;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Linq;

namespace Hjemat
{
    

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

        static RestServer restServer;

        //public static List<Command> commands = new List<Command>();

        public static Dictionary<byte, Device> Devices { get; } = devices;

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

        static Config LoadSettings(string settingsPath = null)
        {
            Config config = new Config(new Uri("http://127.0.0.1/api/"), new Config.SerialConfig("COM3"));
            string filePath;

            if (settingsPath == null)
            {
                filePath = Path.Combine(
                    configFolderPath,
                    "settings.json");
            }
            else
            {
                filePath = Path.Combine(settingsPath, "settings.json");
            }


            Console.WriteLine(filePath);

            if (File.Exists(filePath))
            {
                Console.WriteLine("Reading settings file...");
                var settingsFile = File.ReadAllText(filePath);

                Console.WriteLine(settingsFile);

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

                foreach (var product in productList)
                {
                    products.Add(product.id, product);
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

                    if (response.GetHeader() == Message.CreateHeader(i, CommandID.Pingback))
                    {
                        var productID = response.GetDataBytes();
                        productID = new byte[] { productID[2], productID[1], productID[0], 0 };

                        var device = new Device(i, BitConverter.ToInt32(productID, 0));
                        devices.Add(device.deviceID, device);

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

            foreach (var device in devices.Values)
            {
                Console.WriteLine($"Found device with ID {device.deviceID} and product ID {device.productID}");
            }
        }

        static void SetupDevices()
        {
            foreach (var device in devices.Values)
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
                    device.SetupValues(product);

                    Console.WriteLine($"Device {device.deviceID}, a {product.name}, has been set up");
                }
            }

        }

        static Stopwatch delayStopwatch = new Stopwatch();

        public static void Delay(int miliseconds)
        {
            /*delayStopwatch.Start();
            while (delayStopwatch.ElapsedMilliseconds < miliseconds)
            {
                continue;
            }
            delayStopwatch.Stop();*/

            System.Threading.Thread.Sleep(miliseconds);
        }
        
        public static void Loop()
        {
            var quitting = false;
            while(true)
            {
                var cki = Console.ReadKey();
                
                if (cki.Key == ConsoleKey.Q || quitting)
                {
                    if (!quitting)
                    {
                        Console.Write("\nAre you sure you want to quit? (y/n): ");

                        quitting = true;
                    }
                    
                    if (cki.Key == ConsoleKey.Y)
                    {
                        Console.WriteLine();
                        break;
                    }
                    else if (cki.Key == ConsoleKey.N)
                    {
                        quitting = false;
                    }
                }
                    
            }
        }


        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                Console.WriteLine("Arg[{0}] = [{1}]", i, args[i]);
            }

            Message.rwPinConfig = ConnectorPin.P1Pin11.Output();
            Message.rwPinConnection = new GpioConnection(Message.rwPinConfig);

            configFolderPath = args[0];

            //Message.rwPinConnection.Toggle(Message.rwPinConfig);

            try
            {
                if (!File.Exists(configFolderPath))
                    Directory.CreateDirectory(args[0] ?? configFolderPath);

                config = LoadSettings(args[0]);
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

            serialPort = config.CreateSerialPort();
            Message.serialPort = serialPort;
            Device.serialPort = serialPort;

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

            var testWatch = new Stopwatch();
            Delay(400);

            Console.WriteLine("");

            ScanForDevices();

            SetupDevices();

            restServer = new RestServer(config.serverUrl);

            restServer.SynchronizeDevices(devices);

            var wssv = WebSocket.CreateServer();

            wssv.Start();
            Console.WriteLine($"Started WebSocket server on port {wssv.Port}");

            Loop();

            serialPort.Close();
            Message.rwPinConnection.Close();
            wssv.Stop();
        }
    }
}
