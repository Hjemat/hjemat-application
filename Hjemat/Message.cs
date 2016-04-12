using System.IO.Ports;
using System.Linq;

namespace Hjemat
{
    class Message
    {
        public static byte CreateHeader(byte deviceID, Command command)
        {
            var commandID = (int)command;

            var header = deviceID << 3;
            header = header | commandID;

            return (byte)header;
        }

        public byte[] bytes = new byte[4];

        public Message(byte[] bytes)
        {
            for (int i = 0; i < 4; i++)
            {
                this.bytes[i] = bytes?[i] ?? 0;
            }
        }

        public Message(byte deviceID, Command command, byte[] bytes)
        {
            this.bytes[0] = Message.CreateHeader(deviceID, command);

            for (int i = 1; i < 4; i++)
            {
                this.bytes[i] = bytes?[i] ?? 0;
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
        
        public byte[] GetDataBytes()
        {
            return new byte[3] { bytes[1], bytes[2], bytes[3] };
        }

        public bool Send(SerialPort serialPort)
        {
            serialPort.Write(bytes, 0, 4);

            return true;
        }
    }
}