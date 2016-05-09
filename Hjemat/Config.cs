using System;
using System.IO.Ports;

namespace Hjemat
{
    public class Config
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
        
        public SerialPort CreateSerialPort()
        {
            var serialPort = new SerialPort();

            serialPort.PortName = this.serialConfig.portName;
            serialPort.BaudRate = this.serialConfig.baudRate;
            serialPort.Parity = this.serialConfig.parity;
            serialPort.StopBits = this.serialConfig.stopBits;
            serialPort.DataBits = this.serialConfig.dataBits;
            serialPort.Handshake = this.serialConfig.handshake;
            serialPort.ReadTimeout = this.serialConfig.readTimeout;

            return serialPort;
        }
    }
}