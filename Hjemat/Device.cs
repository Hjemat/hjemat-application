

using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Collections.Generic;

namespace Hjemat
{
    class Device
    {
        public byte deviceID;
        public int productID;
        public List<DeviceValue> values;

        public static SerialPort serialPort;

        public class DeviceValue
        {
            public byte id;
            public int value;

            public DeviceValue(byte id, int value)
            {
                this.id = id;
                this.value = value;
            }
        }

        public Device(byte deviceID = 0x0, int productID = 0x0, List<DeviceValue> values = null)
        {
            this.deviceID = deviceID;
            this.productID = productID;
           
            this.values = values ?? new List<DeviceValue>();
        }

        public bool SendMessage(Command command, byte byte1, byte byte2, byte byte3)
        {
            byte[] message = new byte[4];

            message[0] = Message.CreateHeader(deviceID, command);
            message[1] = byte1;
            message[2] = byte2;
            message[3] = byte3;

            serialPort.Write(message, 0, 4);

            return true;
        }

        public bool SendMessage(Command command, byte byte1, int data)
        {
            var dataBytes = BitConverter.GetBytes(data);

            return SendMessage(command, byte1, dataBytes[1], dataBytes[0]);
        }

        public bool SendMessage(Command command, int data)
        {
            var dataBytes = BitConverter.GetBytes(data);

            return SendMessage(command, dataBytes[2], dataBytes[1], dataBytes[0]);
        }

        public bool SendData(byte dataID, int data)
        {
            SendMessage(Command.Set, dataID, data);

            byte[] confirmationMessage = new byte[4];
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool reading = true;
            int messageIndex = 0;

            while (reading)
            {
                var numBytes = serialPort.BytesToRead;
                if (numBytes < 1)
                    continue;

                serialPort.Read(confirmationMessage, messageIndex, numBytes);
                messageIndex += numBytes;

                if (messageIndex >= 3)
                {
                    reading = false;
                }
            }

            var expectedHeader = Message.CreateHeader(deviceID, Command.Return);

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
}