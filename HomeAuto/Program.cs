using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;

namespace HomeAuto
{
    enum Command
    {
        Error, Request, Send, Ping, Confirmation
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

    class Device
    {
        public byte deviceID;
        public int productID;

        public class DeviceValue 
        {
            public byte valueID;
            public int value;

            public DeviceValue(byte valueID, int value)
            {
                this.valueID = valueID;
                this.value = value;
            }
        }

        public Device(byte deviceID, int productID)
        {
            this.deviceID = deviceID;
            this.productID = productID;
        }

        public bool SendData(SerialPort serial, byte dataID, int data)
        {
            byte[] message = new byte[4];
            var dataBytes = BitConverter.GetBytes(data); 

            message[0] = Message.CreateHeader(deviceID, Command.Send);
            message[1] = dataID;
            message[2] = dataBytes[1];
            message[3] = dataBytes[0];

            serial.DiscardInBuffer();
            serial.Write(message, 0, 4);
            Console.WriteLine("Written data to device");

            byte[] confirmationMessage = new byte[4];
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool reading = true;
            int messageIndex = 0;

            while(reading)
            {
                var numBytes = serial.BytesToRead;
                if (numBytes < 1)
                    continue;
                    
                serial.Read(confirmationMessage, messageIndex, numBytes);
                messageIndex += numBytes;

                if (messageIndex >= 3)
                {
                    reading = false;
                }
            }

            var expectedHeader = Message.CreateHeader(deviceID, Command.Confirmation);

            if (confirmationMessage[0] == expectedHeader)
            {
                Console.WriteLine("Data confirmed received");
                return true;
            }
            else
            {
                Console.WriteLine("No confirmation of reception");
                foreach (var part in confirmationMessage)
                {
                    Console.Write(part);
                    Console.Write(" ");
                }
                Console.WriteLine("");

                return false;
            }
        }
    }

    class Message
    {
        public static Dictionary<Command, byte> commandIDs = new Dictionary<Command, byte>()
        {
            {Command.Error, 0x0 },
            {Command.Request, 0x1 },
            {Command.Send, 0x5 },
            {Command.Ping, 0x4 },
            {Command.Confirmation, 0x6 }
        };

        public static byte CreateHeader(byte deviceID, Command command)
        {
            var commandID = commandIDs[command];

            var header = deviceID << 3;
            header = header | commandID;

            return (byte)header;
        }
    }

    class Program
    {
        int updateInterval = 3;
        static SerialPort serialPort = new SerialPort();
        static Config config = new Config("http://127.0.0.1/api/", new Config.SerialConfig("COM3"));

        static Device testDevice = new Device(deviceID: 2, productID: 10);

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
            if (File.Exists("settings.json"))
            {
                Console.WriteLine("Setting up according to settings.json...");
                var settingsFile = File.ReadAllText("settings.json");
                
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
                var input = Console.ReadLine();
                
                switch (input.ToLower())
                {
                    case "0":
                        devices.Find(x => x.deviceID == 2).SendData(serialPort, dataID: 0x1, data: 0x0);
                        Console.WriteLine("Turned off light");
                        break;
                    case "1":
                        devices.Find(x => x.deviceID == 2).SendData(serialPort, dataID: 0x1, data: 0x1);
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
