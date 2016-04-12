using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;

namespace Hjemat
{
    class Message
    {
        public static SerialPort serialPort;

        public static byte CreateHeader(byte deviceID, Command command)
        {
            var commandID = (int)command;

            var header = deviceID << 3;
            header = header | commandID;

            return (byte)header;
        }

        public static Message CreatePing(byte deviceID)
        {
            return new Message(deviceID, Command.Ping, new byte?[3] { 0, 0, 0 });
        }

        public byte[] bytes = new byte[4];

        public Message(byte[] bytes)
        {
            for (int i = 0; i < 4; i++)
            {
                this.bytes[i] = bytes?[i] ?? 0;
            }
        }

        public Message(byte deviceID, Command command, byte?[] bytes)
        {
            this.bytes[0] = Message.CreateHeader(deviceID, command);

            for (int i = 1; i < 4; i++)
            {
                this.bytes[i] = bytes?[i - 1] ?? 0;
            }
        }

        public byte GetDeviceID()
        {
            return (byte)(bytes[0] >> 3);
        }

        public Command GetCommand()
        {
            // 7 = 00000111 bin
            return (Command)(bytes[0] & 7);
        }
        
        public byte GetHeader()
        {
            return bytes[0];
        }

        public byte[] GetDataBytes()
        {
            return new byte[3] { bytes[1], bytes[2], bytes[3] };
        }

        public static bool Send(byte[] bytes)
        {
            serialPort.Write(bytes, 0, 4);

            return true;
        }

        public bool Send()
        {
            return Send(bytes);
        }

        public static Message Read()
        {
            byte[] message = new byte[4];

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool reading = true;
            int messageIndex = 0;

            while (reading)
            {
                if (stopwatch.ElapsedMilliseconds > serialPort.ReadTimeout)
                {
                    throw new System.TimeoutException();
                }

                var numBytes = serialPort.BytesToRead;
                if (numBytes < 1)
                    continue;

                serialPort.Read(message, messageIndex, numBytes);
                messageIndex += numBytes;

                if (messageIndex >= 3)
                {
                    reading = false;
                }
            }

            stopwatch.Stop();

            return new Message(message);
        }
    }
}