using LibBase;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APG_ABS_Sim {
    class Program {
        private static SerialPortClass _sp;
        private static List<Frame> _frames;
        private const int Interval = 1000;
        private static byte _syncNum = 0;
        private static byte _0300Times = 0;
        private static byte _0404Times = 0;

        static void Main(string[] args) {
            _sp = new SerialPortClass("COM6", 9600, Parity.None, 8, StopBits.One);
            try {
                _sp.OpenPort();
                _sp.DataReceived += new SerialPortClass.SerialPortDataReceiveEventArgs(SerialDataReceived);
                _frames = new List<Frame>();
                Console.WriteLine("Waiting for command");
            } catch (Exception ex) {
                Console.WriteLine("Open serial port error: " + ex.Message);
                throw;
            }
            try {
                while (true) {
                    if (_frames.Count > 0) {
                        if (_frames.First().Baud == 0x05 && _frames.First().Data == 0x83) {
                            _sp.SendData(new byte[] { 0x96, 0x55 }, 0, 2);
                            _sp.SendData(new byte[] { 0x96, 0x01 }, 0, 2);
                            _sp.SendData(new byte[] { 0x96, 0x8A }, 0, 2);
                        } else if (_frames.First().Baud == 0x96 && _frames.First().Data == 0x75) {
                            ++_0300Times;
                            SendData(0xF6, "ABSMadeByAPG");
                        } else if (_frames.First().Baud == 0x96 && _frames.First().Data != null) {
                            List<byte> cmd = new List<byte>();
                            RecvCMD(cmd);
                            switch (cmd[0]) {
                            case 0x03:
                                switch (cmd[2]) {
                                case 0x00:
                                    switch (++_0300Times) {
                                    case 1:
                                        SendData(0xF6, "ABSMadeByAPG");
                                        break;
                                    case 2:
                                        SendData(0xF6, "APG3550700E3");
                                        break;
                                    case 3:
                                        SendData(0xF6, "SWV ZHU V500");
                                        break;
                                    case 4:
                                        SendData(0xF6, new byte[] { 0x00, 0x07, 0xFA, 0x00, 0x01 });
                                        _0300Times = 0;
                                        break;
                                    }
                                    break;
                                case 0x05:
                                    // 不处理
                                    _0300Times = 0;
                                    SendData(0x09, Array.Empty<byte>());
                                    break;
                                case 0x06:
                                    // 系统复位退出
                                    _0300Times = 0;
                                    _0404Times = 0;
                                    _syncNum = 0;
                                    break;
                                case 0x07:
                                    SendData(0xFC, new byte[] { 0x01, 0x1B, 0x05, 0x01, 0x1D, 0x05, 0x01, 0x22, 0x05, 0x01, 0x1F, 0x05, 0x01, 0x2D, 0x05, 0x01, 0x2E, 0x05 });
                                    _0300Times = 0;
                                    break;
                                case 0x09:
                                    // 不处理
                                    _0300Times = 0;
                                    break;
                                default:
                                    _0300Times = 0;
                                    break;
                                }
                                break;
                            case 0x04:
                                switch (cmd[2]) {
                                case 0x04:
                                    switch (++_0404Times) {
                                    case 1:
                                        SendData(0xF5, new byte[] { 0x04, 0xFC });
                                        break;
                                    case 2:
                                        SendData(0xF5, new byte[] { 0x02, 0xA1 });
                                        break;
                                    case 3:
                                        SendData(0xF5, new byte[] { 0x02, 0xA3 });
                                        break;
                                    case 4:
                                        SendData(0xF5, new byte[] { 0x02, 0xA4 });
                                        break;
                                    case 5:
                                        SendData(0xF5, new byte[] { 0x02, 0xA5 });
                                        break;
                                    case 6:
                                        SendData(0xF5, new byte[] { 0x02, 0xA6 });
                                        break;
                                    case 7:
                                        SendData(0xF5, new byte[] { 0x02, 0xA3 });
                                        break;
                                    case 8:
                                        SendData(0xF5, new byte[] { 0x02, 0xA2 });
                                        break;
                                    case 9:
                                        SendData(0xF5, new byte[] { 0x02, 0xA1 });
                                        break;
                                    case 10:
                                        SendData(0xF5, new byte[] { 0x02, 0xA8 });
                                        break;
                                    case 11:
                                        SendData(0xF5, new byte[] { 0x02, 0xA9 });
                                        break;
                                    case 12:
                                        SendData(0xF5, new byte[] { 0x02, 0xAA });
                                        break;
                                    case 13:
                                        SendData(0xF5, new byte[] { 0x02, 0xAB });
                                        break;
                                    case 14:
                                        SendData(0xF5, new byte[] { 0x02, 0xA8 });
                                        break;
                                    case 15:
                                        SendData(0xF5, new byte[] { 0x02, 0xA2 });
                                        break;
                                    case 16:
                                        SendData(0xF5, new byte[] { 0x02, 0xA1 });
                                        break;
                                    case 17:
                                        SendData(0xF5, new byte[] { 0x02, 0xAD });
                                        break;
                                    case 18:
                                        SendData(0xF5, new byte[] { 0x02, 0xAE });
                                        break;
                                    case 19:
                                        SendData(0xF5, new byte[] { 0x02, 0xAF });
                                        break;
                                    case 20:
                                        SendData(0xF5, new byte[] { 0x02, 0xB0 });
                                        break;
                                    case 21:
                                        SendData(0xF5, new byte[] { 0x02, 0xAD });
                                        break;
                                    case 22:
                                        SendData(0xF5, new byte[] { 0x02, 0xA2 });
                                        break;
                                    case 23:
                                        SendData(0xF5, new byte[] { 0x02, 0xA1 });
                                        break;
                                    case 24:
                                        SendData(0xF5, new byte[] { 0x02, 0xB2 });
                                        break;
                                    case 25:
                                        SendData(0xF5, new byte[] { 0x02, 0xB3 });
                                        break;
                                    case 26:
                                        SendData(0xF5, new byte[] { 0x02, 0xB4 });
                                        break;
                                    case 27:
                                        SendData(0xF5, new byte[] { 0x02, 0xB5 });
                                        break;
                                    case 28:
                                        SendData(0xF5, new byte[] { 0x02, 0xB2 });
                                        break;
                                    case 29:
                                        SendData(0x0A, new byte[] { 0xAF });
                                        _0404Times = 0;
                                        break;
                                    }
                                    break;
                                case 0x29:
                                    SendData(0xE7, new byte[] { 0x01, 0x01, 0x01, 0x01 });
                                    _0404Times = 0;
                                    break;
                                default:
                                    _0404Times = 0;
                                    break;
                                }
                                break;
                            }
                        }
                        _frames.Clear();
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                throw;
            }
        }

        private static void SerialDataReceived(object sender, SerialDataReceivedEventArgs e, byte[] bits) {
            string log = string.Empty;
            for (int i = 0; i < bits.Length; i++) {
                log += bits[i].ToString("X2") + " ";
                if (_frames.Count > 0 && _frames[_frames.Count - 1].Data == null) {
                    _frames[_frames.Count - 1].Data = bits[i];
                } else {
                    if (bits[i] == 0x05 || bits[i] == 0x96) {
                        Frame frame = new Frame(bits[0]);
                        if (i + 1 < bits.Length) {
                            frame.Data = bits[i + 1];
                        }
                        _frames.Add(frame);
                    }
                }
            }
            Console.WriteLine("Recv: " + log);
        }

        private static bool SendByte(byte data) {
            _frames.Clear();
            _sp.SendData(new byte[] { 0x96, data }, 0, 2);
            DateTime start = DateTime.Now;
            while (_frames.Count <= 0) {
                TimeSpan interval = DateTime.Now - start;
                if (interval.TotalMilliseconds > Interval) {
                    Console.WriteLine("Receive verify code timeout");
                    return false;
                }
                Thread.Sleep(Interval / 5);
            }
            if (_frames[0].Data != (byte)~data) {
                Console.WriteLine(string.Format("Receive verify code wrong, Send: {0:X2}, Recv: {1:X2}", data, _frames[0].Data));
                return false;
            } else {
                return true;
            }
        }

        public static void SendData(byte funCode, string strData) {
            if (!SendByte((byte)(strData.Length + 3))) {
                return;
            }
            if (!SendByte(++_syncNum)) {
                return;
            }
            if (!SendByte(funCode)) {
                return;
            }
            foreach (byte item in strData) {
                if (!SendByte(item)) {
                    return;
                }
            }
            _sp.SendData(new byte[] { 0x96, 0x03 }, 0, 2);
        }

        public static void SendData(byte funCode, byte[] datas) {
            if (!SendByte((byte)(datas.Length + 3))) {
                return;
            }
            if (!SendByte(++_syncNum)) {
                return;
            }
            if (!SendByte(funCode)) {
                return;
            }
            foreach (byte item in datas) {
                if (!SendByte(item)) {
                    return;
                }
            }
            _sp.SendData(new byte[] { 0x96, 0x03 }, 0, 2);
        }

        private static bool RecvByte(out byte buf, bool bNotEnd) {
            buf = 0;
            for (int i = 0; i < 50; i++) {
                if (_frames.Count > 0) {
                    buf = (byte)_frames.First().Data;
                    if (bNotEnd || buf != 0x03) {
                        _sp.SendData(new byte[] { 0x96, (byte)~buf }, 0, 2);
                    } else {
                        Console.WriteLine("Terminal:" + buf.ToString("X2"));
                    }
                    _frames.Clear();
                    return true;
                } else {
                    Thread.Sleep(Interval / 5);
                }
            }
            return false;
        }

        public static bool RecvCMD(List<byte> cmd) {
            while (_frames.Count <= 0) {
                Thread.Sleep(Interval / 5);
            }
            if (RecvByte(out byte buf, cmd.Count < 3)) {
                cmd.Add(buf);
            } else {
                return false;
            }
            int size = buf;
            if (buf > 127) {
                size = (byte)~buf;
            }
            do {
                if (RecvByte(out buf, cmd.Count < size)) {
                    cmd.Add(buf);
                } else {
                    return false;
                }
            } while (!(buf == 0x03 && cmd.Count > size));
            _syncNum = cmd[1];
            return true;
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

}
