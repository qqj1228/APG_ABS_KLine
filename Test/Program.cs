using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APG_ABS_KLine;

namespace Test {
    class Program {
        static void Main(string[] args) {
            DllMain kl = new DllMain(Environment.CurrentDirectory);
            kl.ConnectCOMPort();

            Console.WriteLine("Start Initialize()...");
            int code = InitAndGetAllECUInfo(kl, out string DABS, out string DAPG, out string DSWV);
            Console.WriteLine("Initialize() return code: " + (ErrCode)code);
            Console.WriteLine(string.Format("DABS:{0}, DAPG:{1}, DSWV:{2}", DABS, DAPG, DSWV));
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Start GetDescription()...");
            code = kl.GetAllECUInfo(out DABS, out DAPG, out DSWV);
            Console.WriteLine("GetDescription() return code: " + (ErrCode)code);
            Console.WriteLine(string.Format("DABS:{0}, DAPG:{1}, DSWV:{2}", DABS, DAPG, DSWV));
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Start GetDTC()...");
            code = kl.ReadDTC(out uint[] DTCs);
            Console.WriteLine("GetDTC() return code: " + (ErrCode)code);
            foreach (uint item in DTCs) {
                Console.Write(item.ToString("X6") + " ");
            }
            Console.WriteLine();
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Start ClearDTC()...");
            code = kl.ClearDTC();
            Console.WriteLine("ClearDTC() return code: " + (ErrCode)code);
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Start RoutineAll()...");
            code = RoutineAll(kl);
            Console.WriteLine("RoutineAll() return code: " + (ErrCode)code);
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Start ReadSensorData()...");
            for (int i = 0; i < 43; i++) {
                code = kl.ReadSensorData(out uint data);
                Console.WriteLine("ReadSensorData() return code: " + (ErrCode)code);
                Console.WriteLine(string.Format("LF:{0:X2}, RF:{1:X2}, LR:{2:X2}, RR:{3:X2} - {4}Time(s)",
                    (data >> 24) & 0x000000FF, (data >> 16) & 0x000000FF, (data >> 8) & 0x000000FF, data & 0x000000FF, i + 1));
                if (code != (int)ErrCode.NoError) {
                    Console.ReadKey();
                    return;
                }
            }

            Console.WriteLine("Start Exit()...");
            code = kl.Exit();
            Console.WriteLine("Exit() return code: " + (ErrCode)code);
            if (code != (int)ErrCode.NoError) {
                Console.ReadKey();
                return;
            }

            kl.ReleaseCOMPort();
            Console.ReadKey();
        }

        static int InitAndGetAllECUInfo(DllMain kl, out string DABS, out string DAPG, out string DSWV) {
            DAPG = string.Empty;
            DSWV = string.Empty;

            int errCode = kl.Initialize(out DABS);
            if (errCode != (int)ErrCode.NoError) {
                return errCode;
            }
            Console.WriteLine("DABS: " + DABS);

            errCode = kl.GetOneECUInfo(out DAPG);
            if (errCode != (int)ErrCode.NoError) {
                return errCode;
            }
            Console.WriteLine("DAPG: " + DAPG);

            errCode = kl.GetOneECUInfo(out DSWV);
            if (errCode != (int)ErrCode.NoError) {
                return errCode;
            }
            Console.WriteLine("DSWV: " + DSWV);

            errCode = kl.GetOneECUInfo(out byte[] temp);
            if (errCode != (int)ErrCode.NoError) {
                return errCode;
            }
            Console.Write("Others: ");
            foreach (var item in temp) {
                Console.Write(item.ToString("X2") + " ");
            }
            Console.WriteLine();

            return (int)ErrCode.NoError;
        }

        static int RoutineAll(DllMain kl) {
            int errCode = kl.RoutineCtrl(out uint data);
            if (errCode != (int)ErrCode.NoError) {
                return errCode;
            }
            string msg = kl.DecodeRoutineCode(data, out int act);
            Console.WriteLine(msg + "," + act);

            while (data != 0xAF) {
                errCode = kl.RoutineCtrl(out data);
                if (errCode != (int)ErrCode.NoError) {
                    return errCode;
                }
                msg = kl.DecodeRoutineCode(data, out act);
                Console.WriteLine(msg + "," + act);
            }

            return (int)ErrCode.NoError;
        }
    }
}
