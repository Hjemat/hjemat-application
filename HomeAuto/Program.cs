using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Diagnostics;

namespace HomeAuto
{
    enum Command
    {
        Error, Request, Send, Ping, Confirmation
    }

    class Device
    {
        public byte deviceID;
        public int productID;

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
        string serverURL = "http://192.168.1.177/api/";
        int updateInterval = 3;
        static SerialPort serialPort = new SerialPort("COM3");

        static Device testDevice = new Device(deviceID: 2, productID: 10);

        static List<Device> devices = new List<Device>();

        static void SetupSerialPort()
        {
            serialPort.BaudRate = 9600;
            serialPort.Parity = Parity.Odd;
            serialPort.StopBits = StopBits.One;
            serialPort.DataBits = 8;
            serialPort.Handshake = Handshake.None;
            serialPort.ReadTimeout = 2000;
        }

        static void Main(string[] args)
        {
            devices.Add(testDevice);

            SetupSerialPort();
            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();

            devices.Find(x => x.deviceID == 2).SendData(serialPort, dataID: 0x1, data: 0x1);

            serialPort.Close();

            Console.ReadLine();
        }
    }
}
