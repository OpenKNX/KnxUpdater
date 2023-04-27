

using System.IO.Compression;

namespace KnxUpdater;


public class Converter
{

    public static byte[] ToBin(string path, bool force, uint openknxid, uint appNumber, uint appVersion, uint appRevision)
    {
        using(MemoryStream ms = new MemoryStream())
        {
            bool checkedApp = false;
            int counter = 0;
            long addr = -1;
            Block block;
            do
            {
                block = ParseBlock(path, counter);

                if(block.IsValid && !block.FlagNotMainFlash)
                {
                    if(!checkedApp && block.Tags.Count > 0)
                    {
                        Tag? tag = block.Tags.SingleOrDefault(t => t.Type == 0x010101);
                        if(tag != null && tag.Type == 0x010101)
                        {
                            checkedApp = true;
                            if(!CheckApplication(tag, force, openknxid, appNumber, appVersion, appRevision))
                                throw new Exception("Update wurde aufgrund Inkompatibilität abgebrochen.");
                            
                            Console.WriteLine($"Conv:  Datei enthält OpenKnxId={openknxid:X2} AppNumber={appNumber:X2} AppVersion={appVersion:X2} AppRevision={appRevision:X2}");
                        }
                    }

                    if(addr == -1)
                        addr = block.Address;

                    var padding = block.Address - addr;
                    if (padding < 0) {
                        throw new DataMisalignedException(string.Format("Blockreihenfolge falsch an Position {0}", addr));
                    }
                    if (padding > 10 * 1024 * 1024) {
                        throw new DataMisalignedException(string.Format("Mehr als 10M zum Auffüllen (padding) benötigt an Position {0}", addr));
                    }
                    if (padding % 4 != 0) {
                        throw new DataMisalignedException(string.Format("Adresse zum Auffüllen (padding) nicht an einer Wortgrenze ausgerichtet an Position {0}", addr));
                    }
                    while (padding > 0) {
                        padding -= 4;
                        ms.Write(BitConverter.GetBytes(0), 0, 4);
                    }
                    addr += block.Size;

                    ms.Write(block.Data, 0, block.Data.Length);
                }
                else
                    Console.WriteLine($"Conv:  Block an Position {counter} ignoriert; Falsche 'magic number'!");

                counter++;
            } while(block.Sequence < block.BlockCount -1);

            
            ms.Flush();
            return ms.ToArray();
        }
    }

    private static bool CheckApplication(Tag tag, bool force, uint deviceOpenKnxId,  uint deviceAppNumber, uint deviceAppVersion, uint deviceAppRevision)
    {
        uint openKnxId = tag.Data[0];
        uint appNumber = tag.Data[1];
        uint appVersion = tag.Data[2];
        uint appRevision = tag.Data[3];

        if(openKnxId != deviceOpenKnxId)
        {
            Console.WriteLine("Conv:  Die OpenKnxId auf dem Gerät ist {0:X2}, die der Firmware ist {1:X2}.", deviceOpenKnxId, openKnxId);
            Console.WriteLine("       Das führt zu einem neuen Gerät, die PA ist dann 15.15.255.");
            Console.WriteLine("       Es muss komplett über die ETS neu aufgesetzt werden!");
            Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            return force ? true : Continue();
        } else if(appNumber != deviceAppNumber)
        {
            Console.WriteLine("Conv:  Die Applikationsnummer auf dem Gerät ist {0:X2}, die der Firmware ist {1:X2}.", deviceAppNumber, appNumber);
            Console.WriteLine("       Das führt zu einem neuen Gerät, die PA ist dann 15.15.255.");
            Console.WriteLine("       Es muss komplett über die ETS neu aufgesetzt werden!");
            Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            return force ? true : Continue();
        } else if (appVersion == deviceAppVersion) {
            Console.WriteLine("Conv:  Die Applikationsversion auf dem Gerät ist {0:X2}, die der Firmware auch.", deviceAppVersion);
            Console.WriteLine("       Da derzeit die Firmware-Revision nicht geprüft werden kann,");
            Console.WriteLine("       musst Du sicherstellen, dass Du nicht versehentlich ein Downgrade machst!");
            return force ? true : Continue();
        } else if (appVersion < deviceAppVersion) {
            Console.WriteLine("Conv:  Die Applikationsversion auf dem Gerät ist {0:X2}, die der Firmware ist {1:X2}.", deviceAppVersion, appVersion);
            Console.WriteLine("       Das führt zu einem Downgrade!");
            Console.WriteLine("       Das Gerät muss mit der ETS neu programmiert werden (die PA bleibt erhalten).");
            return force ? true : Continue();
        }

        return true;
    }

    private static bool Continue()
    {
        Console.Write("Comv:  Update trotzdem durchführen? ");
        var key = Console.ReadKey(false);
        Console.WriteLine();
        if (key.KeyChar == 'J' || key.KeyChar == 'j') {
            Console.WriteLine("Conv:  Update wird fortgesetzt!");
            return true;
        }
        return false;
    }

    private static Block ParseBlock(string path, int block)
    {
        List<uint> data = new List<uint>();
        byte[] file = System.IO.File.ReadAllBytes(path);
        file = file.Skip(block * 512).Take(512).ToArray();
        byte[] buffer = new byte[4];

        for(int i = 0; i < 9; i++)
        {
            int addr = i * 4;

            if(i == 8)
                addr = 508;

            for(int x = 0; x < 4; x++)
                buffer[x] = file[addr + x];

            uint num = BitConverter.ToUInt32(buffer);
            data.Add(num);
        }

        Block output = new Block();

        if(data[0] == 0x0A324655 && data[1] == 0x9E5D5157 && data[8] == 0x0AB16F30)
        {
            output.IsValid = true;
            output.Address = data[3];
            output.Size = data[4];
            output.FlagNotMainFlash = (data[2] & 0x00000001) != 0;
            output.FlagFileContainer = (data[2] & 0x00001000) != 0;
            output.FlagFamilyId = (data[2] & 0x00002000) != 0;
            output.FlagMD5 = (data[2] & 0x00004000) != 0;
            output.FlagExtensionTags = (data[2] & 0x00008000) != 0;

            output.DataLength = data[4];
            output.Sequence = data[5];
            output.BlockCount = data[6];
            output.Info = data[7];

            
            /*Console.WriteLine("-------------------------------------------");
            Console.WriteLine("Got correct block");
            Console.WriteLine("Flags:");
            Console.WriteLine(" - NotMainFlash:   " + output.FlagNotMainFlash);
            Console.WriteLine(" - FileContainer:  " + output.FlagFileContainer);
            Console.WriteLine(" - FamilyId:       " + output.FlagFamilyId);
            Console.WriteLine(" - MD5 Checksum:   " + output.FlagMD5);
            Console.WriteLine(" - Extension Tags: " + output.FlagExtensionTags);
            Console.WriteLine("Address:  " + output.Address);
            Console.WriteLine("Size:     " + output.Size);
            Console.WriteLine("Length:   " + output.DataLength);
            Console.WriteLine("Sequence: " + output.Sequence);
            Console.WriteLine("Blocks:   " + output.BlockCount);

            if(output.FlagFamilyId)
            {
                Console.WriteLine("FamilyId: " + output.FamilyName);
            }
            else if(output.FlagFileContainer)
                Console.WriteLine("FileSize: " + output.Info);
            */

            output.Data = file.Skip(32).Take((int)output.DataLength).ToArray();

            if(output.BlockCount - 1 == output.Sequence)
            {
                int xcounter = (int)output.DataLength - 1;
                while(output.Data[xcounter] == 0x00)
                {
                    xcounter--;
                }
                output.Data = output.Data.Take(xcounter+1).ToArray();
            }

            if(output.FlagExtensionTags)
            {
                uint addr = 32 + output.DataLength;
                while(file[addr] != 0 && file[addr+1] != 0)
                {
                    Tag tag = new Tag();
                    tag.Size = file[addr];
                    tag.Type = (uint)(file[addr+1] | file[addr+2] << 8 | file[addr+3] << 16);
                    tag.Data = file.Skip((int)addr+4).Take((int)tag.Size - 4).ToArray();
                    output.Tags.Add(tag);

                    uint padding = (4 - (tag.Size % 4)) % 4;
                    addr += tag.Size + padding;
                }   
            }
        }


        return output;
    }

    public static void ToGZip(string path)
    {
        using FileStream originalFileStream = File.Open(path, FileMode.Open);
        using FileStream compressedFileStream = File.Create(path + ".gz");
        using var compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
        originalFileStream.CopyTo(compressor);
    }
}

public class Block
{
    public bool IsValid { get; set; }

    public bool FlagNotMainFlash { get; set; }
    public bool FlagFileContainer { get; set; }
    public bool FlagFamilyId { get; set; }
    public bool FlagMD5 { get; set; }
    public bool FlagExtensionTags { get; set; }


    public uint Address { get; set; }
    public uint Size { get; set; }
    public uint BlockCount { get; set; }
    public uint DataLength { get; set; }
    public uint Sequence { get; set; }

    //FileSize or FamilyId or Zero
    public uint Info { get; set; }

    public byte[] Data { get; set; } = new byte[0];

    public List<Tag> Tags { get; set; } = new List<Tag>();

    
    public string FamilyName
    {
        get {
            if(FamilyNames.ContainsKey(Info))
                return FamilyNames[Info];
            return "Unknown";
        }
    }

    private Dictionary<uint, string> FamilyNames = new Dictionary<uint, string>() {
        {0x68ed2b88, "SAMD21"},
        {0xe48bff56, "RP2040"}
    };
}

public class Tag
{
    public uint Size { get; set; }
    public uint Type { get; set; }
    public byte[] Data { get; set; } = new byte[0];
}

public enum TagTypes
{
    FirmwareVersion = 0x9fc7bc,
    Description = 0x650d9d,
    PageSize = 0x0be9f7,
    SHA2 = 0xb46db0,
    DeviceTypeIdentifier = 0xc8a729
}