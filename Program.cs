// See https://aka.ms/new-console-template for more information
Console.WriteLine("Willkommen zum KnxUpdater!!");
Console.WriteLine();
Console.WriteLine($"IP-Adresse: {args[0]}");
Console.WriteLine($"IP-Port:    {args[1]}");
Console.WriteLine($"PA:         {args[2]}");
Console.WriteLine($"Firmware:   {args[3]}");
Console.WriteLine();

if(!args[3].EndsWith(".gz"))
{
    Console.WriteLine("Info: Es wird empfohlen die Firmware mit gzip zu komprimieren");
}

Kaenx.Konnect.Connections.KnxIpTunneling conn = new Kaenx.Konnect.Connections.KnxIpTunneling(args[0], int.Parse(args[1]));
await conn.Connect();
Kaenx.Konnect.Classes.BusDevice device = new Kaenx.Konnect.Classes.BusDevice(args[2], conn);
await device.Connect();

byte[] data = System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, args[3]));
Console.WriteLine($"Size:       {data} Bytes ({data.Length/1024} kB)");

Kaenx.Konnect.Messages.Response.MsgFunctionPropertyStateRes response = await device.InvokeFunctionProperty(0, 243, null, true);

int interval = 15;
int pause = args.Length < 5 ? 0 : int.Parse(args[4]);
int position = 0;
while(true)
{
    if(data.Length - position == 0) break;

    if(data.Length - position < interval)
        interval = data.Length - position;

    byte[] tosend = data.Skip(position).Take(interval).ToArray();

    int crcreq = KnxUpdater.CRC16.Get(tosend);
    Console.WriteLine($"CRC Req:  0x{crcreq:X4}");

    response = await device.InvokeFunctionProperty(0, 244, tosend, true);
    if(response.Data[0] != 0x00)
    {
        throw new Exception($"Fehler beim Übertragen: 0x{response.Data[0]}");
    }

    int crcresp = (response.Data[1] << 8) | response.Data[2];
    Console.WriteLine($"CRC Got:  0x{crcresp:X4}");

    if(crcreq != crcresp)
    {
        throw new Exception("Fehler beim Übertragen: Falscher CRC");
    }

    if(pause != 0) await Task.Delay(pause);
    position += interval;
}

await device.InvokeFunctionProperty(0, 245, null);

await device.Disconnect();
await conn.Disconnect();
Console.WriteLine("Update erfolgreich durchgeführt");