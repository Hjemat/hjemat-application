using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;

namespace Hjemat
{
    enum Command
    {
        Error = 0,
        Request = 1,
        FromDevice = 2,
        Ping = 4,
        Send = 5,
        Confirmation = 6,
        Pingback = 7
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
        
        public string serverUrl;
        public SerialConfig serialConfig;
        
        public Config(string serverUrl, SerialConfig serialConfig)
        {
            this.serverUrl = serverUrl;
            this.serialConfig = serialConfig;
        }
        
    }

    class Program
    {
        int updateInterval = 3;
        static SerialPort serialPort = new SerialPort();
        static Config config = new Config("http://127.0.0.1/api/", new Config.SerialConfig("COM3"));
        

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
        }

        static Config LoadSettings()
        {
            Config config = null;
            
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
            }

            return config;
        }
        
        static void ScanForDevices()
        {
            for (byte i = 0x01; i <= 0x20; i++)
            {
                Console.Write($"\rScanning for devices... {i}/32");
                
                var ping = Message.CreatePing(i);
                ping.Send();

                try
                {
                    var response = Message.Read();

                    if (response.GetHeader() == Message.CreateHeader(i, Command.Pingback))
                    {
                        var productID = response.GetDataBytes();
                        productID = new byte[] { productID[2], productID[1], productID[0], 0 };

                        var device = new Device(i, BitConverter.ToInt32(productID, 0), serialPort);
                        devices.Add(device);

                    }
                }
                catch (System.TimeoutException)
                {
                    continue;
                }

                
            }

            Console.WriteLine("");
            
            foreach( var device in devices)
            {
                Console.WriteLine($"Found device with ID {device.deviceID} and product ID {device.productID}");
            }
        }
        

        static void Main(string[] args)
        {
            config = LoadSettings();
            
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

            ScanForDevices();

            serialPort.Close();

            Console.Write("Enter to end program...");
            Console.ReadLine();
        }
    }
}
