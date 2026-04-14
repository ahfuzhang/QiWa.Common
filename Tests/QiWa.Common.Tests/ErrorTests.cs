using System.Text.RegularExpressions;
using QiWa.Common;
using Xunit;

namespace QiWa.Common.Tests;

public class ErrorTests
{
    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsCodeAndMessage()
    {
        var error = new Error(42u, "test error");

        Assert.Equal(42u, error.Code);
        Assert.Equal("test error", error.Message);
    }

    [Fact]
    public void Constructor_WithZeroCode_StoresZero()
    {
        var error = new Error(0u, "no error");

        Assert.Equal(0u, error.Code);
        Assert.Equal("no error", error.Message);
    }

    [Fact]
    public void Default_HasZeroCode()
    {
        var error = default(Error);

        Assert.Equal(0u, error.Code);
    }

    // ─── Err() ────────────────────────────────────────────────────────────────

    [Fact]
    public void Err_ReturnsFalse_WhenCodeIsZero()
    {
        var error = new Error(0u, "no error");

        Assert.False(error.Err());
    }

    [Fact]
    public void Err_ReturnsFalse_ForDefaultError()
    {
        var error = default(Error);

        Assert.False(error.Err());
    }

    [Fact]
    public void Err_ReturnsTrue_WhenCodeIsOne()
    {
        var error = new Error(1u, "error occurred");

        Assert.True(error.Err());
    }

    [Fact]
    public void Err_ReturnsTrue_WhenCodeIsArbitraryNonZero()
    {
        var error = new Error(255u, "some error");

        Assert.True(error.Err());
    }

    [Fact]
    public void Err_ReturnsTrue_WhenCodeIsMaxValue()
    {
        var error = new Error(uint.MaxValue, "max error");

        Assert.True(error.Err());
    }

    // ─── WithLoc() ────────────────────────────────────────────────────────────

    [Fact]
    public void WithLoc_SetsCode()
    {
        var error = Error.WithLoc(99u, "test message");

        Assert.Equal(99u, error.Code);
    }

    [Fact]
    public void WithLoc_MessageStripsDirectoryFromFilePath()
    {
        var error = Error.WithLoc(1u, "msg");

        // First line of message should be "filename:line (member)"
        var firstLine = error.Message.Split('\n')[0];
        // Should contain only the filename, not the full path
        Assert.StartsWith("ErrorTests.cs:", firstLine);
    }

    [Fact]
    public void WithLoc_MessageContainsCallerMemberName()
    {
        var error = Error.WithLoc(1u, "msg");

        Assert.Contains(nameof(WithLoc_MessageContainsCallerMemberName), error.Message);
    }

    [Fact]
    public void WithLoc_MessageContainsOriginalMessage()
    {
        const string original = "my specific error description";
        var error = Error.WithLoc(1u, original);

        Assert.Contains(original, error.Message);
    }

    [Fact]
    public void WithLoc_MessageContainsCode()
    {
        var error = Error.WithLoc(123u, "msg");

        Assert.Contains("123", error.Message);
    }

    [Fact]
    public void WithLoc_MessageContainsLineNumber()
    {
        var error = Error.WithLoc(1u, "msg");

        // Format: "ErrorTests.cs:42 (MethodName)"
        Assert.Matches(new Regex(@"ErrorTests\.cs:\d+ \("), error.Message);
    }

    [Fact]
    public void WithLoc_MessageFollowsExpectedFormat()
    {
        var error = Error.WithLoc(7u, "description");

        // Format: "file:line (member)\n\t Code=code,Message=message"
        Assert.Contains("\n\t Code=7,Message=description", error.Message);
    }

    [Fact]
    public void WithLoc_ReturnsErrorWithErrTrue()
    {
        var error = Error.WithLoc(1u, "msg");

        Assert.True(error.Err());
    }
}
