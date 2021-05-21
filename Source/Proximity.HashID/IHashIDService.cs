using System;

namespace Proximity.HashID
{
    public interface IHashIDService
    {
        bool TryEncode(ReadOnlySpan<long> numbers, Span<char> result, out int charsWritten);
    }
}
