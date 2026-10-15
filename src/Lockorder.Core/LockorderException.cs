namespace Lockorder.Core;

public sealed class LockorderException : Exception
{
    public LockorderException(string message)
        : base(message)
    {
    }
}
