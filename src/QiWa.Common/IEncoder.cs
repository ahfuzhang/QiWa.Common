#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace QiWa.Common;

public interface IEncoder
{
    int ProtobufSize();
    Error ToProtobuf(ref RentedBuffer buf);
    void ToJSON(ref RentedBuffer buf);
}
