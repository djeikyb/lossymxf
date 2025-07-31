using System.Buffers.Binary;

namespace Common;

public static class Asn1Ber
{
    public static long DecodeLength(Stream stream)
    {
        Span<byte> buf = stackalloc byte[8];
        var maybe = buf.Slice(0, 1);
        stream.ReadExactly(maybe);
        if (maybe[0] <= 0x7f) return maybe[0];
        if (maybe[0] == 0x83)
        {
            buf[0] = 0x0;
            var n = buf.Slice(1, 3);
            stream.ReadExactly(n);
            return BinaryPrimitives.ReadInt32BigEndian(buf.Slice(0, 4));
        }

        if (maybe[0] == 0x87)
        {
            buf[0] = 0x0;
            var n = buf.Slice(1, 7);
            stream.ReadExactly(n);
            return BinaryPrimitives.ReadInt64BigEndian(buf.Slice(0, 8));
        }

        throw new Exception($"this is a weird L in this klv. normal is 0x83 (int32) or 0x87 (int64), got decimal len {(int)maybe[0]}.");
    }
}