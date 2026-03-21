namespace Common;

public class Ul
{
    private readonly byte[] _octets = new byte[16];

    public Ul(ReadOnlySpan<char> urn)
    {
        if (urn.Length != 48) throw new Exception("Not an urn:smpte:ul");
        var deurned = urn.Slice("urn:smpte:ul:".Length);
        var delimiterBump = 0; // usually period, but who cares which exact char
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) delimiterBump += 1;
            _octets[i] = (byte)(C2B(deurned[(i * 2) + delimiterBump]) << 4 | C2B(deurned[(i * 2) + 1 + delimiterBump]));
        }

        if (_octets[4] == 0x02) _octets[5] = 0x7f;
    }

    public bool Equals(Ul other) => _octets.SequenceEqual(other._octets);
    public bool Equals(ReadOnlySpan<byte> other)
    {
        if (other[4] == 0x02)
        {
            // byte5 means the keys is from the groups register
            // byte6 indicates local set vs whatever else
            // an urn ul, copied from docs, will have byte6 set to 7f
            // a wild ul, copied from an mxf, will not
            // so, alter byte6 to make equals work
            byte[] copy = [..other];
            copy[5] = 0x7f;
            other = copy;
        }
        return _octets.AsSpan().SequenceEqual(other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Ul)obj);
    }

    public override int GetHashCode() => _octets.GetHashCode();

    public override string ToString() => ToUrn(_octets);

    public static string ToUrn(ReadOnlySpan<byte> s)
    {
        var p1 = Convert.ToHexString(s.Slice(0, 4));
        var p2 = Convert.ToHexString(s.Slice(4, 4));
        var p3 = Convert.ToHexString(s.Slice(8, 4));
        var p4 = Convert.ToHexString(s.Slice(12, 4));
        return $"urn:smpte:ul:{p1}.{p2}.{p3}.{p4}";
    }

    private static byte C2B(char c)
    {
        switch (c)
        {
            case '0':
                return 0;
            case '1':
                return 1;
            case '2':
                return 2;
            case '3':
                return 3;
            case '4':
                return 4;
            case '5':
                return 5;
            case '6':
                return 6;
            case '7':
                return 7;
            case '8':
                return 8;
            case '9':
                return 9;
            case 'a':
            case 'A':
                return 10;
            case 'b':
            case 'B':
                return 11;
            case 'c':
            case 'C':
                return 12;
            case 'd':
            case 'D':
                return 13;
            case 'e':
            case 'E':
                return 14;
            case 'f':
            case 'F':
                return 15;
            default:
                throw new Exception($"Not a hex char: '{c}'.");
        }
    }
}