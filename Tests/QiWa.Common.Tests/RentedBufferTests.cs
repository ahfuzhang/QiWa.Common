using System.Text;
using System.Text.Json;
using QiWa.Common;
using Xunit;

namespace QiWa.Common.Tests;

public class RentedBufferTests
{
    /// <summary>Decodes the current content of the buffer as a UTF-8 string.</summary>
    private static string AsString(RentedBuffer buf) =>
        Encoding.UTF8.GetString(buf.AsSpan());

    // ─── Constructor / Rent ───────────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesBufferWithZeroLength()
    {
        var buf = new RentedBuffer(64);
        try
        {
            Assert.NotNull(buf.Data);
            Assert.Equal(0, buf.Length);
            Assert.True(buf.Data.Length >= 64);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void Rent_SetsDataAndResetsLength()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append("hello");
            Assert.Equal(5, buf.Length);

            buf.Rent(32);
            Assert.NotNull(buf.Data);
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_SetsDataToNullAndLengthToZero()
    {
        var buf = new RentedBuffer(64);
        buf.Append("test");
        buf.Dispose();

        Assert.Null(buf.Data);
        Assert.Equal(0, buf.Length);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var buf = new RentedBuffer(64);
        buf.Dispose();
        buf.Dispose(); // second call should be a no-op
    }

    // ─── AsSpan ───────────────────────────────────────────────────────────────

    [Fact]
    public void AsSpan_ReturnsEmpty_WhenDataIsNull()
    {
        var buf = new RentedBuffer(64);
        buf.Dispose(); // sets Data = null

        Assert.True(buf.AsSpan().IsEmpty);
    }

    [Fact]
    public void AsSpan_ReturnsEmpty_WhenLengthIsZero()
    {
        var buf = new RentedBuffer(64);
        try
        {
            Assert.Equal(0, buf.Length);
            Assert.True(buf.AsSpan().IsEmpty);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSliceOfData()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append((byte)'A');
            buf.Append((byte)'B');
            buf.Append((byte)'C');

            var span = buf.AsSpan();
            Assert.Equal(3, span.Length);
            Assert.Equal((byte)'A', span[0]);
            Assert.Equal((byte)'B', span[1]);
            Assert.Equal((byte)'C', span[2]);
        }
        finally { buf.Dispose(); }
    }

    // ─── Extend ───────────────────────────────────────────────────────────────

    [Fact]
    public void Extend_DoesNotGrow_WhenEnoughSpaceAvailable()
    {
        var buf = new RentedBuffer(100);
        try
        {
            int cap = buf.Data!.Length;
            buf.Extend(cap); // Length(0) + needed(cap) == cap → no growth
            Assert.Equal(cap, buf.Data!.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void Extend_Grows_WhenCapacityExceeded()
    {
        var buf = new RentedBuffer(16);
        try
        {
            int cap = buf.Data!.Length;
            buf.Length = cap; // treat buffer as full
            buf.Extend(1);
            Assert.True(buf.Data!.Length > cap);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void Extend_UsesDoubleCapacity_WhenSmallAmountNeeded()
    {
        // When needed is small, the new size should be at least Data.Length * 2.
        var buf = new RentedBuffer(16);
        try
        {
            int cap = buf.Data!.Length;
            buf.Length = cap;
            buf.Extend(1); // Math.Max(cap * 2, cap + 1) == cap * 2
            Assert.True(buf.Data!.Length >= cap * 2);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void Extend_UsesNeededSize_WhenLargerThanDoubleCapacity()
    {
        // When needed >> Data.Length * 2, the new size should accommodate Length + needed.
        var buf = new RentedBuffer(16);
        try
        {
            int cap = buf.Data!.Length;
            buf.Length = cap;
            int needed = cap * 10; // much larger than cap * 2
            buf.Extend(needed);
            Assert.True(buf.Data!.Length >= cap + needed);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void Extend_PreservesExistingDataAfterGrowth()
    {
        var buf = new RentedBuffer(16);
        try
        {
            buf.Append("Hello");
            // Force a large extension to trigger reallocation
            string large = new string('X', 200);
            buf.Append(large);

            string result = AsString(buf);
            Assert.StartsWith("Hello", result);
            Assert.EndsWith(large, result);
        }
        finally { buf.Dispose(); }
    }

    // ─── AppendMulti ──────────────────────────────────────────────────────────

    [Fact]
    public void AppendMulti_SkipsNullAndEmptyStrings()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendMulti("", null!, "hello");
            Assert.Equal("hello", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendMulti_AppendsAllNonEmptyStrings()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendMulti("foo", "bar", "baz");
            Assert.Equal("foobarbaz", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendMulti_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.AppendMulti("hello", "world");
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(string) ───────────────────────────────────────────────────────

    [Fact]
    public void AppendString_WithNull_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append((string)null!);
            Assert.False(err.Err());
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendString_WithEmpty_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append("");
            Assert.False(err.Err());
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendString_WithAscii_AppendsCorrectBytes()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append("Hello, World!");
            Assert.Equal("Hello, World!", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendString_WithUnicode_AppendsUtf8Encoded()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append("你好");
            Assert.Equal("你好", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendString_Multiple_ConcatenatesInOrder()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append("one");
            buf.Append("two");
            buf.Append("three");
            Assert.Equal("onetwothree", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(byte) ─────────────────────────────────────────────────────────

    [Fact]
    public void AppendByte_AppendsSingleByte()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append((byte)0x41); // 'A'
            Assert.Equal(1, buf.Length);
            Assert.Equal(0x41, buf.Data![0]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendByte_Multiple_AppendsInOrder()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append((byte)'x');
            buf.Append((byte)'y');
            buf.Append((byte)'z');
            Assert.Equal("xyz", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendByte_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append((byte)0xFF);
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(ReadOnlySpan<byte>) ───────────────────────────────────────────

    [Fact]
    public void AppendSpan_WithEmpty_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append(ReadOnlySpan<byte>.Empty);
            Assert.False(err.Err());
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendSpan_AppendsAllBytes()
    {
        var buf = new RentedBuffer(64);
        try
        {
            ReadOnlySpan<byte> data = new byte[] { 1, 2, 3, 4 };
            buf.Append(data);
            Assert.Equal(4, buf.Length);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf.AsSpan().ToArray());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(long) ─────────────────────────────────────────────────────────

    [Fact]
    public void AppendLong_Zero_WritesZero()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(0L);
            Assert.Equal("0", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendLong_PositiveValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(123456789L);
            Assert.Equal("123456789", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendLong_NegativeValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(-42L);
            Assert.Equal("-42", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendLong_MaxValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(long.MaxValue);
            Assert.Equal(long.MaxValue.ToString(), AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendLong_MinValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(long.MinValue);
            Assert.Equal(long.MinValue.ToString(), AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendLong_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append(42L);
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(UInt64) ───────────────────────────────────────────────────────

    [Fact]
    public void AppendUInt64_Zero()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(0UL);
            Assert.Equal("0", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUInt64_LargeValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(9876543210UL);
            Assert.Equal("9876543210", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUInt64_MaxValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(ulong.MaxValue);
            Assert.Equal(ulong.MaxValue.ToString(), AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUInt64_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append(1UL);
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(double) ───────────────────────────────────────────────────────

    [Fact]
    public void AppendDouble_PositiveExactValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(1.5); // exactly representable in binary float
            Assert.Equal("1.5", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendDouble_NegativeExactValue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(-1.5);
            Assert.Equal("-1.5", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendDouble_Zero()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(0.0);
            double parsed = double.Parse(AsString(buf));
            Assert.Equal(0.0, parsed);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendDouble_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append(3.14);
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── Append(bool) ─────────────────────────────────────────────────────────

    [Fact]
    public void AppendBool_True_WritesLiteralTrue()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(true);
            Assert.Equal("true", AsString(buf));
            Assert.Equal(4, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendBool_False_WritesLiteralFalse()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.Append(false);
            Assert.Equal("false", AsString(buf));
            Assert.Equal(5, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendBool_ReturnsDefaultError()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var err = buf.Append(true);
            Assert.False(err.Err());
        }
        finally { buf.Dispose(); }
    }

    // ─── AppendUtcDatetime ────────────────────────────────────────────────────

    [Fact]
    public void AppendUtcDatetime_UtcKind_FormatsCorrectly()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var dt = new DateTime(2024, 3, 15, 10, 30, 45, DateTimeKind.Utc);
            buf.AppendUtcDatetime(dt);
            Assert.Equal("2024-03-15T10:30:45.0000000Z", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_AlwaysProduces28Chars()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendUtcDatetime(DateTime.UtcNow);
            Assert.Equal(28, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_OutputEndsWithZ()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendUtcDatetime(DateTime.UtcNow);
            Assert.Equal((byte)'Z', buf.Data![27]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_WithSubsecondTicks()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var dt = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)
                     + TimeSpan.FromTicks(1234567);
            buf.AppendUtcDatetime(dt);
            Assert.Equal("2024-12-31T23:59:59.1234567Z", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_LocalKind_ConvertsToUtc()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var local = DateTime.Now;
            Assert.Equal(DateTimeKind.Local, local.Kind);
            buf.AppendUtcDatetime(local);

            var result = AsString(buf);
            Assert.Equal(28, result.Length);
            Assert.EndsWith("Z", result);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_UnspecifiedKind_TreatedAsUtc()
    {
        var buf = new RentedBuffer(64);
        try
        {
            // DateTimeKind.Unspecified – the method should treat as UTC (no conversion)
            var dt = new DateTime(2024, 6, 15, 8, 0, 0); // Kind == Unspecified
            Assert.Equal(DateTimeKind.Unspecified, dt.Kind);
            buf.AppendUtcDatetime(dt);
            Assert.Equal("2024-06-15T08:00:00.0000000Z", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendUtcDatetime_PaddingForSingleDigitMonthAndDay()
    {
        var buf = new RentedBuffer(64);
        try
        {
            var dt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            buf.AppendUtcDatetime(dt);
            // Month and day must be zero-padded
            var result = AsString(buf);
            Assert.Equal("2000-01-01T00:00:00.0000000Z", result);
        }
        finally { buf.Dispose(); }
    }

    // ─── Clone ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clone_CreatesACopyWithSameContent()
    {
        var buf = new RentedBuffer(64);
        RentedBuffer clone = default;
        try
        {
            buf.Append("original");
            clone = buf.Clone();

            Assert.Equal("original", AsString(clone));
            Assert.Equal(buf.Length, clone.Length);
        }
        finally
        {
            buf.Dispose();
            clone.Dispose();
        }
    }

    [Fact]
    public void Clone_IsIndependent_ModifyingCloneDoesNotAffectOriginal()
    {
        var buf = new RentedBuffer(64);
        RentedBuffer clone = default;
        try
        {
            buf.Append("original");
            clone = buf.Clone();

            clone.Append(" extra");

            Assert.Equal("original", AsString(buf));
            Assert.Equal("original extra", AsString(clone));
        }
        finally
        {
            buf.Dispose();
            clone.Dispose();
        }
    }

    [Fact]
    public void Clone_HasDifferentDataArrayReference()
    {
        var buf = new RentedBuffer(64);
        RentedBuffer clone = default;
        try
        {
            buf.Append("data");
            clone = buf.Clone();

            Assert.NotSame(buf.Data, clone.Data);
        }
        finally
        {
            buf.Dispose();
            clone.Dispose();
        }
    }

    // ─── AppendAsJsonEscapedString(string) ────────────────────────────────────

    [Fact]
    public void AppendAsJsonEscapedString_String_WithNull_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString((string)null!);
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_String_WithEmpty_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString("");
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_String_WithPlainText()
    {
        var buf = new RentedBuffer(256);
        try
        {
            buf.AppendAsJsonEscapedString("hello world");
            Assert.Equal("hello world", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_String_EscapesSpecialChars_RoundTripsViaJsonParse()
    {
        // Raw value contains: backslash, double-quote, tab, newline, and a Unicode control char (\u0001).
        const string rawValue = "back\\slash, \"quoted\", tab\there,\nnewline, and \u0001 control";

        var buf = new RentedBuffer(512);
        try
        {
            // Build {"key":"<escaped value>"} manually.
            buf.Append((byte)'{');
            buf.Append("\"key\":");
            buf.Append((byte)'"');
            buf.AppendAsJsonEscapedString(rawValue);
            buf.Append((byte)'"');
            buf.Append((byte)'}');

            string json = AsString(buf);

            // Verify the JSON is well-formed and the value round-trips correctly.
            using var doc = JsonDocument.Parse(json);
            string? parsed = doc.RootElement.GetProperty("key").GetString();
            Assert.Equal(rawValue, parsed);
        }
        finally { buf.Dispose(); }
    }

    // ─── AppendAsJsonEscapedString(ReadOnlySpan<char>) ────────────────────────

    [Fact]
    public void AppendAsJsonEscapedString_CharSpan_WithEmpty_DoesNothing()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString(ReadOnlySpan<char>.Empty);
            Assert.Equal(0, buf.Length);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_CharSpan_ShortString_UsesStackalloc()
    {
        // s.Length == 12 → maxUtf8Bytes = (12+1)*3 = 39 ≤ 1024 → stackalloc path
        var buf = new RentedBuffer(256);
        try
        {
            const string input = "short string";
            buf.AppendAsJsonEscapedString(input.AsSpan());
            Assert.Equal("short string", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_CharSpan_LongString_UsesArrayPool()
    {
        // s.Length == 400 → maxUtf8Bytes = (400+1)*3 = 1203 > 1024 → ArrayPool path
        var buf = new RentedBuffer(256);
        try
        {
            string input = new string('A', 400);
            buf.AppendAsJsonEscapedString(input.AsSpan());
            Assert.Equal(input, AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_CharSpan_EscapesDoubleQuotes()
    {
        var buf = new RentedBuffer(256);
        try
        {
            buf.AppendAsJsonEscapedString("say \"hello\"".AsSpan());
            string result = AsString(buf);
            Assert.Contains("\\\"", result);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_CharSpan_EscapesBackslash()
    {
        var buf = new RentedBuffer(256);
        try
        {
            buf.AppendAsJsonEscapedString("a\\b".AsSpan());
            string result = AsString(buf);
            Assert.Contains("\\\\", result);
        }
        finally { buf.Dispose(); }
    }

    // ─── AppendAsJsonEscapedString(ReadOnlySpan<byte>) ────────────────────────

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_PlainBytes_PassedThrough()
    {
        var buf = new RentedBuffer(64);
        try
        {
            byte[] input = Encoding.UTF8.GetBytes("hello");
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(input));
            Assert.Equal("hello", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_Tab_EscapedAsBackslashT()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(new byte[] { (byte)'\t' }));

            Assert.Equal(2, buf.Length);
            Assert.Equal((byte)'\\', buf.Data![0]);
            Assert.Equal((byte)'t', buf.Data![1]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_Newline_EscapedAsBackslashN()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(new byte[] { (byte)'\n' }));

            Assert.Equal(2, buf.Length);
            Assert.Equal((byte)'\\', buf.Data![0]);
            Assert.Equal((byte)'n', buf.Data![1]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_Backslash_EscapedAsDoubleBackslash()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(new byte[] { (byte)'\\' }));

            Assert.Equal(2, buf.Length);
            Assert.Equal((byte)'\\', buf.Data![0]);
            Assert.Equal((byte)'\\', buf.Data![1]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_Quote_EscapedAsBackslashQuote()
    {
        var buf = new RentedBuffer(64);
        try
        {
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(new byte[] { (byte)'"' }));

            Assert.Equal(2, buf.Length);
            Assert.Equal((byte)'\\', buf.Data![0]);
            Assert.Equal((byte)'"', buf.Data![1]);
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_MixedContent()
    {
        var buf = new RentedBuffer(64);
        try
        {
            // 'a', '\t', 'b', '\n', 'c'  →  "a\tb\nc" (literal backslash sequences)
            byte[] input = { (byte)'a', (byte)'\t', (byte)'b', (byte)'\n', (byte)'c' };
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(input));

            Assert.Equal(@"a\tb\nc", AsString(buf));
        }
        finally { buf.Dispose(); }
    }

    [Fact]
    public void AppendAsJsonEscapedString_ByteSpan_AllSpecialCharsInOneString()
    {
        var buf = new RentedBuffer(64);
        try
        {
            // tab + newline + backslash + quote
            byte[] input = { (byte)'\t', (byte)'\n', (byte)'\\', (byte)'"' };
            buf.AppendAsJsonEscapedString(new ReadOnlySpan<byte>(input));

            string result = AsString(buf);
            // \t→\t, \n→\n, \→\\, "→\"  (8 output chars)
            Assert.Equal(@"\t\n\\\""", result);
        }
        finally { buf.Dispose(); }
    }
}
