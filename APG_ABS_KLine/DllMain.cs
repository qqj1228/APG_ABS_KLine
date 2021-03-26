using LibBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APG_ABS_KLine {
    public class DllMain {
        private readonly Logger _log;
        private readonly Config _cfg;
        private SerialPortClass _sp;
        private readonly Queue<Frame> _frames;
        private readonly byte _addr, _sync, _k1, _k2, _k3;
        private readonly int _loopInterval;
        private readonly int _retryTimes;
        private static byte _synNum;

        public DllMain(string basePath) {
            _log = new Logger(basePath + "\\log", EnumLogLevel.LogLevelAll, true, 100);
            _log.TraceInfo("===========================================================================");
            _log.TraceInfo("===================== START DllVersion: " + DllVersion<DllMain>.AssemblyVersion + " =====================");
            _cfg = new Config(basePath, _log);
            _cfg.LoadConfigAll();
            _addr = 0x83;
            _sync = 0x55;
            _k1 = 0x01;
            _k2 = 0x8A;
            _k3 = 0x75;
            _loopInterval = 20;
            _retryTimes = 2;
            _frames = new Queue<Frame>();
        }

        public void ConnectCOMPort() {
            _sp = new SerialPortClass(_cfg.Setting.Data.SerialPort, _cfg.Setting.Data.SerialBaud, Parity.None, 8, StopBits.One);
            try {
                _sp.OpenPort();
                _sp.DataReceived += new SerialPortClass.SerialPortDataReceiveEventArgs(SerialDataReceived);
            } catch (Exception ex) {
                _log.TraceError("Open serial port error: " + ex.Message);
                throw;
            }
        }

        public void ReleaseCOMPort() {
            _sp.ClosePort();
        }

        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e, byte[] bits) {
            string log = string.Empty;
            for (int i = 0; i < bits.Length; i++) {
                log += bits[i].ToString("X2") + " ";
                Frame frameBuf = new Frame(0, 0);
                if (frameBuf.Data == null) {
                    frameBuf.Data = bits[i];
                    _frames.Enqueue(frameBuf);
                } else {
                    if (bits[i] == 0x05 || bits[i] == 0x96) {
                        frameBuf.Baud = bits[i];
                        frameBuf.Data = null;
                        if (i + 1 < bits.Length) {
                            log += bits[i + 1].ToString("X2") + " ";
                            frameBuf.Data = bits[i + 1];
                            ++i;
                        } else {
                            break;
                        }
                        _frames.Enqueue(frameBuf);
                    }
                }
            }
            //#if DEBUG
            Console.WriteLine("Recv: " + log);
            //#endif
        }

        private void SendSerialData(byte[] data, int offset, int count) {
            _sp.SendData(data, offset, count);
            string log = string.Empty;
            foreach (byte item in data) {
                log += item.ToString("X2") + " ";
            }
            //#if DEBUG
            Console.WriteLine("Send: " + log);
            //#endif
        }

        private int SendByte(byte data, out byte verifyCode) {
            verifyCode = 0;
            SendSerialData(new byte[] { 0x96, data }, 0, 2);
            DateTime start = DateTime.Now;
            while (true) {
                if (_frames.Count > 0) {
                    break;
                }
                TimeSpan interval = DateTime.Now - start;
                if (interval.TotalMilliseconds > _cfg.Setting.Data.Interval) {
                    return (int)ErrCode.Timeout;
                }
                Thread.Sleep(_loopInterval);
            }
            Frame frame = _frames.Dequeue();
            verifyCode = (byte)(frame.Data.HasValue ? frame.Data : 0);
            if (frame.Data != (byte)~data) {
                _log.TraceError(string.Format("VerifyError, Send: {0:X2}, Recv: {1:X2}", data, frame.Data));
                return (int)ErrCode.VerifyError;
            } else {
                return (int)ErrCode.NoError;
            }
        }

        public int SendCMD(byte[] cmd) {
            string log = string.Empty;
            int errCode = SendByte(cmd[0], out byte verifyCode);
            //log += string.Format("{0:X2}({1:X2})", cmd[0], verifyCode);
            log += string.Format("{0:X2} ", cmd[0]);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceInfo("TX: " + log);
                return errCode;
            }
            errCode = SendByte(++_synNum, out verifyCode);
            //log += string.Format("{0:X2}({1:X2})", _synNum, verifyCode);
            log += string.Format("{0:X2} ", _synNum);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceInfo("TX: " + log);
                return errCode;
            }
            for (int i = 1; i < cmd.Length; i++) {
                errCode = SendByte(cmd[i], out verifyCode);
                //log += string.Format("{0:X2}({1:X2})", cmd[i], verifyCode);
                log += string.Format("{0:X2} ", cmd[i]);
                if (errCode != (int)ErrCode.NoError) {
                    _log.TraceInfo("TX: " + log);
                    return errCode;
                }
            }
            SendSerialData(new byte[] { 0x96, 0x03 }, 0, 2);
            log += 0x03.ToString("X2");
            _log.TraceInfo("TX: " + log);
            return (int)ErrCode.NoError;
        }

        private bool RecvByte(out byte buf, bool bNotEnd, out byte resp) {
            buf = 0;
            resp = 0xFF;
            int loopNum = _cfg.Setting.Data.Interval / _loopInterval;
            for (int i = 0; i < loopNum; i++) {
                if (_frames.Count > 0) {
                    Frame frame = _frames.Dequeue();
                    buf = (byte)(frame.Data.HasValue ? frame.Data : 0);
                    if (bNotEnd || buf != 0x03) {
                        resp = (byte)~buf;
                        SendSerialData(new byte[] { 0x96, resp }, 0, 2);
                    }
                    return true;
                } else {
                    Thread.Sleep(_loopInterval);
                }
            }
            return false;
        }

        public int RecvData(List<byte> datas) {
            string log = string.Empty;
            List<byte> resps = new List<byte>();
            if (RecvByte(out byte buf, datas.Count < 3, out byte resp)) {
                datas.Add(buf);
                resps.Add(resp);
            } else {
                for (int i = 0; i < datas.Count; i++) {
                    if (i == datas.Count - 1 && (i >= resps.Count || datas[i] != (byte)~resps[i])) {
                        log += string.Format("{0:X2}", datas[i]);
                    } else {
                        //log += string.Format("{0:X2}({1:X2})", datas[i], resps[i]);
                        log += string.Format("{0:X2} ", datas[i]);
                    }
                }
                _log.TraceInfo("RX: " + log);
                return (int)ErrCode.Timeout;
            }
            int size = buf;
            if (buf > 127) {
                size = (byte)~buf;
            }
            do {
                if (RecvByte(out buf, datas.Count < size, out resp)) {
                    datas.Add(buf);
                    resps.Add(resp);
                } else {
                    for (int i = 0; i < datas.Count; i++) {
                        if (i == datas.Count - 1 && (i >= resps.Count || datas[i] != (byte)~resps[i])) {
                            log += string.Format("{0:X2}", datas[i]);
                        } else {
                            //log += string.Format("{0:X2}({1:X2})", datas[i], resps[i]);
                            log += string.Format("{0:X2} ", datas[i]);
                        }
                    }
                    _log.TraceInfo("RX: " + log);
                    return (int)ErrCode.Timeout;
                }
            } while (!(buf == 0x03 && datas.Count > size));
            _synNum = datas[1];
            for (int i = 0; i < datas.Count; i++) {
                if (i == datas.Count - 1 && (i >= resps.Count || datas[i] != (byte)~resps[i])) {
                    log += string.Format("{0:X2}", datas[i]);
                } else {
                    //log += string.Format("{0:X2}({1:X2})", datas[i], resps[i]);
                    log += string.Format("{0:X2} ", datas[i]);
                }
            }
            _log.TraceInfo("RX: " + log);
            return (int)ErrCode.NoError;
        }

        public int Initialize(out string DABS) {
            DABS = string.Empty;
            _frames.Clear();
            SendSerialData(new byte[] { 0x05, _addr }, 0, 2);
            _log.TraceInfo(string.Format("TX(5 Baud): {0:X2}", _addr));
            DateTime start = DateTime.Now;
            while (_frames.Count < 3) {
                TimeSpan interval = DateTime.Now - start;
                if (interval.TotalMilliseconds > _cfg.Setting.Data.Interval * 4) {
                    return (int)ErrCode.Timeout;
                }
                Thread.Sleep(_loopInterval);
            }
            Frame sync = _frames.Dequeue();
            Frame k1 = _frames.Dequeue();
            Frame k2 = _frames.Dequeue();
            _log.TraceInfo(string.Format("RX: {0:X2} {1:X2} {2:X2}", sync.Data, k1.Data, k2.Data));
            if (sync.Data == _sync && k1.Data == _k1 && k2.Data == _k2) {
                SendSerialData(new byte[] { 0x96, _k3 }, 0, 2);
                _log.TraceInfo(string.Format("TX: {0:X2}", _k3));

                List<byte> datas = new List<byte>();
                int errCode = RecvData(datas);
                for (int i = 0; i < _retryTimes; i++) {
                    if (errCode == (int)ErrCode.NoError) {
                        break;
                    }
                    _log.TraceError(((ErrCode)errCode).ToString());
                    SendSerialData(new byte[] { 0x96, _k3 }, 0, 2);
                    _log.TraceInfo(string.Format("TX: {0:X2}", _k3));
                    datas.Clear();
                    errCode = RecvData(datas);
                }
                if (errCode != (int)ErrCode.NoError) {
                    _log.TraceError(((ErrCode)errCode).ToString());
                    return errCode;
                }
                DABS = string.Empty;
                for (int i = 3; i < datas[0]; i++) {
                    DABS += (char)datas[i];
                }
                if (datas[2] == 0xF6) {
                    return (int)ErrCode.NoError;
                } else {
                    _log.TraceError("RecvDataWrong");
                    return (int)ErrCode.RecvDataWrong;
                }
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
        }

        /// <summary>
        /// 发送 0300 命令，接受单个ECU描述信息
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public int GetOneECUInfo(out string info) {
            info = string.Empty;
            int errCode = SendCMD(new byte[] { 0x03, 0x00 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x03, 0x00 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            info = string.Empty;
            for (int i = 3; i < datas[0]; i++) {
                info += (char)datas[i];
            }
            if (datas[2] == 0xF6) {
                return (int)ErrCode.NoError;
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
        }

        /// <summary>
        /// 发送 0300 命令，返回byte[]数据
        /// </summary>
        /// <param name="infos"></param>
        /// <returns></returns>
        public int GetOneECUInfo(out byte[] infos) {
            infos = new byte[5];
            int errCode = SendCMD(new byte[] { 0x03, 0x00 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x03, 0x00 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            infos = new byte[datas[0] - 3];
            for (int i = 3; i < datas[0]; i++) {
                infos[i - 3] = datas[i];
            }
            if (datas[2] == 0xF6) {
                return (int)ErrCode.NoError;
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
        }

        /// <summary>
        /// 发送4次 0300 命令，接受ECU描述信息
        /// </summary>
        /// <param name="DABS"></param>
        /// <param name="DAPG"></param>
        /// <param name="DSWV"></param>
        /// <returns></returns>
        public int GetAllECUInfo(out string DABS, out string DAPG, out string DSWV) {
            DAPG = string.Empty;
            DSWV = string.Empty;

            int errCode = GetOneECUInfo(out DABS);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }

            errCode = GetOneECUInfo(out DAPG);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }

            errCode = GetOneECUInfo(out DSWV);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }

            errCode = GetOneECUInfo(out byte[] _);
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }

            return (int)ErrCode.NoError;
        }

        /// <summary>
        /// 发送 0305 命令，清除DTC，接受到 09 数据，ECU等待
        /// </summary>
        /// <returns></returns>
        public int ClearDTC() {
            int errCode = SendCMD(new byte[] { 0x03, 0x05 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x03, 0x05 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            if (datas[2] == 0x09) {
                return (int)ErrCode.NoError;
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
        }

        /// <summary>
        /// 发送 0306 命令，让ECU复位退出
        /// </summary>
        /// <returns></returns>
        public int Exit() {
            int errCode = SendCMD(new byte[] { 0x03, 0x06 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            return (int)ErrCode.NoError;
        }

        /// <summary>
        /// 发送 0307 命令，获取DTC数量
        /// </summary>
        /// <returns></returns>
        public int GetDTCCount(out int count) {
            count = 0;
            int errCode = SendCMD(new byte[] { 0x03, 0x07 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x03, 0x07 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            if (datas[2] == 0xFC) {
                count = (datas[0] - 3) / 3;
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
            return (int)ErrCode.NoError;
        }

        /// <summary>
        /// 发送 0307 命令，获取DTC
        /// </summary>
        /// <returns></returns>
        public int ReadDTC(out uint[] DTCs) {
            DTCs = new uint[1];
            int errCode = SendCMD(new byte[] { 0x03, 0x07 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x03, 0x07 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            DTCs = new uint[(datas[0] - 3) / 3];
            if (datas[2] == 0xFC) {
                for (int i = 3; i < datas[0]; i += 3) {
                    uint DTC = datas[i];
                    DTC <<= 8;
                    DTC += datas[i + 1];
                    DTC <<= 8;
                    DTC += datas[i + 2];
                    DTCs[i / 3 - 1] = DTC;
                }
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
            return (int)ErrCode.NoError;
        }

        /// <summary>
        /// 发送 040400 命令，测试执行元件，返回0xAF表示测试结束
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public int RoutineCtrl(out uint func) {
            func = 0;
            int errCode = SendCMD(new byte[] { 0x04, 0x04, 0x00 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x04, 0x04, 0x00 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            if (datas[2] == 0xF5 || datas[2] == 0x0A) {
                for (int i = 3; i < datas[0]; i++) {
                    func <<= 8;
                    func += datas[i];
                }
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
            return (int)ErrCode.NoError;
        }

        /// <summary>
        /// 转换测试功能号为描述信息
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string DecodeRoutineCode(uint func, out int act) {
            switch (func) {
            case 0x04FC:
                act = 1;
                return "准备测试";
            case 0x02A1:
                act = 2;
                return "请踩下制动踏板";
            case 0x02A2:
                act = 3;
                return "请松开制动踏板";
            case 0x02A3:
                act = 4;
                return "左前轮锁死";
            case 0x02A4:
                act = 5;
                return "左前轮保持";
            case 0x02A5:
                act = 6;
                return "左前轮自由";
            case 0x02A6:
                act = 7;
                return "左前轮保持";
            case 0x02A8:
                act = 8;
                return "右前轮锁死";
            case 0x02A9:
                act = 9;
                return "右前轮保持";
            case 0x02AA:
                act = 10;
                return "右前轮自由";
            case 0x02AB:
                act = 11;
                return "右前轮保持";
            case 0x02AD:
                act = 12;
                return "左后轮锁死";
            case 0x02AE:
                act = 13;
                return "左后轮保持";
            case 0x02AF:
                act = 14;
                return "左后轮自由";
            case 0x02B0:
                act = 15;
                return "左后轮保持";
            case 0x02B2:
                act = 16;
                return "右后轮锁死";
            case 0x02B3:
                act = 17;
                return "右后轮保持";
            case 0x02B4:
                act = 18;
                return "右后轮自由";
            case 0x02B5:
                act = 19;
                return "右后轮保持";
            case 0xAF:
                act = 0;
                return "ABS测试结束";
            default:
                act = 0;
                return "未知功能";
            }
        }

        /// <summary>
        /// 发送 042900 命令，读测量数据流
        /// </summary>
        /// <returns></returns>
        public int ReadSensorData(out uint sensorData) {
            sensorData = 0;
            int errCode = SendCMD(new byte[] { 0x04, 0x29, 0x00 });
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            List<byte> datas = new List<byte>();
            errCode = RecvData(datas);
            //for (int i = 0; i < _retryTimes; i++) {
            //    if (errCode == (int)ErrCode.NoError) {
            //        break;
            //    }
            //    _log.TraceError(((ErrCode)errCode).ToString());
            //    _frames.Clear();
            //    errCode = SendCMD(new byte[] { 0x04, 0x29, 0x00 });
            //    if (errCode != (int)ErrCode.NoError) {
            //        _log.TraceError(((ErrCode)errCode).ToString());
            //        return errCode;
            //    }
            //    datas.Clear();
            //    errCode = RecvData(datas);
            //}
            if (errCode != (int)ErrCode.NoError) {
                _log.TraceError(((ErrCode)errCode).ToString());
                return errCode;
            }
            if (datas[2] == 0xE7) {
                for (int i = 3; i < datas[0]; i++) {
                    sensorData <<= 8;
                    sensorData += datas[i];
                }
            } else {
                _log.TraceError("RecvDataWrong");
                return (int)ErrCode.RecvDataWrong;
            }
            return (int)ErrCode.NoError;
        }
    }

    /// <summary>
    /// 获取主程序版本类
    /// </summary>
    public static class MainFileVersion {
        public static Version AssemblyVersion {
            get { return ((Assembly.GetEntryAssembly()).GetName()).Version; }
        }

        public static Version AssemblyFileVersion {
            get { return new Version(FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileVersion); }
        }

        public static string AssemblyInformationalVersion {
            get { return FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion; }
        }
    }

    /// <summary>
    /// 获取dll版本类，需要传入dll主class数据类型
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class DllVersion<T> {
        public static Version AssemblyVersion {
            get { return Assembly.GetAssembly(typeof(T)).GetName().Version; }
        }
    }

}
