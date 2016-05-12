using System;
using System.Diagnostics;
using System.IO.Ports;
using Raspberry.IO.GeneralPurpose;

namespace Hjemat
{
    class Message
    {
        public static OutputPinConfiguration rwPinConfig;
        public static GpioConnection rwPinConnection;

        public static SerialPort serialPort;

        public static byte CreateHeader(byte deviceID, CommandID command)
        {
            var commandID = (int)command;

            var header = deviceID << 3;
            header = header | commandID;

            return (byte)header;
        }
        
        public static byte CreateHeader(byte deviceID, CommandIDPair command)
        {
            var commandID = (int)command;

            var header = deviceID << 3;
            header = header | commandID;

            return (byte)header;
        }

        public static Message CreatePing(byte deviceID)
        {
            return new Message(deviceID, CommandID.Ping, new byte?[3] { 0, 0, 0 });
        }
        
        public static Message CreatePairAllow()
        {
            return new Message(0, CommandIDPair.Allow, new byte?[3] { 0, 0, 0 });
        }
        
        public static Message CreatePairReturn(byte deviceID)
        {
            return new Message(0, CommandIDPair.Return, new byte?[3] { deviceID, 0, 0 });
        }
        
        public static Message CreatePairStop()
        {
            return new Message(0, CommandIDPair.Stop, new byte?[3] { 0, 0, 0 });
        }

        public byte[] bytes = new byte[4];

        public Message(byte[] bytes)
        {
            for (int i = 0; i < 4; i++)
            {
                this.bytes[i] = bytes?[i] ?? 0;
            }
        }

        public Message(byte deviceID, CommandID command, byte?[] bytes)
        {
            this.bytes[0] = Message.CreateHeader(deviceID, command);

            for (int i = 1; i < 4; i++)
            {
                this.bytes[i] = bytes?[i - 1] ?? 0;
            }
        }
        
        public Message(byte deviceID, CommandIDPair command, byte?[] bytes)
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
        
        public int GetProductID()
        {
            Byte[]  array = { bytes[3], bytes[2], bytes[1], 0x00 };
            return BitConverter.ToInt32(array, 0);
        }

        public CommandID GetCommand()
        {
            // 7 = 00000111 bin
            return (CommandID)(bytes[0] & 7);
        }
        
        public byte GetHeader()
        {
            return bytes[0];
        }

        public byte[] GetDataBytes()
        {
            return new byte[3] { bytes[1], bytes[2], bytes[3] };
        }
        
        public short GetShortData()
        {
            return BitConverter.ToInt16(new byte[] { bytes[3], bytes[2] }, 0);
        }

        public static bool Send(byte[] bytes)
        {
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            
            rwPinConnection.Toggle(rwPinConfig);
            Program.Delay(5);

            serialPort.Write(bytes, 0, 4);

            Program.Delay(5);
            rwPinConnection.Toggle(rwPinConfig);

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
                    stopwatch.Stop();
                    throw new System.TimeoutException();
                }

                var numBytes = serialPort.BytesToRead;
                
                if (numBytes < 1)
                    continue;

                try
                {
                    serialPort.Read(message, messageIndex, numBytes);
                }
                catch (System.Exception)
                {
                    Console.WriteLine("ReadTimeout");
                    throw;
                }

                
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