using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;

namespace Proximity.HashID
{
    public partial class HashIDService : IHashIDService, IDisposable
    {
        public byte[] DecodeBinary(string hash) => DecodeBinary(hash.AsSpan());

        public byte[] DecodeBinary(ReadOnlySpan<char> hash)
        {
            var Length = MeasureDecode(hash);

            if (Length == 0)
                return Array.Empty<byte>();

            Span<byte> Result = stackalloc byte[Length * 8];

            if (!TryDecodeBinary(hash, Result, out var BytesWritten))
                return Array.Empty<byte>();

            return Result.Slice(0, BytesWritten).ToArray();
        }

        public string DecodeHex(string hash) => DecodeHex(hash.AsSpan());

        public string DecodeHex(ReadOnlySpan<char> hash)
        {
            var Length = MeasureDecodeHex(hash);

            if (Length == 0)
                return string.Empty;

            Span<char> Result = stackalloc char[Length];

            if (!TryDecodeHex(hash, Result, out var CharsWritten))
                return string.Empty;

            return Result.Slice(0, CharsWritten).ToString();
        }

        public int[] DecodeInt32(string hash) => DecodeInt32(hash.AsSpan());

        public int[] DecodeInt32(ReadOnlySpan<char> hash)
        {
            var Length = MeasureDecode(hash);

            if (Length == 0)
                return Array.Empty<int>();

            Span<long> Values = stackalloc long[Length];

            if (!InternalDecode(hash, Values, out var ValuesWritten))
                return Array.Empty<int>();

            if (ValuesWritten != Length)
                throw new InvalidOperationException("Did not measure correctly");

            var Results = new int[Length];

            for (var Index = 0; Index < Length; Index++)
            {
                var Value = Values[Index];

                if (Value > int.MaxValue || Value < int.MinValue)
                    return Array.Empty<int>();

                Results[Index] = (int)Value;
            }

            return Results;
        }

        public long[] DecodeInt64(string hash) => DecodeInt64(hash.AsSpan());

        public long[] DecodeInt64(ReadOnlySpan<char> hash)
        {
            var Length = MeasureDecode(hash);

            if (Length == 0)
                return Array.Empty<long>();

            var Results = new long[Length];

            if (!InternalDecode(hash, Results, out var ValuesWritten))
                return Array.Empty<long>();

            if (ValuesWritten != Length)
                throw new InvalidOperationException("Did not measure correctly");

            return Results;
        }

        public int DecodeSingleInt32(string hash) => DecodeSingleInt32(hash.AsSpan());

        public int DecodeSingleInt32(ReadOnlySpan<char> hash)
        {
            if (!TryDecodeSingleInt32(hash, out var Value))
                return 0;

            return Value;
        }

        public long DecodeSingleInt64(string hash) => DecodeSingleInt64(hash.AsSpan());

        public long DecodeSingleInt64(ReadOnlySpan<char> hash)
        {
            if (!TryDecodeSingleInt64(hash, out var Value))
                return 0;

            return Value;
        }
        
        public int MeasureDecode(ReadOnlySpan<char> hash)
        {
            if (hash.IsEmpty)
                return 0;

            // Find the first guard character (if any)
            var LeftGuard = hash.IndexOfAny(guards.Span);

            if (LeftGuard != -1)
            {
                hash = hash.Slice(LeftGuard + 1);

                // Find the second guard character (if any)
                var RightGuard = hash.IndexOfAny(guards.Span);

                // Trim the hash down to the values within the guards
                if (RightGuard != -1)
                    hash = hash.Slice(0, RightGuard);
            }

            if (hash.IsEmpty)
                return 0;
            
            var Values = 0;
            
            for (; ;)
            {
                Values++;

                var NextIndex = hash.IndexOfAny(separators.Span);

                if (NextIndex == -1)
                    break;
                
                hash = hash.Slice(NextIndex + 1);
            }

            return Values;
        }

        public int MeasureDecodeHex(ReadOnlySpan<char> hash)
        {
            if (hash.IsEmpty)
                return 0;

            // Find the first guard character (if any)
            var LeftGuard = hash.IndexOfAny(guards.Span);

            if (LeftGuard != -1)
            {
                hash = hash.Slice(LeftGuard + 1);

                // Find the second guard character (if any)
                var RightGuard = hash.IndexOfAny(guards.Span);

                // Trim the hash down to the values within the guards
                if (RightGuard != -1)
                    hash = hash.Slice(0, RightGuard);
            }

            if (hash.IsEmpty)
                return 0;
            
            var Values = 0;
            
            for (; ;)
            {
                Values += 12; // Measure returns the worst-case

                var NextIndex = hash.IndexOfAny(separators.Span);

                if (NextIndex == -1)
                    break;
                
                hash = hash.Slice(NextIndex + 1);
            }

            return Values;
        }

        public bool TryDecodeBinary(ReadOnlySpan<char> hash, Span<byte> bytes, out int bytesWritten)
        {
            if (hash.IsEmpty)
            {
                bytesWritten = 0;

                return true;
            }

            var Length = MeasureDecode(hash);
            Span<long> Values = stackalloc long[Length];

            // First decode the hash into its component longs
            if (!InternalDecode(hash, Values, out var ValuesWritten))
            {
                bytesWritten = 0;

                return false;
            }
            
            if (ValuesWritten != Length)
                throw new InvalidOperationException("Did not measure correctly");

            bytesWritten = 0;

            Span<byte> TempSegment = stackalloc byte[8];

            for (var Index = 0; Index < Values.Length; Index++)
            {
                var Value = Values[Index];

                if (bytes.IsEmpty)
                {
                    bytesWritten = 0;

                    return false;
                }

                if (bytes.Length >= 8)
                {
                    // Read the full long directly into our output
                    BinaryPrimitives.WriteInt64LittleEndian(bytes, Value);

                    if (Index == Values.Length - 1)
                    {
                        // We're the last long, so we must have a 1-terminator
                        var RemainingLength = bytes.Slice(0, 8).LastIndexOf((byte)1);

                        if (RemainingLength == -1)
                        {
                            // We're not an encoded binary
                            bytesWritten = 0;

                            return false;
                        }

                        bytesWritten += RemainingLength;
                    }
                    else
                    {
                        bytesWritten += 8;
                    }
                }
                else
                {
                    // Not enough bytes left in the buffer, but if we're the last segment that's okay
                    BinaryPrimitives.WriteInt64LittleEndian(TempSegment, Value);

                    if (Index == Values.Length - 1)
                    {
                        var RemainingLength = bytes.LastIndexOf((byte)1);

                        if (RemainingLength == -1 || bytes.Length < RemainingLength)
                        {
                            // We're not an encoded binary
                            bytesWritten = 0;

                            return false;
                        }

                        TempSegment.Slice(0, RemainingLength).CopyTo(bytes);

                        bytesWritten += RemainingLength;
                    }
                    else
                    {

                        bytesWritten = 0;

                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryDecodeHex(string hash, out string hexString)
        {
            if (hash is null || hash.Length == 0)
            {
                hexString = string.Empty;

                return true;
            }

            var Length = MeasureDecodeHex(hash.AsSpan());

            Span<char> Result = stackalloc char[Length];

            if (!TryDecodeHex(hash.AsSpan(), Result, out var CharsWritten))
            {
                hexString = string.Empty;

                return false;
            }
            
            hexString = Result.Slice(0, CharsWritten).ToString();

            return true;
        }

        public bool TryDecodeHex(ReadOnlySpan<char> hash, Span<char> hexString, out int charsWritten)
        {
            if (hash.IsEmpty)
            {
                charsWritten = 0;

                return true;
            }

            var Length = MeasureDecode(hash);
            Span<long> Values = stackalloc long[Length];

            // First decode the hash into its component longs
            if (!InternalDecode(hash, Values, out var ValuesWritten))
            {
                charsWritten = 0;

                return false;
            }
            
            if (ValuesWritten != Length)
                throw new InvalidOperationException("Did not measure correctly");

#if !NETSTANDARD2_0
            Span<char> TempSegment = stackalloc char[13];
#endif
            charsWritten = 0;

            foreach (var Value in Values)
            {
                if (hexString.IsEmpty)
                {
                    charsWritten = 0;

                    return false;
                }

#if NETSTANDARD2_0
                var Segment = Value.ToString("X", CultureInfo.InvariantCulture).AsSpan();

                if (Segment.Length > 13 || Segment[0] != '1')
                {
                    charsWritten = 0;

                    return false;
                }

                Segment = Segment.Slice(1);
#else
                if (!Value.TryFormat(TempSegment, out var CharsWritten, "X") || CharsWritten < 2 || TempSegment[0] != '1')
                {
                    charsWritten = 0;

                    return false;
                }

                var Segment = TempSegment.Slice(1, CharsWritten - 1);
#endif
                if (hexString.Length < Segment.Length)
                {
                    charsWritten = 0;

                    return false;
                }

                Segment.CopyTo(hexString);

                charsWritten += Segment.Length;

                hexString = hexString.Slice(Segment.Length);
            }

            return true;
        }

        public bool TryDecodeSingleInt32(string hash, out int value) => TryDecodeSingleInt32(hash.AsSpan(), out value);

        public bool TryDecodeSingleInt32(ReadOnlySpan<char> hash, out int value)
        {
            if (TryDecodeSingleInt64(hash, out var Value) && Value <= int.MaxValue && Value >= int.MinValue)
            {
                value = (int)Value;

                return true;
            }

            value = 0;

            return false;
        }

        public bool TryDecodeSingleInt64(string hash, out long value) => TryDecodeSingleInt64(hash.AsSpan(), out value);

        public bool TryDecodeSingleInt64(ReadOnlySpan<char> hash, out long value)
        {
            if (hash.IsEmpty)
            {
                value = 0;

                return true;
            }

            Span<long> Values = stackalloc long[1];

            if (!InternalDecode(hash, Values, out var Written))
            {
                value = default;
                return false;
            }

            value = Values[0];

            return true;
        }

        public bool TryDecode(string hash, out long[] numbers) => TryDecode(hash.AsSpan(), out numbers);
        
        public bool TryDecode(ReadOnlySpan<char> hash, out long[] numbers)
        {
            if (hash.IsEmpty)
            {
                numbers = Array.Empty<long>();

                return true;
            }

            var Length = MeasureDecode(hash);

            var Results = new long[Length];

            if (!InternalDecode(hash, Results.AsSpan(), out var NumbersWritten))
            {
                numbers = null!;

                return false;
            }

            if (NumbersWritten != Length)
                throw new InvalidOperationException("Did not measure correctly");
            
            numbers = Results;

            return true;
        }

        public bool TryDecode(ReadOnlySpan<char> hash, Span<long> numbers, out int numbersWritten)
        {
            if (hash.IsEmpty)
            {
                numbersWritten = 0;

                return true;
            }

            return InternalDecode(hash, numbers, out numbersWritten);
        }

        private bool InternalDecode(ReadOnlySpan<char> hash, Span<long> numbers, out int numbersWritten)
        {
            var ResultLength = 0;
            var Hash = hash;

            // Copy the alphabet to a buffer we can shuffle around
            Span<char> Alphabet = stackalloc char[alphabet.Length];
            alphabet.Span.CopyTo(Alphabet);

            // Find the first guard character (if any)
            var LeftGuard = Hash.IndexOfAny(guards.Span);

            if (LeftGuard != -1)
            {
                Hash = Hash.Slice(LeftGuard + 1);

                // Find the second guard character (if any)
                var RightGuard = Hash.IndexOfAny(guards.Span);

                // Trim the hash down to the values within the guards
                if (RightGuard != -1)
                    Hash = Hash.Slice(0, RightGuard);
            }

            // Maybe someone generated a hash that's all guards, no numbers
            if (Hash.IsEmpty || Hash[0] == '\0')
            {
                numbersWritten = 0;

                return true;
            }

            // Find the lottery character
            var Lottery = Hash[0];
            Hash = Hash.Slice(1);

            // We only need it to be the same length as the Alphabet though
            Span<char> Buffer = stackalloc char[Alphabet.Length];
            Buffer[0] = Lottery;
            salt.Span.Slice(0, Math.Min(Alphabet.Length - 1, salt.Length)).CopyTo(Buffer.Slice(1));

            for (; ;)
            {
                var NextSeparator = Hash.IndexOfAny(separators.Span);
                var SubHash = NextSeparator == -1 ? Hash : Hash.Slice(0, NextSeparator);

                // Fill the remainder of the shuffle buffer with the current alphabet
                if (salt.Length + 1 < Alphabet.Length)
                    Alphabet.Slice(0, Alphabet.Length - salt.Length - 1).CopyTo(Buffer.Slice(salt.Length + 1));

                ConsistentShuffle(Alphabet, Buffer);

                if (TryUnhash(SubHash, Alphabet, out var Value))
                {
                    numbers[ResultLength++] = Value;
                }
                else
                {
                    numbersWritten = 0;

                    return false;
                }

                if (NextSeparator == -1)
                    break;

                Hash = Hash.Slice(NextSeparator + 1);
            }

            // Now we validate the calculation by encoding it again
            // If the salt used to decode is different from the salt used to encode, 
            Span<char> TempHash = stackalloc char[hash.Length];

            if (!InternalEncode(numbers.Slice(0, ResultLength), TempHash, out var EncodedChars) || EncodedChars != hash.Length || !hash.SequenceEqual(TempHash))
            {
                numbersWritten = 0;

                return false;
            }

            numbersWritten = ResultLength;

            return true;
        }

        private bool TryUnhash(ReadOnlySpan<char> input, Span<char> alphabet, out long number)
        {
            long Number = 0;

            foreach (var Char in input)
            {
                var Position = alphabet.IndexOf(Char);

                // Is this character within the alphabet?
                if (Position == -1)
                {
                    number = 0;
                    return false;
                }

                Number = Number * alphabet.Length + Position;
            }

            number = Number;

            return true;
        }
    }
}
