using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APG_ABS_KLine {
    [Serializable]
    public class Setting {
        public string TCPServerIP { get; set; }
        public int TCPServerPort { get; set; }
        public string SerialPort { get; set; }
        public int SerialBaud { get; set; }
        public int Interval { get; set; } // 接收一个数据包的超时时间，单位ms

        public Setting() {
            TCPServerIP = "10.10.100.254";
            TCPServerPort = 8899;
            SerialPort = "COM5";
            SerialBaud = 9600;
            Interval = 1000;
        }
    }

    public class Frame {
        public byte Baud { get; set; }
        public byte? Data { get; set; }

        public Frame(byte baud, byte? data = null) {
            Baud = baud;
            Data = data;
        }
    }

    public enum ErrCode {
        NoError = 0,
        Unknown,
        Timeout,
        RecvDataWrong,
        VerifyError,
    }
}
