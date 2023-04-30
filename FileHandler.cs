using System.IO.Compression;

namespace KnxUpdater;


internal class FileHandler
{


    public static byte[] GetBytes(string path, bool force, uint openknxid, uint appNumber, uint appVersion, uint appRevision)
    {
        string extension = path.Substring(path.LastIndexOf("."));
        switch(extension)
        {
            case ".bin":
            {
                using (MemoryStream result = new MemoryStream())
                {
                    using (GZipStream compressionStream = new GZipStream(result, CompressionMode.Compress))
                    {
                        using(FileStream fs = System.IO.File.Open(path, FileMode.Open))
                        {
                            fs.CopyTo(compressionStream);
                            fs.Flush();
                        }
                    }
                    return result.ToArray();
                }
            }

            case ".gz":
                return System.IO.File.ReadAllBytes(path);

            case ".uf2":
            {
                using (MemoryStream result = new MemoryStream())
                {
                    using (GZipStream compressionStream = new GZipStream(result, CompressionMode.Compress))
                    {
                        byte[] data = Converter.ToBin(path, force, openknxid, appNumber, appVersion, appRevision);
                        foreach(byte d in data)
                            compressionStream.WriteByte(d);
                    }
                    return result.ToArray();
                }
            }
        }

        throw new Exception("Nicht unterstütztes Dateiformat: " + extension);
    }
}