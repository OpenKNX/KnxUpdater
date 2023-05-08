using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;



namespace ConsoleApp1
{
    internal class Program
    {
        static int errorCount = 0;
        static bool canFancy = true;
        static bool firstSpeed = true;
        static bool firstDraw = true;
        
        static Dictionary<string, int> arguments = new Dictionary<string, int> {
            {"port", 3671},
            {"delay", 0},
            {"pkg", 200},
            {"errors", 3},
            {"force", 0},
        };


        static async Task<int> Main(string[] args)
        {
            var version = typeof(Program).Assembly.GetName().Version;
            var versionString = "";
            if (version != null) 
                versionString = string.Format(" {0}.{1}.{2}", version.Major, version.Minor, version.Build);
            Console.WriteLine("Willkommen zum KnxUpdater{0}!!", versionString);
            Console.WriteLine();

            if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
            {
                Console.WriteLine();
                Console.WriteLine("KnxUpdater <IP-Address> <PhysicalAddress> <PathToFirmware> (--port=3671 --delay=0 --pkg=228 --errors=3 --force)"); // --verbose)");
                Console.WriteLine();
                Console.WriteLine("IP-Address:      IP of the KNX-IP-interface");
                Console.WriteLine("PhysicalAddress: Address of the KNX-Device (1.2.120)");
                Console.WriteLine("PathToFirmware:  Path to the firmware.bin or firmware.bin.gz");
                Console.WriteLine("Port:            Optional - Port of the KNX-IP-interface (3671)");
                Console.WriteLine("Delay:           Optional - Delay after each telegram (0 ms)");
                Console.WriteLine("Package (pkg):   Optional - data size to transfer in one telegram (228 bytes)");
                Console.WriteLine("Errors:          Optional - Max count of errors before abort update");
                Console.WriteLine("Force:           Ignore warnings from application check");
                return 0;
            }

            foreach (string arg in args.Where(a => a.StartsWith("--")))
            {
                string[] sp = arg.Split("=");
                string name = sp[0].Substring(2);
                if (sp.Length == 1)
                {
                    arguments[name] = 1;
                }
                else
                {
                    if (!arguments.ContainsKey(name))
                    {
                        Console.WriteLine("Unbekanntes Argument: " + name);
                        return -1;
                    }
                    arguments[name] = int.Parse(sp[1]);
                }
            }

            Console.WriteLine($"IP-Adresse: {args[0]}");
            Console.WriteLine($"IP-Port:    {arguments["port"]}");
            Console.WriteLine($"PA:         {args[1]}");
            Console.WriteLine($"Firmware:   {args[2]}");
            Console.WriteLine($"Package:    {arguments["pkg"]} bytes/telegram");
            Console.WriteLine($"Delay:      {arguments["delay"]} ms");
            Console.WriteLine($"Fehler:     {arguments["errors"]}x");
            Console.WriteLine();

            if (!File.Exists(args[2]))
            {
                Console.WriteLine("Error: Das Programm kann die angegebene Firmware nicht finden");
                return 0;
            }

            string extension = args[2].Substring(args[2].LastIndexOf("."));
            switch(extension)
            {
                case ".bin":
                    Console.WriteLine("Info:  Bei diesem Dateiformat kann die Kompatibilität\r\n       zur Applikation nicht überprüft werden.");
                    Console.WriteLine("Info:  (beta) Die Firmware wird komprimiert übertragen!");
                    break;

                case ".gz":
                    Console.WriteLine("Info:  Bei diesem Dateiformat kann die Kompatibilität\r\n       zur Applikation nicht überprüft werden.");
                    break;

                case ".uf2":
                    Console.WriteLine("Info:  (beta) Die Firmware wird komprimiert übertragen!");
                    break;
            }
            
            try { int top = Console.CursorTop; }
            catch { canFancy = false; }

            Kaenx.Konnect.Connections.KnxIpTunneling? conn = null;
            Kaenx.Konnect.Classes.BusDevice? device = null;
            var startTime = DateTime.Now;

            try
            {
                uint deviceOpenKnxId = 0;
                uint deviceAppNumber = 0;
                uint deviceAppVersion  = 0;
                uint deviceAppRevision = 0;

                conn = new Kaenx.Konnect.Connections.KnxIpTunneling(args[0], arguments["port"]);
                await conn.Connect();
                Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
                device = new Kaenx.Konnect.Classes.BusDevice(args[1], conn);
                await device.Connect();
                Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {args[1]} hergestellt");

                try
                {
                    byte[] res = await device.PropertyRead(0, 78);
                    if (res.Length > 0) {
                        deviceOpenKnxId = res[2];
                        deviceAppNumber = res[3];
                        deviceAppVersion = res[4];
                        deviceAppRevision = res[5];
                    } else {
                        throw new Exception("PropertyResponse für HardwareType war ungültig");
                    }
                }
                catch (Exception ex)
                {
                    Error(ex.Message);
                    return 1;
                }

                await device.Disconnect();
                using(MemoryStream stream = new MemoryStream())
                {
                    KnxUpdater.FileHandler.GetBytes(stream, args[2], arguments["force"] == 1, deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision);
                    await device.Connect();
                    Console.WriteLine($"Size:       {stream.Length} Bytes ({stream.Length / 1024} kB)");
                    Console.WriteLine();
                    Console.WriteLine();

                    uint fileSize = (uint)stream.Length;
                    byte[] initdata = BitConverter.GetBytes(fileSize);

                    KnxFtpClient.Lib.FtpClient client = new KnxFtpClient.Lib.FtpClient(device);
                    client.ProcessChanged += ProcessChanged;
                    await client.FileUpload("/firmware.bin", stream, arguments["pkg"]);
                }

                Console.WriteLine("Info:  Übertragung abgeschlossen. Gerät wird neu gestartet     ");
                await device.InvokeFunctionProperty(0, 245, null);

                await device.Disconnect();
                await conn.Disconnect();
                // we inform the user about the time the update was running
                var duration = DateTime.Now - startTime;
                Console.WriteLine("Info:  Update erfolgreich durchgeführt in {0:D}:{1:D2} Minuten mit {2} Byte/Sekunde", (int)duration.TotalMinutes, duration.Seconds, (int)duration.TotalSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message} ({DateTime.Now.ToString()})");
                Console.WriteLine("Info:  Update abgebrochen");
                if(device != null)
                {
                    try{
                        await device.InvokeFunctionProperty(0, 246, null);
                    } catch { }
                    await device.Disconnect();
                }
                if(conn != null)
                    await conn.Disconnect();
            }

            return 0;
        }

        static void ProcessChanged(int percent, int speed, int time)
        {
            Print(percent, speed, time);
        }

        static void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if(canFancy) 
            {
                int top = Console.CursorTop;
                Console.SetCursorPosition(0,  top);
                Console.WriteLine($"Error: ({errorCount+1}) {msg} ({DateTime.Now.ToString()})");
                firstDraw = true;
            } else {
                Console.WriteLine($"Error: ({errorCount+1}) {msg} ({DateTime.Now.ToString()})");
            }
            Console.ResetColor();
            
            errorCount++;
            if(arguments["errors"] < errorCount)
                throw new Exception($"Zu viele Fehler ({errorCount}) während der Verbindung");
        }

        static void Print(int progress, int speed, int timeLeft)
        {
            if(firstSpeed)
            {
                speed = 0;
                timeLeft = 0;
                firstSpeed = false;
            }
            if (firstDraw)
            {
                firstDraw = false;

                if (canFancy)
                    Console.Write("Progress: [                    ]    % -     B/s -      s left");
            }

            if (canFancy)
            {
                Console.SetCursorPosition(36 - progress.ToString().Length, Console.CursorTop);
                Console.Write(progress);

                Console.SetCursorPosition(11, Console.CursorTop);
                for (int i = 0; i < ((int)Math.Floor(progress / 5.0)); i++)
                    Console.Write("=");

                Console.SetCursorPosition(40, Console.CursorTop);
                for (int i = 0; i < 3 - speed.ToString().Length; i++)
                    Console.Write(" ");
                Console.Write(speed);

                Console.SetCursorPosition(50, Console.CursorTop);
                for (int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                    Console.Write(" ");
                Console.Write(timeLeft);

                Console.SetCursorPosition(0, Console.CursorTop);
            }
            else
            {
                Console.Write("Progress: [");
                for (int i = 0; i < ((int)Math.Floor(progress / 5.0)); i++)
                    Console.Write("=");
                for (int i = 0; i < 20 - ((int)Math.Floor(progress / 5.0)); i++)
                    Console.Write(" ");
                Console.Write("] ");

                for (int i = 0; i < (3 - progress.ToString().Length); i++)
                    Console.Write(" ");
                Console.Write(progress + "% - ");

                for (int i = 0; i < 3 - speed.ToString().Length; i++)
                    Console.Write(" ");
                Console.Write(speed + " B/s - ");

                for (int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                    Console.Write(" ");
                Console.Write(timeLeft + " s left");
                Console.WriteLine();
            }
        }
    }
}