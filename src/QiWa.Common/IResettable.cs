namespace QiWa.Common;

/// <summary>
/// Defines an interface for objects that can be reset to their initial state.
/// Implementing this interface allows an object to be reused without needing to create a new instance, which can be beneficial for performance and memory management in certain scenarios.
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Set all members of the object to their default values, effectively resetting the object to its initial state.
    /// </summary>
    void Reset();
}

