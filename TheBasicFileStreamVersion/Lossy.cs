using System.Buffers.Binary;
using Common;

// ReSharper disable InconsistentNaming

namespace TheBasicFileStreamVersion;

public class Op1aException : Exception
{
    public Op1aException(string? msg = null, Exception? inner = null) : base(msg, inner)
    {
    }
}

public class Lossy
{
    private static readonly Ul Ul_ClosedBodyPartition = new Ul("urn:smpte:ul:060e2b34.027f0101.0d010201.01030400");
    private static readonly Ul Ul_Rip = new Ul("urn:smpte:ul:060E2B34.02050101.0D010201.01110100");

    public const int bufferSize = 16 * 1024;

    public static void Copy(string pathMxf, string pathDst)
    {

        FileStream fsWrite;
        if (File.Exists(pathDst))
            fsWrite = new FileStream(pathDst, FileMode.Truncate, FileAccess.Write, FileShare.Read, bufferSize,
                FileOptions.WriteThrough);
        else
            fsWrite = new FileStream(pathDst, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize,
                FileOptions.WriteThrough);

        Copy(pathMxf, fsWrite);
    }

    public static void Copy(string pathMxf, Stream fsWrite)
    {
        using var fsRead = new FileStream(pathMxf, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize,
            FileOptions.RandomAccess);

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
        if (!Ul_Rip.Equals(buf16))
        {
            throw new Op1aException($"Failed to find RIP for {pathMxf}.");
        }

        var len = Asn1Ber.DecodeLength(fsRead);

        // find where the body starts
        long bodyoffset = 0;
        var readRip = 0;
        long lastPartition = 0;
        while (readRip < (len - 4)) // -4 to skip last rip property: final length
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

        if (bodyoffset == 0) throw new Op1aException("No body partition found in RIP.");

        Span<byte> buf = stackalloc byte[bufferSize];


        // copy header to dst
        long copied = 0;
        fsRead.Seek(0, SeekOrigin.Begin);

        var mod = bodyoffset % bufferSize;
        var justBeforeLastLittleBit = bodyoffset - mod;
        while (copied < justBeforeLastLittleBit)
        {
            fsRead.ReadExactly(buf);
            fsWrite.Write(buf);
            copied += bufferSize;
        }

        var lastLittleBit = buf.Slice(0, checked((int)mod));
        fsRead.ReadExactly(lastLittleBit);
        fsWrite.Write(lastLittleBit);


        // next should be the BODY PARTITION
        fsRead.ReadExactly(buf16);
        if (!Ul_ClosedBodyPartition.Equals(buf16))
            throw new Op1aException($"Expected body partition pack, got {Ul.ToUrn(buf16)}. Pos {fsRead.Position}.");

        // write body partition key
        fsWrite.Write(buf16);

        // r/w body partition length
        len = Asn1Ber.DecodeLength(fsRead);
        switch (len)
        {
            case > int.MaxValue:
                throw new NotImplementedException("Long length is (too) long.");
            case > 0xffffff:
                throw new NotImplementedException("Body partition pack length oughtta be < 0xffffff aka L=0x83");
            case > bufferSize:
                throw new Op1aException($"Expected body partition pack ({len}) to fit in the read buffer ({buf.Length}).");
            default:
                BinaryPrimitives.WriteInt32BigEndian(buf16, (int)len);
                fsWrite.Write([0x83]); // signal ber long, three bytes
                fsWrite.Write(buf16.Slice(1, 3)); // skip first byte, which is just 0x0 anyway
                break;
        }

        // r/w write body partition pack
        {
            var v = buf.Slice(0, checked((int)len));
            fsRead.ReadExactly(v);
            fsWrite.Write(v);
        }


        // now, essence!
        // we'll copy one triplet with the value zeroed out


        // copy essence key!
        fsRead.ReadExactly(buf16);
        fsWrite.Write(buf16);

        // r/w essence triplet length
        len = Asn1Ber.DecodeLength(fsRead);
        switch (len)
        {
            case > int.MaxValue:
                throw new NotImplementedException("Long length is (too) long.");
            case > 0xffffff:
                throw new NotImplementedException("Body partition length oughtta be < 0xffffff aka L=0x83");
            default:
                BinaryPrimitives.WriteInt32BigEndian(buf16, (int)len);
                fsWrite.Write([0x83]); // signal ber long, three bytes
                fsWrite.Write(buf16.Slice(1, 3)); // skip first byte, which is just 0x0 anyway
                break;
        }

        // fill essence value with zeroes
        if (len <= buf.Length)
        {
            var v = buf.Slice(0, checked((int)len));
            v.Clear();
            fsWrite.Write(v);
        }
        else
        {
            buf.Clear();
            copied = 0;
            mod = len % bufferSize;
            justBeforeLastLittleBit = bodyoffset - mod;
            while (copied < justBeforeLastLittleBit)
            {
                fsWrite.Write(buf);
                copied += buf.Length;
            }

            if (mod != 0) fsWrite.Write(buf.Slice(0, checked((int)mod)));
        }
        fsRead.Position += len;


        // proooooooooobably should rewrite the
        // index table and rip
        // but for now..


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