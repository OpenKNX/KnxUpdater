

using System.IO.Compression;

namespace KnxUpdater;


public class Converter
{

    public static void ToBin(string path)
    {
        FileStream fout = System.IO.File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firmware.bin"));

        int counter = 1;
        Block block;
        do
        {
            block = ParseBlock(path, counter++);

            if(block.IsValid && !block.FlagNotMainFlash)
                fout.Write(block.Data, 0, block.Data.Length);

        } while(block.Sequence < block.BlockCount -1);

        
        fout.Flush();
        fout.Close();
        fout.Dispose();
    }

    private static Block ParseBlock(string path, int block)
    {
        List<uint> data = new List<uint>();
        byte[] file = System.IO.File.ReadAllBytes(path);
        int fileSize = file.Length;
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