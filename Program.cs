// See https://aka.ms/new-console-template for more information
Console.WriteLine("Willkommen zum KnxUpdater!!");
Console.WriteLine();

if(args.Length == 0 || args[0] == "help" || args[0] == "--help")
{
    Console.WriteLine();
    Console.WriteLine("knxupdater <IP-Address> <PhysicalAddress> <PathToFirmware> (<Port> <Delay>)");
    Console.WriteLine();
    Console.WriteLine("IP-Address:      IP of the KNX-IP-interface");
    Console.WriteLine("PhysicalAddress: Address of the KNX-Device (ex. 1.2.120)");
    Console.WriteLine("PathToFirmware:  Path to the firmware.bin or firmware.bin.gz");
    Console.WriteLine("Port:            Optional - Port of the KNX-IP-interface (3671)");
    Console.WriteLine("Delay:           Optional - Delay after each telegram");
    return;
}

int port = args.Length < 4 ? 3671 : int.Parse(args[3]);

Console.WriteLine($"IP-Adresse: {args[0]}");
Console.WriteLine($"IP-Port:    {port}");
Console.WriteLine($"PA:         {args[1]}");
Console.WriteLine($"Firmware:   {args[2]}");
Console.WriteLine();


if(!File.Exists(args[2]))
{
    Console.WriteLine("Error: Das Programm kann die angegebene Firmware nicht finden");
    return;
}


if(!args[2].EndsWith(".gz"))
{
    Console.WriteLine("Info:  Es wird empfohlen die Firmware mit gzip zu komprimieren");
}

try
{
    Kaenx.Konnect.Connections.KnxIpTunneling conn = new Kaenx.Konnect.Connections.KnxIpTunneling(args[0], port);
    await conn.Connect();
    Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
    Kaenx.Konnect.Classes.BusDevice device = new Kaenx.Konnect.Classes.BusDevice(args[1], conn);
    await device.Connect();
    Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {args[1]} hergestellt");

    byte[] data = System.IO.File.ReadAllBytes(args[2]);
    Console.WriteLine($"Size:       {data.Length} Bytes ({data.Length/1024} kB)");

    uint fileSize = (uint)data.Length;
    byte[] initdata = BitConverter.GetBytes(fileSize);
    string xy = BitConverter.ToString(initdata);

    Kaenx.Konnect.Messages.Response.MsgFunctionPropertyStateRes response = await device.InvokeFunctionProperty(0, 243, initdata, true);

    bool canFancy = true;
    try{
        int top = Console.CursorTop;
    } catch {
        canFancy = false;
    }

    int interval = 228; // Number of data-bytes per telegram
    int pause = args.Length < 5 ? 0 : int.Parse(args[4]);
    int position = 0;
    DateTime lastCheck = DateTime.Now;
    bool firstSpeed = true;
    while(true)
    {
        if(data.Length - position == 0) break;

        if(data.Length - position < interval)
            interval = data.Length - position;

        byte[] tosend = data.Skip(position).Take(interval).ToArray();

        int crcreq = KnxUpdater.CRC16.Get(tosend);

        try{
            response = await device.InvokeFunctionProperty(0, 244, tosend, true);
        } catch (Exception ex) {
            if(firstSpeed && interval != 12)
            {
                interval = 12;
                Console.WriteLine("Checking speed " + interval);
                continue;
            }
            throw ex;
        }
        if(response.Data[0] != 0x00)
        {
            switch(response.Data[0])
            {
                case 0x01:
                    throw new Exception($"Es wurden nicht so viele Bytes geschrieben wie geschickt wurden");

                case 0x02:
                    throw new Exception($"Der Download wurde vom Modul abgebrochen");
            }
            throw new Exception($"Fehler beim Übertragen: 0x{response.Data[0]:X2}");
        }

        int crcresp = (response.Data[1] << 8) | response.Data[2];

        if(crcreq != crcresp)
        {
            throw new Exception($"Error: Fehler beim Übertragen -> Falscher CRC (Req: {crcreq:X4} / Res: {crcresp:X4})");
        }

        int progress = (int)Math.Floor(position * 100.0 / fileSize);
        if(progress > 100) progress = 100;

        TimeSpan time = DateTime.Now - lastCheck;
        int speed = (int)Math.Floor(interval / (double)time.TotalSeconds);
        int timeLeft = (int)Math.Ceiling((fileSize - position) / (double)speed);
        if(timeLeft > 9999)
            timeLeft = 9999;

        if(firstSpeed)
        {
            speed = 0;
            firstSpeed = false;
            timeLeft = 0;

            if(canFancy)
                Console.Write("Progress: [                    ]    % -     B/s -      s left");
        }

        if(canFancy)
        {
            Console.SetCursorPosition(36 - progress.ToString().Length, Console.CursorTop);
            Console.Write(progress);

            Console.SetCursorPosition(11, Console.CursorTop);
            for(int i = 0; i < ((int)Math.Floor(progress / 5.0)); i++)
                Console.Write("=");

            Console.SetCursorPosition(40, Console.CursorTop);
            for(int i = 0; i < 3 - speed.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(speed);
                
            Console.SetCursorPosition(50, Console.CursorTop);
            for(int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(timeLeft);
                
            Console.SetCursorPosition(0, Console.CursorTop);
        } else {
            Console.Write("Progress: [");
            for(int i = 0; i < ((int)Math.Floor(progress / 5.0)); i++)
                Console.Write("=");
            for(int i = 0; i < 20 - ((int)Math.Floor(progress / 5.0)); i++)
                Console.Write(" ");
            Console.Write("] ");

            for(int i = 0; i < (3 - progress.ToString().Length); i++)
                Console.Write(" ");
            Console.Write(progress + "% - ");
            
            for(int i = 0; i < 3 - speed.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(speed + " B/s - ");
            
            for(int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(timeLeft + " s left");
            Console.WriteLine();
        }


        position += interval;
        lastCheck = DateTime.Now;
        if(pause != 0) await Task.Delay(pause);
    }

    Console.WriteLine("Info:  Übertragung abgeschlossen. Gerät wird neu gestartet");
    await device.InvokeFunctionProperty(0, 245, null);

    await device.Disconnect();
    await conn.Disconnect();
    Console.WriteLine("Info:  Update erfolgreich durchgeführt");
} catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"Error: {ex.Message}");
}