using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using QiWa.Common;

namespace QiWa.Common.Benchmarks;

[Config(typeof(AppendAsJsonEscapedStringConfig))]
[MemoryDiagnoser]
public class AppendAsJsonEscapedStringBenchmarks
{
    private const int OperationsPerInvoke = 1024;

    private const string Payload =
        "benchmark payload with escape chars:\n newline \t tab \" double-quote \\ backslash; " +
        "padding to ensure length exceeds one hundred bytes: 0123456789abcdef0123456789abcdef";

    public static int InputByteCount { get; } = Encoding.UTF8.GetByteCount(Payload);

    private string _payload = string.Empty;
    private byte[] _payloadUtf8 = [];
    private RentedBuffer _buffer;

    [GlobalSetup]
    public void Setup()
    {
        _payload = Payload;
        _payloadUtf8 = Encoding.UTF8.GetBytes(_payload);

        int charOverloadWorstCase = Encoding.UTF8.GetMaxByteCount(_payload.Length) * 6;
        _buffer = new RentedBuffer(charOverloadWorstCase + 64);
        VerifyEquivalentResults();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int StringOverload()
    {
        int totalLength = 0;

        for (int i = 0; i < OperationsPerInvoke; i++)
        {
            _buffer.Length = 0;
            AppendString();
            totalLength += _buffer.Length;
        }

        return totalLength;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
    public int CharSpanOverload()
    {
        int totalLength = 0;

        for (int i = 0; i < OperationsPerInvoke; i++)
        {
            _buffer.Length = 0;
            AppendCharSpan();
            totalLength += _buffer.Length;
        }

        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int ByteSpanOverload()
    {
        int totalLength = 0;

        for (int i = 0; i < OperationsPerInvoke; i++)
        {
            _buffer.Length = 0;
            AppendByteSpan();
            totalLength += _buffer.Length;
        }

        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public int ByteSpanV2Overload()
    {
        int totalLength = 0;

        for (int i = 0; i < OperationsPerInvoke; i++)
        {
            _buffer.Length = 0;
            AppendByteSpanV2();
            totalLength += _buffer.Length;
        }

        return totalLength;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendString()
    {
        _buffer.AppendAsJsonEscapedString(_payload);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendCharSpan()
    {
        _buffer.AppendAsJsonEscapedString(_payload.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendByteSpan()
    {
        _buffer.AppendAsJsonEscapedString(_payloadUtf8.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendByteSpanV2()
    {
        _buffer.AppendAsJsonEscapedStringV2(_payloadUtf8.AsSpan());
    }

    private void VerifyEquivalentResults()
    {
        byte[] stringResult = RunOnce(AppendString);
        byte[] charSpanResult = RunOnce(AppendCharSpan);
        byte[] byteSpanResult = RunOnce(AppendByteSpan);
        byte[] byteSpanV2Result = RunOnce(AppendByteSpanV2);

        VerifyEqual(nameof(StringOverload), stringResult, nameof(CharSpanOverload), charSpanResult);
        VerifyEqual(nameof(StringOverload), stringResult, nameof(ByteSpanOverload), byteSpanResult);
        VerifyEqual(nameof(StringOverload), stringResult, nameof(ByteSpanV2Overload), byteSpanV2Result);

        _buffer.Length = 0;
    }

    private byte[] RunOnce(Action append)
    {
        _buffer.Length = 0;
        append();
        return _buffer.AsSpan().ToArray();
    }

    private static void VerifyEqual(string expectedName, byte[] expected, string actualName, byte[] actual)
    {
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{actualName} output differs from {expectedName}. " +
            $"{expectedName}: length={expected.Length}, value={Encoding.UTF8.GetString(expected)}; " +
            $"{actualName}: length={actual.Length}, value={Encoding.UTF8.GetString(actual)}");
    }
}

public sealed class AppendAsJsonEscapedStringConfig : ManualConfig
{
    private const int InvocationCount = 4096;

    public AppendAsJsonEscapedStringConfig()
    {
        BuildTimeout = TimeSpan.FromMinutes(10);
        AddJob(Job.Default
            .WithInvocationCount(InvocationCount)
            .WithUnrollFactor(1)
            .WithWarmupCount(8)
            .WithIterationCount(15));
        AddColumn(new BytesPerSecondColumn());
    }
}

public sealed class BytesPerSecondColumn : IColumn
{
    public string Id => nameof(BytesPerSecondColumn);
    public string ColumnName => "Bytes/s";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Input bytes processed per second per logical append operation, based on the benchmark mean.";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics is not { Mean: > 0 } statistics)
        {
            return "NA";
        }

        int inputBytes = AppendAsJsonEscapedStringBenchmarks.InputByteCount;
        double secondsPerInvocation = statistics.Mean / 1_000_000_000d;
        return FormatBytesPerSecond(inputBytes / secondsPerInvocation);
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        return GetValue(summary, benchmarkCase);
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase)
    {
        return false;
    }

    public bool IsAvailable(Summary summary)
    {
        return true;
    }

    private static string FormatBytesPerSecond(double bytesPerSecond)
    {
        string[] units = ["B/s", "KiB/s", "MiB/s", "GiB/s"];
        int unitIndex = 0;

        while (bytesPerSecond >= 1024 && unitIndex < units.Length - 1)
        {
            bytesPerSecond /= 1024;
            unitIndex++;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{bytesPerSecond:N2} {units[unitIndex]}");
    }
}
