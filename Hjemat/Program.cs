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
    public enum CommandID
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
                    device.values = new Dictionary<byte, short>();

                    foreach (var productValue in product.values)
                    {
                        device.values.Add(productValue.id, device.GetValue(productValue.id));
                    }

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

            var testWatch = new Stopwatch();
            Delay(400);

            Console.WriteLine("");

            ScanForDevices();

            SetupDevices();

            restServer = new RestServer(config.serverUrl);

            var client = new RestClient();
            client.BaseUrl = config.serverUrl;

            var request = new RestRequest();
            request.Resource = "devices";

            var response = client.Execute(request);

            if (response.ErrorException != null)
            {
                const string message = "Error retrieving response.  Check inner details for more info.";
                throw new ApplicationException(message, response.ErrorException);
            }

            var devicesFromServer = JsonConvert.DeserializeObject<List<Device>>(response.Content);

            Console.WriteLine(devicesFromServer.Count);

            foreach (var device in devices.Values.ToList())
            {
                Console.WriteLine(JsonConvert.SerializeObject(device, Formatting.Indented));
            }

            for (byte i = 1; i < 0x20; i++)
            {
                var remoteDevice = devicesFromServer.Find(x => x.deviceID == i) ?? null;

                Device localDevice = null;

                if (devices.ContainsKey(i))
                {
                    localDevice = devices[i];
                }
                

                if (remoteDevice == null && localDevice != null)
                {
                    Console.WriteLine($"Posted local device {i} to server");

                    var postRequest = new RestRequest($"devices/", Method.POST);
                    postRequest.AddHeader("Accept", "application/json");
                    postRequest.Parameters.Clear();
                    postRequest.AddParameter("application/json", JsonConvert.SerializeObject(localDevice), ParameterType.RequestBody);

                    Console.WriteLine( client.Execute(postRequest).StatusCode );
                }
                else if (remoteDevice != null && localDevice != null)
                {
                    Console.WriteLine($"Device with id {i} on server and locally. Putting local device to server");

                    var putRequest = new RestRequest($"devices/{i}", Method.PUT);
                    putRequest.AddHeader("Accept", "application/json");
                    putRequest.Parameters.Clear();
                    putRequest.AddParameter("application/json", JsonConvert.SerializeObject(localDevice), ParameterType.RequestBody);

                    Console.WriteLine( client.Execute(putRequest).StatusCode );
                }
                else if (remoteDevice != null && localDevice == null)
                {
                    Console.WriteLine($"Device with id {i} on server but not locally. Deleting from server");

                    var deleteRequest = new RestRequest($"devices/{i}", Method.DELETE);
                    Console.WriteLine( client.Execute(deleteRequest).StatusCode );
                }
            }
            
            var wssv = new WebSocketServer(8010);
            wssv.AddWebSocketService<WebSocket>("/");

            wssv.Start();
            Console.WriteLine($"Started WebSocket server on port {wssv.Port}");

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

            serialPort.Close();
            Message.rwPinConnection.Close();
            wssv.Stop();
        }
    }
}
