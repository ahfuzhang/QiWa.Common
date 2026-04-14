using Xunit;

namespace QiWa.Common.Tests;

public class ProtobufUtilsTests
{
    // ─── Constants ────────────────────────────────────────────────────────────

    [Fact]
    public void Constants_HaveCorrectProtobufWireTypeValues()
    {
        Assert.Equal(0, ProtobufUtils.WireTypeVarint);
        Assert.Equal(1, ProtobufUtils.WireType64Bit);
        Assert.Equal(2, ProtobufUtils.WireTypeLenDelim);
        Assert.Equal(5, ProtobufUtils.WireType32Bit);
    }

    // ─── WriteTag ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0,  8UL)]   // (1<<3)|0 = 8
    [InlineData(1, 2, 10UL)]   // (1<<3)|2 = 10
    [InlineData(2, 1, 17UL)]   // (2<<3)|1 = 17
    [InlineData(1, 5, 13UL)]   // (1<<3)|5 = 13
    public void WriteTag_EncodesFieldNumberAndWireType(int fieldNumber, int wireType, ulong expected)
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteTag(ref buf, fieldNumber, wireType);

            // Read back the varint and verify
            bool ok = ProtobufUtils.TryReadVarint(buf.AsSpan(), 0, out ulong actual, out _);
            Assert.True(ok);
            Assert.Equal(expected, actual);
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteVarint — byte count per case ───────────────────────────────────

    [Theory]
    [InlineData(0UL,                       1)]   // n=1 boundary: 0
    [InlineData(1UL,                       1)]   // n=1
    [InlineData(127UL,                     1)]   // n=1 upper bound
    [InlineData(128UL,                     2)]   // n=2 lower bound
    [InlineData(16383UL,                   2)]   // n=2 upper bound
    [InlineData(16384UL,                   3)]   // n=3 lower bound
    [InlineData(2097151UL,                 3)]   // n=3 upper bound
    [InlineData(2097152UL,                 4)]   // n=4 lower bound
    [InlineData(268435455UL,               4)]   // n=4 upper bound
    [InlineData(268435456UL,               5)]   // n=5 lower bound
    [InlineData(34359738367UL,             5)]   // n=5 upper bound
    [InlineData(34359738368UL,             6)]   // n=6 lower bound
    [InlineData(4398046511103UL,           6)]   // n=6 upper bound
    [InlineData(4398046511104UL,           7)]   // n=7 lower bound
    [InlineData(562949953421311UL,         7)]   // n=7 upper bound
    [InlineData(562949953421312UL,         8)]   // n=8 lower bound
    [InlineData(72057594037927935UL,       8)]   // n=8 upper bound
    [InlineData(72057594037927936UL,       9)]   // n=9 lower bound
    [InlineData(9223372036854775807UL,     9)]   // n=9 upper bound  (long.MaxValue)
    [InlineData(9223372036854775808UL,    10)]   // n=10 lower bound (1UL<<63)
    [InlineData(18446744073709551615UL,   10)]   // n=10 upper bound (ulong.MaxValue)
    public void WriteVarint_ProducesCorrectByteCount(ulong value, int expectedBytes)
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, value);
            Assert.Equal(expectedBytes, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteVarint — round-trip via TryReadVarint ───────────────────────────

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(127UL)]
    [InlineData(128UL)]
    [InlineData(16383UL)]
    [InlineData(16384UL)]
    [InlineData(2097151UL)]
    [InlineData(268435455UL)]
    [InlineData(268435456UL)]
    [InlineData(34359738367UL)]
    [InlineData(34359738368UL)]
    [InlineData(4398046511103UL)]
    [InlineData(562949953421311UL)]
    [InlineData(562949953421312UL)]
    [InlineData(72057594037927935UL)]
    [InlineData(72057594037927936UL)]
    [InlineData(9223372036854775807UL)]
    [InlineData(9223372036854775808UL)]
    [InlineData(18446744073709551615UL)]
    public void WriteVarint_RoundTrips_WithTryReadVarint(ulong value)
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, value);
            bool ok = ProtobufUtils.TryReadVarint(buf.AsSpan(), 0, out ulong decoded, out int bytesRead);
            Assert.True(ok);
            Assert.Equal(value, decoded);
            Assert.Equal(buf.Length, bytesRead);
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteVarint — specific byte patterns ────────────────────────────────

    [Fact]
    public void WriteVarint_Value0_ProducesSingleZeroByte()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 0UL);
            Assert.Equal(new byte[] { 0x00 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_Value1_ProducesSingleOneByte()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 1UL);
            Assert.Equal(new byte[] { 0x01 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_Value127_ProducesSingleByte0x7F()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 127UL);
            Assert.Equal(new byte[] { 0x7F }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_Value128_ProducesTwoBytesLittleEndian()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 128UL);
            // 128 = 0b10000000; varint: [0x80, 0x01]
            Assert.Equal(new byte[] { 0x80, 0x01 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_Value16383_ProducesTwoBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 16383UL);
            // 16383 = 0x3FFF; varint: [0xFF, 0x7F]
            Assert.Equal(new byte[] { 0xFF, 0x7F }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_Value16384_ProducesThreeBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, 16384UL);
            // 16384 = 0x4000; varint: [0x80, 0x80, 0x01]
            Assert.Equal(new byte[] { 0x80, 0x80, 0x01 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteVarint_ULongMaxValue_ProducesTenBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteVarint(ref buf, ulong.MaxValue);
            // ulong.MaxValue encodes to 10 bytes: nine 0xFF then 0x01
            var expected = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
            Assert.Equal(expected, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteFixed32 ────────────────────────────────────────────────────────

    [Fact]
    public void WriteFixed32_Zero_WritesFourZeroBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed32(ref buf, 0u);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed32_One_WritesLittleEndianEncoding()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed32(ref buf, 1u);
            Assert.Equal(new byte[] { 1, 0, 0, 0 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed32_MaxValue_WritesFourFFBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed32(ref buf, uint.MaxValue);
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed32_ArbitraryValue_WritesLittleEndian()
    {
        var buf = new RentedBuffer(16);
        try
        {
            // 0x01020304 → LE: [0x04, 0x03, 0x02, 0x01]
            ProtobufUtils.WriteFixed32(ref buf, 0x01020304u);
            Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed32_AdvancesLengthByFour()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed32(ref buf, 42u);
            Assert.Equal(4, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed32_RoundTrip_WithTryReadFixed32()
    {
        var buf = new RentedBuffer(16);
        try
        {
            uint original = 0xDEADBEEF;
            ProtobufUtils.WriteFixed32(ref buf, original);
            bool ok = ProtobufUtils.TryReadFixed32(buf.AsSpan(), 0, out uint decoded);
            Assert.True(ok);
            Assert.Equal(original, decoded);
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteFixed64 ────────────────────────────────────────────────────────

    [Fact]
    public void WriteFixed64_Zero_WritesEightZeroBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed64(ref buf, 0UL);
            Assert.Equal(new byte[8], buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed64_One_WritesLittleEndianEncoding()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed64(ref buf, 1UL);
            var expected = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Equal(expected, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed64_MaxValue_WritesEightFFBytes()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed64(ref buf, ulong.MaxValue);
            var expected = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            Assert.Equal(expected, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed64_ArbitraryValue_WritesLittleEndian()
    {
        var buf = new RentedBuffer(16);
        try
        {
            // 0x0102030405060708 → LE: [0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01]
            ProtobufUtils.WriteFixed64(ref buf, 0x0102030405060708UL);
            var expected = new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 };
            Assert.Equal(expected, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed64_AdvancesLengthByEight()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteFixed64(ref buf, 99UL);
            Assert.Equal(8, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteFixed64_RoundTrip_WithTryReadFixed64()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ulong original = 0xCAFEBABEDEADBEEFUL;
            ProtobufUtils.WriteFixed64(ref buf, original);
            bool ok = ProtobufUtils.TryReadFixed64(buf.AsSpan(), 0, out ulong decoded);
            Assert.True(ok);
            Assert.Equal(original, decoded);
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteBytes ───────────────────────────────────────────────────────────

    [Fact]
    public void WriteBytes_Empty_WritesOnlyLengthPrefixZero()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteBytes(ref buf, ReadOnlySpan<byte>.Empty);
            // Length prefix varint(0) = [0x00]; no data bytes
            Assert.Equal(new byte[] { 0x00 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteBytes_SingleByte_WritesLengthPrefixThenData()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteBytes(ref buf, new byte[] { 0xAB });
            // varint(1)=0x01, then 0xAB
            Assert.Equal(new byte[] { 0x01, 0xAB }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteBytes_MultipleBytes_WritesLengthPrefixThenData()
    {
        var buf = new RentedBuffer(16);
        try
        {
            byte[] data = { 0x01, 0x02, 0x03, 0x04 };
            ProtobufUtils.WriteBytes(ref buf, data);
            // varint(4)=0x04, then four data bytes
            Assert.Equal(new byte[] { 0x04, 0x01, 0x02, 0x03, 0x04 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteBytes_128Bytes_UsesMultiBytePrefix()
    {
        var buf = new RentedBuffer(256);
        try
        {
            byte[] data = new byte[128];
            for (int i = 0; i < 128; i++) data[i] = (byte)i;

            ProtobufUtils.WriteBytes(ref buf, data);

            // varint(128) = [0x80, 0x01] (2 bytes)
            Assert.Equal(130, buf.Length);
            Assert.Equal(0x80, buf.Data![0]);
            Assert.Equal(0x01, buf.Data![1]);
            Assert.Equal(data, buf.AsSpan()[2..].ToArray());
        }
        finally { buf.Dispose(); }
    }

    // ─── WriteString ──────────────────────────────────────────────────────────

    [Fact]
    public void WriteString_Null_WritesVarintZeroOnly()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteString(ref buf, null!);
            Assert.Equal(new byte[] { 0x00 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteString_Empty_WritesVarintZeroOnly()
    {
        var buf = new RentedBuffer(16);
        try
        {
            ProtobufUtils.WriteString(ref buf, string.Empty);
            Assert.Equal(new byte[] { 0x00 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteString_Ascii_WritesLengthPrefixedUtf8()
    {
        var buf = new RentedBuffer(32);
        try
        {
            ProtobufUtils.WriteString(ref buf, "hi");
            // varint(2)=0x02, 'h'=0x68, 'i'=0x69
            Assert.Equal(new byte[] { 0x02, 0x68, 0x69 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteString_Unicode_WritesUtf8Encoded()
    {
        var buf = new RentedBuffer(32);
        try
        {
            // '中' is U+4E2D, UTF-8: E4 B8 AD (3 bytes)
            ProtobufUtils.WriteString(ref buf, "中");
            var bytes = buf.AsSpan().ToArray();
            Assert.Equal(0x03, bytes[0]); // length prefix = 3
            Assert.Equal(3, bytes.Length - 1); // 3 UTF-8 bytes
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void WriteString_RoundTrips_AsLengthPrefixedData()
    {
        const string original = "Hello, Protobuf!";
        var buf = new RentedBuffer(32);
        try
        {
            ProtobufUtils.WriteString(ref buf, original);
            var span = buf.AsSpan();

            // Read the length prefix varint
            bool ok = ProtobufUtils.TryReadVarint(span, 0, out ulong length, out int prefixLen);
            Assert.True(ok);

            // Decode the UTF-8 content
            string decoded = System.Text.Encoding.UTF8.GetString(span.Slice(prefixLen, (int)length));
            Assert.Equal(original, decoded);
        }
        finally { buf.Dispose(); }
    }

    // ─── ZigZagEncode32 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,           0UL)]
    [InlineData(1,           2UL)]
    [InlineData(-1,          1UL)]
    [InlineData(2,           4UL)]
    [InlineData(-2,          3UL)]
    [InlineData(int.MaxValue,  18446744073709551614UL)]  // sign-extended: (ulong)(int)(-2)
    [InlineData(int.MinValue,  18446744073709551615UL)]  // sign-extended: (ulong)(int)(-1)
    public void ZigZagEncode32_ProducesCorrectValue(int input, ulong expected)
    {
        Assert.Equal(expected, ProtobufUtils.ZigZagEncode32(input));
    }

    // ─── ZigZagEncode64 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0L,            0UL)]
    [InlineData(1L,            2UL)]
    [InlineData(-1L,           1UL)]
    [InlineData(2L,            4UL)]
    [InlineData(-2L,           3UL)]
    [InlineData(long.MaxValue, 18446744073709551614UL)]  // ulong.MaxValue - 1
    [InlineData(long.MinValue, 18446744073709551615UL)]  // ulong.MaxValue
    public void ZigZagEncode64_ProducesCorrectValue(long input, ulong expected)
    {
        Assert.Equal(expected, ProtobufUtils.ZigZagEncode64(input));
    }

    // ─── ZigZagDecode32 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0UL,           0)]
    [InlineData(1UL,          -1)]
    [InlineData(2UL,           1)]
    [InlineData(3UL,          -2)]
    [InlineData(4294967294UL,  int.MaxValue)]
    [InlineData(4294967295UL,  int.MinValue)]
    public void ZigZagDecode32_ProducesCorrectValue(ulong input, int expected)
    {
        Assert.Equal(expected, ProtobufUtils.ZigZagDecode32(input));
    }

    // ─── ZigZagDecode64 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0UL,                         0L)]
    [InlineData(1UL,                        -1L)]
    [InlineData(2UL,                         1L)]
    [InlineData(3UL,                        -2L)]
    [InlineData(18446744073709551614UL,  long.MaxValue)]
    [InlineData(18446744073709551615UL,  long.MinValue)]
    public void ZigZagDecode64_ProducesCorrectValue(ulong input, long expected)
    {
        Assert.Equal(expected, ProtobufUtils.ZigZagDecode64(input));
    }

    // ─── ZigZag round-trip ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ZigZag32_RoundTrip(int value)
    {
        ulong encoded = ProtobufUtils.ZigZagEncode32(value);
        int   decoded = ProtobufUtils.ZigZagDecode32(encoded);
        Assert.Equal(value, decoded);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void ZigZag64_RoundTrip(long value)
    {
        ulong encoded = ProtobufUtils.ZigZagEncode64(value);
        long  decoded = ProtobufUtils.ZigZagDecode64(encoded);
        Assert.Equal(value, decoded);
    }

    // ─── TryReadVarint ───────────────────────────────────────────────────────

    [Fact]
    public void TryReadVarint_EmptyBuffer_ReturnsFalse()
    {
        bool ok = ProtobufUtils.TryReadVarint(ReadOnlySpan<byte>.Empty, 0, out _, out int bytesRead);
        Assert.False(ok);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarint_SingleByteNoContination_ReturnsTrue()
    {
        byte[] buf = { 0x2A }; // 42
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out ulong value, out int bytesRead);
        Assert.True(ok);
        Assert.Equal(42UL, value);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void TryReadVarint_TwoByteValue_ReturnsTrue()
    {
        byte[] buf = { 0x80, 0x01 }; // 128
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out ulong value, out int bytesRead);
        Assert.True(ok);
        Assert.Equal(128UL, value);
        Assert.Equal(2, bytesRead);
    }

    [Fact]
    public void TryReadVarint_TruncatedBuffer_ReturnsFalse()
    {
        // Continuation bit set but no following byte
        byte[] buf = { 0x80 };
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryReadVarint_NonZeroStartPosition_ReadsFromCorrectOffset()
    {
        // First byte is padding (0x00), real varint starts at pos=1
        byte[] buf = { 0x00, 0x7F };
        bool ok = ProtobufUtils.TryReadVarint(buf, 1, out ulong value, out int bytesRead);
        Assert.True(ok);
        Assert.Equal(127UL, value);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void TryReadVarint_PosAtEndOfBuffer_ReturnsFalse()
    {
        byte[] buf = { 0x01 };
        bool ok = ProtobufUtils.TryReadVarint(buf, 1, out _, out _); // pos == buf.Length
        Assert.False(ok);
    }

    [Fact]
    public void TryReadVarint_TenContinuationBytes_OverflowReturnsFalse()
    {
        // Ten bytes all with continuation bit set → shift reaches 70, overflow
        byte[] buf = { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 };
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryReadVarint_TenByteMaxValue_ReturnsTrue()
    {
        // ulong.MaxValue encodes as: nine 0xFF then 0x01
        byte[] buf = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out ulong value, out int bytesRead);
        Assert.True(ok);
        Assert.Equal(ulong.MaxValue, value);
        Assert.Equal(10, bytesRead);
    }

    [Fact]
    public void TryReadVarint_DataAfterVarint_IsIgnored()
    {
        // Varint for 1, followed by extra bytes
        byte[] buf = { 0x01, 0xFF, 0xFF };
        bool ok = ProtobufUtils.TryReadVarint(buf, 0, out ulong value, out int bytesRead);
        Assert.True(ok);
        Assert.Equal(1UL, value);
        Assert.Equal(1, bytesRead); // only reads 1 byte
    }

    // ─── TryReadFixed32 ──────────────────────────────────────────────────────

    [Fact]
    public void TryReadFixed32_TooShort_ReturnsFalse()
    {
        byte[] buf = { 0x01, 0x02, 0x03 }; // only 3 bytes, need 4
        bool ok = ProtobufUtils.TryReadFixed32(buf, 0, out uint value);
        Assert.False(ok);
        Assert.Equal(0u, value);
    }

    [Fact]
    public void TryReadFixed32_ExactlyFourBytes_ReturnsTrue()
    {
        byte[] buf = { 0x01, 0x00, 0x00, 0x00 }; // 1 in LE
        bool ok = ProtobufUtils.TryReadFixed32(buf, 0, out uint value);
        Assert.True(ok);
        Assert.Equal(1u, value);
    }

    [Fact]
    public void TryReadFixed32_LittleEndianByteOrder()
    {
        // 0x04030201 in LE = bytes [0x01, 0x02, 0x03, 0x04]
        byte[] buf = { 0x01, 0x02, 0x03, 0x04 };
        bool ok = ProtobufUtils.TryReadFixed32(buf, 0, out uint value);
        Assert.True(ok);
        Assert.Equal(0x04030201u, value);
    }

    [Fact]
    public void TryReadFixed32_WithOffset_ReadsFromCorrectPosition()
    {
        byte[] buf = { 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }; // 4-byte value starts at pos=2
        bool ok = ProtobufUtils.TryReadFixed32(buf, 2, out uint value);
        Assert.True(ok);
        Assert.Equal(uint.MaxValue, value);
    }

    [Fact]
    public void TryReadFixed32_PosAtBoundary_ReturnsFalse()
    {
        byte[] buf = { 0x01, 0x02, 0x03, 0x04 };
        bool ok = ProtobufUtils.TryReadFixed32(buf, 1, out _); // pos+4=5 > 4
        Assert.False(ok);
    }

    // ─── TryReadFixed64 ──────────────────────────────────────────────────────

    [Fact]
    public void TryReadFixed64_TooShort_ReturnsFalse()
    {
        byte[] buf = new byte[7]; // only 7 bytes, need 8
        bool ok = ProtobufUtils.TryReadFixed64(buf, 0, out ulong value);
        Assert.False(ok);
        Assert.Equal(0UL, value);
    }

    [Fact]
    public void TryReadFixed64_ExactlyEightBytes_ReturnsTrue()
    {
        byte[] buf = { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; // 1 in LE
        bool ok = ProtobufUtils.TryReadFixed64(buf, 0, out ulong value);
        Assert.True(ok);
        Assert.Equal(1UL, value);
    }

    [Fact]
    public void TryReadFixed64_LittleEndianByteOrder()
    {
        // 0x0807060504030201 in LE = bytes [0x01..0x08]
        byte[] buf = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        bool ok = ProtobufUtils.TryReadFixed64(buf, 0, out ulong value);
        Assert.True(ok);
        Assert.Equal(0x0807060504030201UL, value);
    }

    [Fact]
    public void TryReadFixed64_MaxValue_ReturnsAllOnes()
    {
        byte[] buf = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        bool ok = ProtobufUtils.TryReadFixed64(buf, 0, out ulong value);
        Assert.True(ok);
        Assert.Equal(ulong.MaxValue, value);
    }

    [Fact]
    public void TryReadFixed64_WithOffset_ReadsFromCorrectPosition()
    {
        byte[] buf = { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        bool ok = ProtobufUtils.TryReadFixed64(buf, 1, out ulong value);
        Assert.True(ok);
        Assert.Equal(1UL, value);
    }

    [Fact]
    public void TryReadFixed64_PosAtBoundary_ReturnsFalse()
    {
        byte[] buf = new byte[8];
        bool ok = ProtobufUtils.TryReadFixed64(buf, 1, out _); // pos+8=9 > 8
        Assert.False(ok);
    }

    // ─── SkipField ───────────────────────────────────────────────────────────

    // WireTypeVarint (0)

    [Fact]
    public void SkipField_Varint_SingleByte_ReturnsTrue()
    {
        byte[] buf = { 0x01 }; // no continuation bit
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeVarint, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(1, bytesSkipped);
    }

    [Fact]
    public void SkipField_Varint_MultiByte_ReturnsTrue()
    {
        byte[] buf = { 0x80, 0x01 }; // two-byte varint (value=128)
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeVarint, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(2, bytesSkipped);
    }

    [Fact]
    public void SkipField_Varint_Truncated_ReturnsFalse()
    {
        byte[] buf = { 0x80 }; // continuation bit set, no following byte
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeVarint, out _);
        Assert.False(ok);
    }

    [Fact]
    public void SkipField_Varint_EmptyBuffer_ReturnsFalse()
    {
        bool ok = ProtobufUtils.SkipField(ReadOnlySpan<byte>.Empty, 0, ProtobufUtils.WireTypeVarint, out _);
        Assert.False(ok);
    }

    // WireType64Bit (1)

    [Fact]
    public void SkipField_64Bit_ExactlyEightBytes_ReturnsTrue()
    {
        byte[] buf = new byte[8];
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireType64Bit, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(8, bytesSkipped);
    }

    [Fact]
    public void SkipField_64Bit_WithOffset_ReturnsTrue()
    {
        byte[] buf = new byte[10]; // pos=2, need 8 bytes: 2+8=10 <= 10
        bool ok = ProtobufUtils.SkipField(buf, 2, ProtobufUtils.WireType64Bit, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(8, bytesSkipped);
    }

    [Fact]
    public void SkipField_64Bit_TooShort_ReturnsFalse()
    {
        byte[] buf = new byte[7]; // need 8
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireType64Bit, out _);
        Assert.False(ok);
    }

    // WireTypeLenDelim (2)

    [Fact]
    public void SkipField_LenDelim_ZeroLength_ReturnsTrue()
    {
        byte[] buf = { 0x00 }; // varint(0) = length 0
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeLenDelim, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(1, bytesSkipped); // 1 byte varint + 0 data
    }

    [Fact]
    public void SkipField_LenDelim_WithData_ReturnsTrue()
    {
        // varint(3)=0x03, then 3 data bytes
        byte[] buf = { 0x03, 0xAA, 0xBB, 0xCC };
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeLenDelim, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(4, bytesSkipped); // 1 varint + 3 data
    }

    [Fact]
    public void SkipField_LenDelim_TruncatedVarint_ReturnsFalse()
    {
        byte[] buf = { 0x80 }; // continuation bit set, no following byte for length varint
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeLenDelim, out _);
        Assert.False(ok);
    }

    [Fact]
    public void SkipField_LenDelim_DataTooShort_ReturnsFalse()
    {
        // varint(5) says 5 data bytes, but only 2 follow
        byte[] buf = { 0x05, 0x01, 0x02 };
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireTypeLenDelim, out _);
        Assert.False(ok);
    }

    [Fact]
    public void SkipField_LenDelim_WithOffset_ReturnsTrue()
    {
        // pos=1; buf[1]=varint(2), buf[2..3]=data
        byte[] buf = { 0xFF, 0x02, 0xAA, 0xBB };
        bool ok = ProtobufUtils.SkipField(buf, 1, ProtobufUtils.WireTypeLenDelim, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(3, bytesSkipped); // 1 varint byte + 2 data
    }

    // WireType32Bit (5)

    [Fact]
    public void SkipField_32Bit_ExactlyFourBytes_ReturnsTrue()
    {
        byte[] buf = new byte[4];
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireType32Bit, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(4, bytesSkipped);
    }

    [Fact]
    public void SkipField_32Bit_WithOffset_ReturnsTrue()
    {
        byte[] buf = new byte[6]; // pos=2, need 4 bytes: 2+4=6 <= 6
        bool ok = ProtobufUtils.SkipField(buf, 2, ProtobufUtils.WireType32Bit, out int bytesSkipped);
        Assert.True(ok);
        Assert.Equal(4, bytesSkipped);
    }

    [Fact]
    public void SkipField_32Bit_TooShort_ReturnsFalse()
    {
        byte[] buf = new byte[3]; // need 4
        bool ok = ProtobufUtils.SkipField(buf, 0, ProtobufUtils.WireType32Bit, out _);
        Assert.False(ok);
    }

    // Unknown wire type

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(99)]
    public void SkipField_UnknownWireType_ReturnsFalse(int wireType)
    {
        byte[] buf = new byte[16];
        bool ok = ProtobufUtils.SkipField(buf, 0, wireType, out int bytesSkipped);
        Assert.False(ok);
        Assert.Equal(0, bytesSkipped);
    }
}
