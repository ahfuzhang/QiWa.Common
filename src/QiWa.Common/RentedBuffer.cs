using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using static System.Text.Encoding;

namespace QiWa.Common;

/// <summary>
/// Provides a self-managed memory allocation component. High performance, bypasses GC,
/// but forgetting to release it will cause memory leaks. Equivalent to reimplementing Go's []byte.
/// </summary>
public struct RentedBuffer : IDisposable
{
    /// <summary>
    /// Memory borrowed from the shared array pool. May be null, indicating no memory has been borrowed.
    /// </summary>
    public byte[]? Data;

    /// <summary>
    /// Number of bytes used in Data. Data.Length is the total borrowed size; Length is the actual used size. Length ≤ Data.Length.
    /// </summary>
    public System.Int32 Length;

    /// <summary>
    /// Constructor that immediately borrows memory of the specified size. Note: if length is very large,
    /// the pool may be unable to satisfy the request, throwing an OutOfMemoryException.
    /// </summary>
    /// <param name="length"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RentedBuffer(System.Int32 length)
    {
        Rent(length);
    }

    private const string Utf8FormatterFailedMessage = "Utf8Formatter.TryFormat failed.";
    private const int CodeOfFormatFail = 254;

    /// <summary>
    /// Borrows memory from the array pool.
    /// </summary>
    /// <param name="length">Required memory size</param>
    /// <exception>out of memory</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Rent(System.Int32 length)
    {
        Data = ArrayPool<byte>.Shared.Rent(length);
        Length = 0;
    }

    /// <summary>
    /// Returns the borrowed memory to the array pool. Forgetting to call this when leaving the scope will cause a memory leak.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Data != null)
        {
            ArrayPool<byte>.Shared.Return(Data);
            Data = null;
        }
        Length = 0;
    }

    /// <summary>
    /// Accesses the used portion of the buffer as a <see cref="Span{T}"/>. Note: returns an empty Span if Data is null or Length is 0.
    /// </summary>
    /// <returns>A <see cref="Span{T}"/> representing the valid memory buffer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AsSpan()
    {
        if (Data == null || Length == 0)
        {
            return [];
        }
        return Data.AsSpan(0, Length);
    }

    /// <summary>
    /// Ensures the buffer has enough remaining capacity to append the specified number of bytes.
    /// If the current remaining space is insufficient, a larger block is rented and existing data is copied into it.
    /// Note: if needed is very large, the pool may be unable to satisfy the request, throwing an OutOfMemoryException.
    /// </summary>
    /// <param name="needed"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Extend(int needed)
    {
        if (Length + needed <= Data!.Length)
        {
            return;
        }
        var n = System.Math.Max(Data.Length * 2, Length + needed);
        byte[] newData = ArrayPool<byte>.Shared.Rent(n);
        Array.Copy(Data, newData, Length);
        ArrayPool<byte>.Shared.Return(Data);
        Data = newData;
    }

    /// <summary>
    /// Note: calling this method always allocates a new string[] on the heap. AppendMulti is kept separate from Append(string s).
    /// </summary>
    /// <param name="arr">A variable number of strings</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error AppendMulti(params string[] arr)
    {
        foreach (var s in arr)
        {
            if (string.IsNullOrEmpty(s))
            {
                continue;
            }
            int byteCount = UTF8.GetByteCount(s);
            Extend(byteCount);
            int bytesWritten = UTF8.GetBytes(s, 0, s.Length, Data!, Length);
            Length += bytesWritten;
        }
        return default(Error);
    }

    /// <summary>
    /// Appends a string to the buffer.
    /// </summary>
    /// <param name="s">The string to append; it will be encoded as UTF-8 bytes</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return default(Error);
        }
        int byteCount = UTF8.GetByteCount(s);
        Extend(byteCount);
        int bytesWritten = UTF8.GetBytes(s, 0, s.Length, Data!, Length);
        Length += bytesWritten;
        return default(Error);
    }

    /// <summary>
    /// Appends a single byte to the buffer.
    /// </summary>
    /// <param name="c">The byte to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(byte c)
    {
        Extend(1);
        Data![Length] = c;
        Length++;
        return default(Error);
    }

    /// <summary>
    /// Appends a UTF-8 encoded byte sequence to the buffer.
    /// </summary>
    /// <param name="s">The UTF-8 byte sequence to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(ReadOnlySpan<byte> s)
    {
        if (s.IsEmpty)
        {
            return default(Error);
        }
        Extend(s.Length);
        s.CopyTo(Data.AsSpan(Length));
        Length += s.Length;
        return default(Error);
    }

    private const int maxIntegerLength = 20;
    private const int maxBoolLength = 5;
    private const int maxUtcDatetimeLength = 28;

    /// <summary>
    /// Appends an integer to the buffer. Note: the maximum length depends on the value range.
    /// For a 64-bit signed integer, the maximum is 20 bytes (including the minus sign).
    /// If Utf8Formatter.TryFormat fails, an error object is returned.
    /// </summary>
    /// <param name="value">The integer to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(long value)
    {
        Extend(maxIntegerLength);
        if (!Utf8Formatter.TryFormat(value, Data.AsSpan(Length), out int bytesWritten))
        {
            return new Error(code: CodeOfFormatFail, message: Utf8FormatterFailedMessage);
        }
        Length += bytesWritten;
        return default(Error);
    }

    /// <summary>
    /// Appends an unsigned integer to the buffer. Note: the maximum length depends on the value range.
    /// For a 64-bit unsigned integer, the maximum is 20 bytes.
    /// If Utf8Formatter.TryFormat fails, an error object is returned.
    /// </summary>
    /// <param name="value">The unsigned integer to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(System.UInt64 value)
    {
        Extend(maxIntegerLength);
        if (!System.Buffers.Text.Utf8Formatter.TryFormat(value, Data.AsSpan(Length), out int bytesWritten))
        {
            return new Error(code: CodeOfFormatFail, message: Utf8FormatterFailedMessage);
        }
        Length += bytesWritten;
        return default(Error);
    }

    private const int maxFloat64Length = 64;

    /// <summary>
    /// Appends a double-precision floating-point number to the buffer. Note: the maximum length depends on the value
    /// and format. An IEEE 754 double can be up to 24 bytes (including decimal point and exponent); 64 bytes are
    /// reserved for safety. If Utf8Formatter.TryFormat fails, an error object is returned.
    /// </summary>
    /// <param name="value">The double value to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(double value)
    {
        Extend(maxFloat64Length);
        if (!Utf8Formatter.TryFormat(value, Data.AsSpan(Length), out int bytesWritten))
        {
            return new Error(code: CodeOfFormatFail, message: Utf8FormatterFailedMessage);
        }
        Length += bytesWritten;
        return default(Error);
    }

    /// <summary>
    /// Appends a boolean value to the buffer. The string representation is "true" (4 bytes) or "false" (5 bytes).
    /// </summary>
    /// <param name="value">The boolean value to append</param>
    /// <returns>An Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error Append(bool value)
    {
        Extend(maxBoolLength);
        if (value)
        {
            "true"u8.CopyTo(Data.AsSpan(Length));
            Length += 4;
        }
        else
        {
            "false"u8.CopyTo(Data.AsSpan(Length));
            Length += 5;
        }
        return default(Error);
    }

    /// <summary>
    /// Appends a UTC datetime to the buffer in ISO 8601 format, e.g. "2024-06-01T12:34:56.7890000Z" (28 bytes).
    /// </summary>
    /// <param name="dtm">The UTC datetime to append</param>
    /// <returns>An Error object. Never produces an error; included for API consistency.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Error AppendUtcDatetime(DateTime dtm)
    {
        if (dtm.Kind == DateTimeKind.Local)
        {
            dtm = dtm.ToUniversalTime();
        }
        else if (dtm.Kind == DateTimeKind.Unspecified)
        {
            dtm = DateTime.SpecifyKind(dtm, DateTimeKind.Utc);
        }
        Extend(maxUtcDatetimeLength);
        var dst = Data.AsSpan(Length, maxUtcDatetimeLength);

        int year = dtm.Year;
        int month = dtm.Month;
        int day = dtm.Day;
        int hour = dtm.Hour;
        int minute = dtm.Minute;
        int second = dtm.Second;
        int fraction = (int)(dtm.Ticks % TimeSpan.TicksPerSecond);

        Write4(year, dst, 0);
        dst[4] = (byte)'-';
        Write2(month, dst, 5);
        dst[7] = (byte)'-';
        Write2(day, dst, 8);
        dst[10] = (byte)'T';
        Write2(hour, dst, 11);
        dst[13] = (byte)':';
        Write2(minute, dst, 14);
        dst[16] = (byte)':';
        Write2(second, dst, 17);
        dst[19] = (byte)'.';
        Write7(fraction, dst, 20);
        dst[27] = (byte)'Z';

        Length += maxUtcDatetimeLength;
        return default(Error);

        static void Write2(int value, Span<byte> destination, int offset)
        {
            destination[offset] = (byte)('0' + (value / 10));
            destination[offset + 1] = (byte)('0' + (value % 10));
        }

        static void Write4(int value, Span<byte> destination, int offset)
        {
            destination[offset + 3] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 2] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 1] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset] = (byte)('0' + (value % 10));
        }

        static void Write7(int value, Span<byte> destination, int offset)
        {
            destination[offset + 6] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 5] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 4] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 3] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 2] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset + 1] = (byte)('0' + (value % 10));
            value /= 10;
            destination[offset] = (byte)('0' + (value % 10));
        }
    }

    /// <summary>
    /// Creates an independent copy of the current buffer. The clone has the same content but its own backing memory;
    /// mutations to one do not affect the other. A new block is rented from the pool and the used bytes are copied.
    /// If the buffer is very large, the pool may throw an OutOfMemoryException.
    /// </summary>
    /// <returns>A new RentedBuffer instance</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RentedBuffer Clone()
    {
        RentedBuffer cloned = new(Data!.Length);
        Array.Copy(Data, cloned.Data!, Length);
        cloned.Length = Length;
        return cloned;
    }

    /// <summary>
    /// Appends a string to the buffer with JSON escaping. Special characters such as <c>"</c>, <c>\</c>, and <c>\n</c>
    /// are replaced with their escape sequences. Strings with many special characters may grow significantly in size,
    /// potentially causing the pool to throw an OutOfMemoryException.
    /// </summary>
    /// <param name="s">The string to append</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendAsJsonEscapedString(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        AppendAsJsonEscapedString(s.AsSpan());
    }

    /// <summary>
    /// Appends a string to the buffer with JSON escaping. Special characters such as <c>"</c>, <c>\</c>, and <c>\n</c>
    /// are replaced with their escape sequences. Strings with many special characters may grow significantly in size,
    /// potentially causing the pool to throw an OutOfMemoryException.
    /// Zero-heap-allocation version: writes JSON-escaped UTF-8 bytes directly into the buffer.
    /// Uses stackalloc for short strings (UTF-8 encoded length ≤ 1024 bytes), otherwise rents from ArrayPool.
    /// </summary>
    /// <param name="s">The string to append</param>
    public void AppendAsJsonEscapedString(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty) return;

        int maxUtf8Bytes = UTF8.GetMaxByteCount(s.Length);
        // Worst case for JSON escaping: 1 byte → \uXXXX (6 bytes)
        int maxEscapedBytes = maxUtf8Bytes * 6;
        Extend(maxEscapedBytes);

        if (maxUtf8Bytes <= 1024)
        {
            Span<byte> utf8Temp = stackalloc byte[maxUtf8Bytes];
            int utf8Length = UTF8.GetBytes(s, utf8Temp);
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping.EncodeUtf8(
                utf8Temp[..utf8Length],
                Data!.AsSpan(Length, maxEscapedBytes),
                out _, out int bytesWritten);
            Length += bytesWritten;
        }
        else
        {
            byte[] utf8Temp = ArrayPool<byte>.Shared.Rent(maxUtf8Bytes);
            try
            {
                int utf8Length = UTF8.GetBytes(s, utf8Temp);
                JavaScriptEncoder.UnsafeRelaxedJsonEscaping.EncodeUtf8(
                    utf8Temp.AsSpan(0, utf8Length),
                    Data!.AsSpan(Length, maxEscapedBytes),
                    out _, out int bytesWritten);
                Length += bytesWritten;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(utf8Temp);
            }
        }
    }

    /// <summary>
    /// Appends a UTF-8 byte span to the buffer with JSON escaping. Special characters such as <c>"</c>, <c>\</c>,
    /// <c>\t</c>, and <c>\n</c> are replaced with their escape sequences. Strings with many special characters
    /// may grow significantly in size, potentially causing the pool to throw an OutOfMemoryException.
    /// </summary>
    /// <param name="s">The UTF-8 byte span to append</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendAsJsonEscapedString(ReadOnlySpan<byte> s)
    {
        int needed = s.Length * 2;
        Extend(needed);
        foreach (var b in s)
        {
            switch (b)
            {
                case (byte)'\t':
                    Data![Length] = (byte)'\\';
                    Data[Length + 1] = (byte)'t';
                    Length += 2;
                    break;
                case (byte)'\n':
                    Data![Length] = (byte)'\\';
                    Data[Length + 1] = (byte)'n';
                    Length += 2;
                    break;
                case (byte)'\\':
                    Data![Length] = (byte)'\\';
                    Data[Length + 1] = (byte)'\\';
                    Length += 2;
                    break;
                case (byte)'"':
                    Data![Length] = (byte)'\\';
                    Data[Length + 1] = (byte)'"';
                    Length += 2;
                    break;
                default:
                    Data![Length] = b;
                    Length++;
                    break;
            }
        }
    }
}
