#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace QiWa.Common;

/// <summary>
/// ProtobufUtils wire-format encoding / decoding helpers used by generated code.
/// </summary>
public static class ProtobufUtils
{
    public const int WireTypeVarint   = 0;
    public const int WireType64Bit    = 1;
    public const int WireTypeLenDelim = 2;
    public const int WireType32Bit    = 5;

    // ── Writing ──────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong EncodeTag(int fieldNumber, int wireType)
        => ((uint)fieldNumber << 3) | (uint)wireType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteTag(ref RentedBuffer buf, int fieldNumber, int wireType)
        => WriteVarint(ref buf, EncodeTag(fieldNumber, wireType));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteVarint(ref RentedBuffer buf, ulong value)
    {
        buf.Extend(10);
        var data = buf.Data!;
        int pos = buf.Length;

        // Compute required byte count via leading-zero count (BSR intrinsic on x64).
        // (value | 1) avoids the undefined LeadingZeroCount(0) edge case.
        int bits = 64 - BitOperations.LeadingZeroCount(value | 1);
        int n    = (bits + 6) / 7;   // ceiling(bits / 7), range [1, 10]

        // Each case writes exactly n bytes with no branches or loops.
        // The JIT emits a jump table for this dense integer switch.
        switch (n)
        {
            case 1:
                data[pos] = (byte)value;
                break;
            case 2:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)(value >>  7);
                break;
            case 3:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)(value >> 14);
                break;
            case 4:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)(value >> 21);
                break;
            case 5:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)(value >> 28);
                break;
            case 6:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)((value >> 28) | 0x80);
                data[pos + 5] = (byte)(value >> 35);
                break;
            case 7:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)((value >> 28) | 0x80);
                data[pos + 5] = (byte)((value >> 35) | 0x80);
                data[pos + 6] = (byte)(value >> 42);
                break;
            case 8:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)((value >> 28) | 0x80);
                data[pos + 5] = (byte)((value >> 35) | 0x80);
                data[pos + 6] = (byte)((value >> 42) | 0x80);
                data[pos + 7] = (byte)(value >> 49);
                break;
            case 9:
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)((value >> 28) | 0x80);
                data[pos + 5] = (byte)((value >> 35) | 0x80);
                data[pos + 6] = (byte)((value >> 42) | 0x80);
                data[pos + 7] = (byte)((value >> 49) | 0x80);
                data[pos + 8] = (byte)(value >> 56);
                break;
            default: // case 10: value >= 2^63, only reachable for ulong MSB set
                data[pos    ] = (byte)(value        | 0x80);
                data[pos + 1] = (byte)((value >>  7) | 0x80);
                data[pos + 2] = (byte)((value >> 14) | 0x80);
                data[pos + 3] = (byte)((value >> 21) | 0x80);
                data[pos + 4] = (byte)((value >> 28) | 0x80);
                data[pos + 5] = (byte)((value >> 35) | 0x80);
                data[pos + 6] = (byte)((value >> 42) | 0x80);
                data[pos + 7] = (byte)((value >> 49) | 0x80);
                data[pos + 8] = (byte)((value >> 56) | 0x80);
                data[pos + 9] = 1; // bit 63 is the only remaining bit; always 1 here
                break;
        }
        buf.Length = pos + n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixed32(ref RentedBuffer buf, uint value)
    {
        buf.Extend(4);
        buf.Data![buf.Length]     = (byte) value;
        buf.Data![buf.Length + 1] = (byte)(value >>  8);
        buf.Data![buf.Length + 2] = (byte)(value >> 16);
        buf.Data![buf.Length + 3] = (byte)(value >> 24);
        buf.Length += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixed64(ref RentedBuffer buf, ulong value)
    {
        buf.Extend(8);
        for (int i = 0; i < 8; i++)
            buf.Data![buf.Length + i] = (byte)(value >> (i * 8));
        buf.Length += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(ref RentedBuffer buf, ReadOnlySpan<byte> data)
    {
        WriteVarint(ref buf, (ulong)data.Length);
        buf.Extend(data.Length);
        data.CopyTo(buf.Data.AsSpan(buf.Length));
        buf.Length += data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(ref RentedBuffer buf, string s)
    {
        if (string.IsNullOrEmpty(s)) { WriteVarint(ref buf, 0); return; }
        int byteCount = Encoding.UTF8.GetByteCount(s);
        WriteVarint(ref buf, (ulong)byteCount);
        buf.Extend(byteCount);
        Encoding.UTF8.GetBytes(s, buf.Data.AsSpan(buf.Length));
        buf.Length += byteCount;
    }

    // ── ZigZag ───────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ZigZagEncode32(int value)  => (ulong)((value << 1) ^ (value >> 31));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ZigZagEncode64(long value) => (ulong)((value << 1) ^ (value >> 63));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ZigZagDecode32(ulong value)
    {
        uint v = (uint)value;
        return (int)((v >> 1) ^ -(v & 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ZigZagDecode64(ulong value)
        => (long)(value >> 1) ^ -(long)(value & 1);

    // ── Reading ──────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadVarint(ReadOnlySpan<byte> buf, int pos, out ulong value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;
        int shift = 0;
        while (pos + bytesRead < buf.Length)
        {
            byte b = buf[pos + bytesRead];
            bytesRead++;
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
            if (shift >= 64) return false;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadFixed32(ReadOnlySpan<byte> buf, int pos, out uint value)
    {
        if (pos + 4 > buf.Length) { value = 0; return false; }
        value = (uint)buf[pos]
              | ((uint)buf[pos + 1] <<  8)
              | ((uint)buf[pos + 2] << 16)
              | ((uint)buf[pos + 3] << 24);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadFixed64(ReadOnlySpan<byte> buf, int pos, out ulong value)
    {
        if (pos + 8 > buf.Length) { value = 0; return false; }
        value = (ulong)buf[pos]
              | ((ulong)buf[pos + 1] <<  8)
              | ((ulong)buf[pos + 2] << 16)
              | ((ulong)buf[pos + 3] << 24)
              | ((ulong)buf[pos + 4] << 32)
              | ((ulong)buf[pos + 5] << 40)
              | ((ulong)buf[pos + 6] << 48)
              | ((ulong)buf[pos + 7] << 56);
        return true;
    }

    /// <summary>Skip one field at <paramref name="pos"/> with the given wire type.</summary>
    public static bool SkipField(ReadOnlySpan<byte> buf, int pos, int wireType, out int bytesSkipped)
    {
        bytesSkipped = 0;
        switch (wireType)
        {
            case WireTypeVarint:
                while (pos + bytesSkipped < buf.Length)
                {
                    byte b = buf[pos + bytesSkipped];
                    bytesSkipped++;
                    if ((b & 0x80) == 0) return true;
                }
                return false;
            case WireType64Bit:
                if (pos + 8 > buf.Length) return false;
                bytesSkipped = 8; return true;
            case WireTypeLenDelim:
                if (!TryReadVarint(buf, pos, out ulong len, out int vbr)) return false;
                bytesSkipped = vbr + (int)len;
                return pos + bytesSkipped <= buf.Length;
            case WireType32Bit:
                if (pos + 4 > buf.Length) return false;
                bytesSkipped = 4; return true;
            default:
                return false;
        }
    }

    public static int VarintSize(ulong value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }
        return size;
    }

    public static int TagSize(int fieldNum, int wireType)
    {
        return VarintSize(EncodeTag(fieldNum, wireType));
    }

    public static int LenDelimSize(int payloadLength)
    {
        return VarintSize((ulong)payloadLength) + payloadLength;
    }

    public static int StringByteCount(string? value)
    {
        return value != null ? Encoding.UTF8.GetByteCount(value) : 0;
    }
}
