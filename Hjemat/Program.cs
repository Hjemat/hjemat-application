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
        

        static List<Device> devices = new List<Device>();

        static void SetupSerialPort(Config.SerialConfig serialConfig)
        {
            serialPort.PortName     = serialConfig.portName;
            serialPort.BaudRate     = serialConfig.baudRate;
            serialPort.Parity       = serialConfig.parity;
            serialPort.StopBits     = serialConfig.stopBits;
            serialPort.DataBits     = serialConfig.dataBits;
            serialPort.Handshake    = serialConfig.handshake;
            serialPort.ReadTimeout  = serialConfig.readTimeout;

            Message.serialPort = serialPort;
            Device.serialPort = serialPort;
        }

        static Config LoadSettings()
        {
            Config config = new Config(new Uri("http://127.0.0.1/api/"), new Config.SerialConfig("COM3"));
            
            var settingsFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "hjemat-app");

            var filePath = Path.Combine(
                settingsFolderPath,
                "settings.json");

            Directory.CreateDirectory(settingsFolderPath);
            Console.WriteLine(filePath);

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
                        devices.Add(device);
                        
                        Console.WriteLine($"\rFound device with ID {device.deviceID} and product ID {device.productID}");
                    }
                }
                catch (System.TimeoutException)
                {
                    continue;
                }

                
            }

            Console.WriteLine("\n");
            
            foreach( var device in devices)
            {
                Console.WriteLine($"Found device with ID {device.deviceID} and product ID {device.productID}");
            }
        }
        

        static void Main(string[] args)
        {
            try
            {
                config = LoadSettings();
            }
            catch (System.Exception)
            {
                return;
            }
            
            
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

            var client = new RestClient();
            client.BaseUrl = config.serverUrl;

            var request = new RestRequest();
            request.Resource = "devices";

            var response = client.Execute(request);

            var devicesFromServer = JsonConvert.DeserializeObject < List<Device>> (response.Content);

            if (response.ErrorException != null)
            {
                const string message = "Error retrieving response.  Check inner details for more info.";
                var twilioException = new ApplicationException(message, response.ErrorException);
                throw twilioException;
            }

            Console.WriteLine(devicesFromServer.Count);
            
            foreach(var device in devices)
            {
                Console.WriteLine(JsonConvert.SerializeObject(device, Formatting.Indented));
            }
            
            /*
            foreach(var device in devicesFromServer)
            {
                var editRequest = new RestRequest($"devices/{device.deviceID}");

                if (devices.Find(x => x.deviceID == device.deviceID) == null)
                {
                    editRequest.Method = Method.DELETE;
                }
                else
                {
                    editRequest.Method = Method.PUT;
                    JsonConvert.SerializeObject<Device>(devices.Find(x => x.deviceID == devicesFromServer));
                }
            }*/


            serialPort.Close();

            Console.Write("Enter to end program...");
            Console.ReadLine();
        }
    }
}
