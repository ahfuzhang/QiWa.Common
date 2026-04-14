using System.Runtime.CompilerServices;

namespace QiWa.Common;

/// <summary>
/// Encapsulates an error object. Inspired by Go's error design: Code=0 means no error occurred, Code!=0 means an error occurred, and Message contains the error description.
/// </summary>
public readonly struct Error
{
    /// <summary>
    /// Error code. 0 means no error occurred, non-zero means an error occurred.
    /// </summary>
    public readonly System.UInt32 Code;

    /// <summary>
    /// Error message.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="code">Error code</param>
    /// <param name="message">Error message</param>
    public Error(System.UInt32 code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Returns whether an error has occurred.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Err()
    {
        return Code != 0;
    }

    /// <summary>
    /// Creates an error object with source location information attached.
    /// </summary>
    /// <param name="code">Error code</param>
    /// <param name="message">Error message</param>
    /// <param name="file">Provided by the compiler</param>
    /// <param name="member">Provided by the compiler</param>
    /// <param name="line">Provided by the compiler</param>
    /// <returns>Error object</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Error WithLoc(System.UInt32 code, string message,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0
    )
    {
        for (int i = file.Length - 1; i >= 0; i--)
        {
            if (file[i] == '/' || file[i] == '\\')
            {
                file = file.Substring(i + 1);
                break;
            }
        }
        return new Error(code, $"{file}:{line} ({member})\n\t Code={code},Message={message}");
    }
}
