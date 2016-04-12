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
        Confirmation = 6
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
        }

        static void Main(string[] args)
        {
            Device testDevice = new Device(deviceID: 2, productID: 10, serialPort: serialPort);
            
            if (File.Exists("settings.json"))
            {
                Console.WriteLine("Reading settings file...");
                var settingsFile = File.ReadAllText("settings.json");
                
                Console.WriteLine("Setting up according to settings.json...");
                config = JsonConvert.DeserializeObject<Config>(settingsFile);
                
                if (config == null)
                {
                    Console.WriteLine("Error loading settings file");
                    return;
                }
                
            }
            else
            {
                Console.WriteLine("Settings file not found. Creating standard settings file");
                File.WriteAllText("settings.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                
                Console.WriteLine("Settings file created, needs configuration before using program");
                
                return;
            }
            
            devices.Add(testDevice);
            
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
            
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();

            var running = true;
            while (running)
            {
                Console.WriteLine("0 or 1 to turn light off or on. q to exit program.");
                var input = Console.ReadLine();
                
                switch (input.ToLower())
                {
                    case "0":
                        devices.Find(x => x.deviceID == 2).SendData(dataID: 0x1, data: 0x0);
                        Console.WriteLine("Turned off light");
                        break;
                    case "1":
                        devices.Find(x => x.deviceID == 2).SendData(dataID: 0x1, data: 0x1);
                        Console.WriteLine("Turned on light");
                        break;
                    case "q":
                        running = false;
                        Console.Write("Enter to exit");
                        break;
                    default:
                        Console.WriteLine("Invalid input");
                        break;
                }
            }
           
            serialPort.Close();

            Console.ReadLine();
        }
    }
}
