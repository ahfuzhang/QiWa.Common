#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace QiWa.Common;

public interface IDecoder
{
    Error FromProtobuf(ReadOnlySpan<byte> binary);
    Error FromJSON(ReadOnlySpan<byte> text);
}
