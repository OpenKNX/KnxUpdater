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
        
        static Dictionary<string, int> arguments = new Dictionary<string, int> {
            {"port", 3671},
            {"delay", 0},
            {"pkg", 228},
            {"errors", 3},
        //    {"verbose", 0}
        };


        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Willkommen zum KnxUpdater!!");
            Console.WriteLine();

            if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
            {
                Console.WriteLine();
                Console.WriteLine("knxupdater <IP-Address> <PhysicalAddress> <PathToFirmware> (--port=3671 --delay=0 --pkg=228 --errors=3)"); // --verbose)");
                Console.WriteLine();
                Console.WriteLine("IP-Address:      IP of the KNX-IP-interface");
                Console.WriteLine("PhysicalAddress: Address of the KNX-Device (1.2.120)");
                Console.WriteLine("PathToFirmware:  Path to the firmware.bin or firmware.bin.gz");
                Console.WriteLine("Port:            Optional - Port of the KNX-IP-interface (3671)");
                Console.WriteLine("Delay:           Optional - Delay after each telegram (0 ms)");
                Console.WriteLine("Package:         Optional - data size to transfer in one telegram (228 bytes)");
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


            if (!args[2].EndsWith(".gz"))
            {
                Console.WriteLine("Info:  Es wird empfohlen die Firmware mit gzip zu komprimieren");
            }
            
            try { int top = Console.CursorTop; }
            catch { canFancy = false; }

            Kaenx.Konnect.Connections.KnxIpTunneling? conn = null;
            Kaenx.Konnect.Classes.BusDevice? device = null;

            try
            {
                conn = new Kaenx.Konnect.Connections.KnxIpTunneling(args[0], arguments["port"]);
                await conn.Connect();
                Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
                device = new Kaenx.Konnect.Classes.BusDevice(args[1], conn);
                await device.Connect();
                Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {args[1]} hergestellt");

                byte[] data = System.IO.File.ReadAllBytes(args[2]);
                Console.WriteLine($"Size:       {data.Length} Bytes ({data.Length / 1024} kB)");

                uint fileSize = (uint)data.Length;
                byte[] initdata = BitConverter.GetBytes(fileSize);

                Kaenx.Konnect.Messages.Response.MsgFunctionPropertyStateRes response = await device.InvokeFunctionProperty(0, 243, initdata, true);

                int interval = arguments["pkg"]; // Number of data-bytes per telegram
                int position = 0;
                DateTime lastCheck = DateTime.Now;
                while (true)
                {
                    if (data.Length - position == 0) break;

                    if (data.Length - position < interval)
                        interval = data.Length - position;

                    List<byte> tosend = new List<byte>();
                    tosend.AddRange(data.Skip(position).Take(interval));

                    int crcreq = KnxUpdater.CRC16.Get(tosend.ToArray());
                    tosend.InsertRange(0, BitConverter.GetBytes((uint)position));

                    try
                    {
                        response = await device.InvokeFunctionProperty(0, 244, tosend.ToArray(), true);
                    }
                    catch (Exception ex)
                    {
                        if (firstSpeed && interval != 12)
                        {
                            interval = 12;
                            Console.WriteLine("Checking speed " + interval);
                            continue;
                        }
                        Error("Error: " + ex.Message);
                        continue;
                    }

                    if (response.Data[0] != 0x00)
                    {
                        switch (response.Data[0])
                        {
                            case 0x01:
                                throw new Exception($"Es wurden nicht so viele Bytes geschrieben wie geschickt wurden");

                            case 0x02:
                                throw new Exception($"Der Download wurde vom Modul abgebrochen");
                        }
                        throw new Exception($"Fehler beim Übertragen: 0x{response.Data[0]:X2}");
                    }

                    int crcresp = (response.Data[1] << 8) | response.Data[2];

                    if (crcreq != crcresp)
                    {
                        Error($"Fehler beim Übertragen -> Falscher CRC (Req: {crcreq:X4} / Res: {crcresp:X4})");
                        continue;
                    }

                    int progress = (int)Math.Floor(position * 100.0 / fileSize);
                    if (progress > 100) progress = 100;

                    TimeSpan time = DateTime.Now - lastCheck;
                    int speed = (int)Math.Floor(interval / (double)time.TotalSeconds);
                    int timeLeft = (int)Math.Ceiling((fileSize - position) / (double)speed);
                    if (timeLeft > 9999)
                        timeLeft = 9999;

                    Print(progress, speed, timeLeft);

                    position += interval;
                    lastCheck = DateTime.Now;
                    if (arguments["delay"] != 0) await Task.Delay(arguments["delay"]);
                }

                Console.WriteLine("Info:  Übertragung abgeschlossen. Gerät wird neu gestartet");
                await device.InvokeFunctionProperty(0, 245, null);

                await device.Disconnect();
                await conn.Disconnect();
                Console.WriteLine("Info:  Update erfolgreich durchgeführt");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Info:  Update abgebrochen");
                if(device != null)
                {
                    await device.InvokeFunctionProperty(0, 246, null);
                    await device.Disconnect();
                }
                if(conn != null)
                    await conn.Disconnect();
            }

            return 0;
        }

        static void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if(canFancy)
            {
                int top = Console.CursorTop;
                Console.SetCursorPosition(0,  top - 1);
                Console.WriteLine($"Error: ({errorCount+1}){msg}");
                Console.SetCursorPosition(0, top);
            } else {
                Console.WriteLine($"Error: ({errorCount+1}) {msg}");
            }
            Console.ResetColor();
            
            errorCount++;
            if(arguments["errors"] < errorCount)
                throw new Exception($"Zu viele Fehler ({errorCount}) während der Verbindung");
        }

        static void Print(int progress, int speed, int timeLeft)
        {
            if (firstSpeed)
            {
                speed = 0;
                firstSpeed = false;
                timeLeft = 0;

                if (canFancy)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.Write("Progress: [                    ]    % -     B/s -      s left");
                }
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