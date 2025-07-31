using System.Buffers.Binary;
using Common;

namespace TheBasicFileStreamVersion;

public class Lossy
{
    public static void Copy(string pathMxf, string pathDst)
    {
        var fsReadBufferSize = 16 * 1024;
        using var fsRead = new FileStream(pathMxf, FileMode.Open, FileAccess.Read, FileShare.Read, fsReadBufferSize,
            FileOptions.RandomAccess);

        FileStream fsWrite;
        if (File.Exists(pathDst))
            fsWrite = new FileStream(pathDst, FileMode.Truncate, FileAccess.Write, FileShare.Read, fsReadBufferSize,
                FileOptions.WriteThrough);
        else
            fsWrite = new FileStream(pathDst, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, fsReadBufferSize,
                FileOptions.WriteThrough);

        using var _ = fsWrite;

        Span<byte> buf16 = stackalloc byte[16];
        Span<byte> buf8 = buf16.Slice(0, 8);
        Span<byte> buf4 = buf16.Slice(0, 4);

        // go to rip offset
        fsRead.Seek(-4, SeekOrigin.End);
        fsRead.ReadExactly(buf4);
        var ripOffset = BinaryPrimitives.ReadUInt32BigEndian(buf4);
        fsRead.Seek(-ripOffset, SeekOrigin.End);


        fsRead.ReadExactly(buf16); // key
        var ul_rip = new Ul("urn:smpte:ul:060E2B34.02050101.0D010201.01110100");
        if (!ul_rip.Equals(buf16))
        {
            throw new Exception($"Failed to find RIP for {pathMxf}.");
        }

        var klv_len = Asn1Ber.DecodeLength(fsRead);

        // find where the body starts
        long bodyoffset = 0;
        var readRip = 0;
        long lastPartition = 0;
        while (readRip < (klv_len - 4)) // -4 to skip last rip property: final length
        {
            fsRead.ReadExactly(buf4);
            readRip += 4;

            var bodysid = BinaryPrimitives.ReadUInt32BigEndian(buf4);

            fsRead.ReadExactly(buf8);
            readRip += 8;

            var offset = BinaryPrimitives.ReadUInt64BigEndian(buf8);
            lastPartition = checked((long)offset);
            if (bodysid == 1)
            {
                bodyoffset = lastPartition;
            }
        }

        if (bodyoffset == 0) throw new Exception("No body partition found in RIP.");

        // copy header to body
        long copied = 0;
        fsRead.Seek(0, SeekOrigin.Begin);

        var mod = bodyoffset % fsReadBufferSize;
        var justBeforeLastLittleBit = bodyoffset - mod;
        Span<byte> buf = stackalloc byte[fsReadBufferSize];
        while (copied < justBeforeLastLittleBit)
        {
            fsRead.ReadExactly(buf);
            fsWrite.Write(buf);
            copied += fsReadBufferSize;
        }

        fsRead.ReadExactly(buf.Slice(0, checked((int)mod)));

        // copy body key
        fsRead.ReadExactly(buf16);
        fsWrite.Write(buf16);

        // write new body length
        var valueLength = fsReadBufferSize - 16 - 1 - 4; // buf - key - berlongindicator - len
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (valueLength > 0xffffff)
            throw new Exception("chopped klv body length must be less than 0xffffff aka three bytes aka ber long 0x83");
        buf16.Clear();
        BinaryPrimitives.WriteInt32BigEndian(buf16, valueLength);

        fsWrite.Write([0x83]); // signal ber long, three bytes
        fsWrite.Write(buf16.Slice(1, 3)); // skip first byte, which is just 0x0 anyway

        // write new body of zeroes
        var bodyValue = buf.Slice(0, valueLength);
        bodyValue.Fill(0);
        fsWrite.Write(bodyValue);

        // copy everything after the body
        fsRead.Seek(lastPartition, SeekOrigin.Begin);
        int r;
        while ((r = fsRead.Read(buf)) > 0)
        {
            fsWrite.Write(buf.Slice(0, r));
        }

        // close shop
        try
        {
            fsWrite.Flush();
        }
        catch (Exception e)
        {
            Console.WriteLine($"failed to flush writer ({pathMxf})\n{e}");
            // logger.ZLogError(e, $"failed to flush writer ({pathMxf})");
        }

        try
        {
            fsWrite.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"failed to flush writer ({pathMxf})\n{e}");
            // logger.ZLogError(e, $"failed to flush writer ({pathMxf})");
        }

        try
        {
            fsRead.Flush();
        }
        catch (Exception e)
        {
            Console.WriteLine($"failed to flush writer ({pathMxf})\n{e}");
            // logger.ZLogError(e, $"failed to flush writer ({pathMxf})");
        }

        try
        {
            fsRead.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"failed to flush writer ({pathMxf})\n{e}");
            // logger.ZLogError(e, $"failed to flush writer ({pathMxf})");
        }
    }
}