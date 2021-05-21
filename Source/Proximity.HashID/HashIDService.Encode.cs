using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;

namespace Proximity.HashID
{
    public partial class HashIDService
    {
        public string Encode(int number)
        {
            Span<long> Numbers = stackalloc long[1];

            Numbers[0] = number;

            return Encode(Numbers);
        }

        public string Encode(int[] numbers) => Encode(numbers.AsSpan());

        public string Encode(ReadOnlySpan<int> numbers)
        {
            // If there's no numbers, we don't write anything
            if (numbers.IsEmpty)
                return string.Empty;

            Span<long> Numbers = stackalloc long[numbers.Length];

            for (var Index = 0; Index < numbers.Length; Index++)
                Numbers[Index] = numbers[Index];

            var MaximumLength = MeasureEncode(Numbers);

            Span<char> Output = stackalloc char[MaximumLength];

            if (!InternalEncode(Numbers, Output, out var ActualLength))
                throw new InvalidOperationException("Did not measure correctly");

            return Output.Slice(0, ActualLength).ToString();
        }

        public string Encode(long number)
        {
            Span<long> Numbers = stackalloc long[1];
            
            Numbers[0] = number;

            return Encode(Numbers);
        }

        public string Encode(params long[] numbers) => Encode(numbers.AsSpan());

        public string Encode(ReadOnlySpan<long> numbers)
        {
            // If there's no numbers, we don't write anything
            if (numbers.IsEmpty)
                return string.Empty;

            var MaximumLength = MeasureEncode(numbers);

            Span<char> Output = stackalloc char[MaximumLength];

            if (!InternalEncode(numbers, Output, out var ActualLength))
                throw new InvalidOperationException("Did not measure correctly");

            return Output.Slice(0, ActualLength).ToString();
        }

        public string Encode(ReadOnlySpan<byte> bytes)
        {
            // If there's no bytes, we don't write anything
            if (bytes.IsEmpty)
                return string.Empty;

            var MaximumLength = MeasureEncode((bytes.Length / 8) + 1);

            Span<char> Output = stackalloc char[MaximumLength];

            if (!TryEncode(bytes, Output, out var ActualLength))
                throw new InvalidOperationException("Did not measure correctly");

            return Output.Slice(0, ActualLength).ToString();
        }

        public string EncodeHex(string hexString) => EncodeHex(hexString.AsSpan());

        public string EncodeHex(ReadOnlySpan<char> hexString)
        {
            if (hexString.IsEmpty)
                return string.Empty;

            var MaximumLength = MeasureEncodeHex(hexString);

            Span<char> Output = stackalloc char[MaximumLength];

            if (!TryEncodeHex(hexString, Output, out var ActualLength))
                return string.Empty;

            return Output.Slice(0, ActualLength).ToString();
        }

        public int MeasureEncode(int numbers)
        {
            if (numbers < 0)
                throw new ArgumentOutOfRangeException(nameof(numbers));

            if (numbers == 0)
                return 0;

            var Length = 1; // Lottery
            var AlphabetLength = numbers;

            // The maximum size of each value
            Length += maximumSegmentLength * numbers;

            // Number of separator characters
            Length += numbers - 1;

            // Minimum hash length
            return Math.Max(Length, minimumHashLength);

        }

        public int MeasureEncode(int[] numbers) => MeasureEncode(numbers.Length);

        public int MeasureEncode(long[] numbers) => MeasureEncode(numbers.Length);

        public int MeasureEncode(ReadOnlySpan<int> numbers) => MeasureEncode(numbers.Length);

        public int MeasureEncode(ReadOnlySpan<long> numbers) => MeasureEncode(numbers.Length);

        public int MeasureEncodeHex(int hexStringLength)
        {
            if (hexStringLength < 0)
                throw new ArgumentOutOfRangeException(nameof(hexStringLength));

            if (hexStringLength == 0)
                return 0;

            var Length = 1; // Lottery
            var AlphabetLength = alphabet.Length;
            var HexBlocks = (hexStringLength / 12) + 1;

            // The maximum size of each value
            Length += maximumHexSegmentLength * HexBlocks;

            // Number of separator characters
            Length += HexBlocks - 1;

            // Minimum hash length
            return Math.Max(Length, minimumHashLength);
        }

        public int MeasureEncodeHex(string hexString) => MeasureEncodeHex(hexString.Length);

        public int MeasureEncodeHex(ReadOnlySpan<char> hexString) => MeasureEncodeHex(hexString.Length);

        public bool TryEncodeHex(string hexString, Span<char> hash, out int charsWritten) => TryEncodeHex(hexString.AsSpan(), hash, out charsWritten);

        public bool TryEncodeHex(ReadOnlySpan<char> hexString, Span<char> hash, out int charsWritten)
        {
            if (hexString.IsEmpty)
            {
                charsWritten = 0;

                return true;
            }

            var HexBlocks = (hexString.Length / 12) + 1;
            var Index = 0;

            Span<long> Values = stackalloc long[HexBlocks];

            do
            {
                // We only use 12 out of the possible 15 (+1 for padding) hex characters that fit in a long
                var BlockSize = Math.Min(hexString.Length, 12);

#if NETSTANDARD2_0
                // TODO: Write our own span-based hex number parser
                if (!long.TryParse(hexString.Slice(0, BlockSize).ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var BlockValue))
#else
                if (!long.TryParse(hexString.Slice(0, BlockSize), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var BlockValue))
#endif
                {
                    charsWritten = 0;

                    return false;
                }

                // Hex values need to be prepended with 1. Since we're parsing in-place, we need to do a little bit-shifting to stick the 1 at the correct spot
                Values[Index++] = BlockValue | (1L << (4 * BlockSize));

                // Are there any more hex characters to include?
                hexString = hexString.Slice(BlockSize);
            }
            while (!hexString.IsEmpty);

            return InternalEncode(Values, hash, out charsWritten);
        }

        public bool TryEncode(byte[] bytes, Span<char> hash, out int charsWritten) => TryEncode(bytes.AsSpan(), hash, out charsWritten);

        public bool TryEncode(ReadOnlySpan<byte> bytes, Span<char> hash, out int charsWritten)
        {
            if (bytes.IsEmpty)
            {
                charsWritten = 0;

                return true;
            }

            var HexBlocks = (bytes.Length / 8) + 1;
            var Index = 0;

            Span<long> Values = stackalloc long[HexBlocks];
            Span<byte> Buffer = stackalloc byte[8];

            do
            {
                // We use all 8 bytes that fit in a long
                var BlockSize = Math.Min(bytes.Length, 8);

                if (BlockSize < 8)
                {
                    bytes.Slice(0, BlockSize).CopyTo(Buffer);

                    // The final block should have its last byte set to 1, so we can tell if it's all zeros
                    Buffer[BlockSize] = 1;

                    if (BlockSize < 7)
                        Buffer.Slice(BlockSize + 1).Clear();

                    Values[Index++] = BinaryPrimitives.ReadInt64LittleEndian(Buffer);
                }
                else
                {
                    Values[Index++] = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0, 8));
                }

                // Are there any more bytes to include?
                bytes = bytes.Slice(BlockSize);
            }
            while (!bytes.IsEmpty);

            if (Index != HexBlocks)
            {
                // We're a multiple of eight, so we have a final number set to one.
                Values[Index] = 1;
            }

            return InternalEncode(Values, hash, out charsWritten);
        }

        public bool TryEncode(long[] numbers, out string hash) => TryEncode(numbers.AsSpan(), out hash);

        public bool TryEncode(ReadOnlySpan<long> numbers, out string hash)
        {
            if (numbers.IsEmpty)
            {
                hash = string.Empty;

                return true;
            }

            var MaximumLength = MeasureEncode(numbers);

            Span<char> Output = stackalloc char[MaximumLength];

            if (!InternalEncode(numbers, Output, out var ActualLength))
                throw new InvalidOperationException("Did not measure correctly");

            hash = Output.Slice(0, ActualLength).ToString();

            return true;
        }

        public bool TryEncode(int number, Span<char> result, out int charsWritten)
        {
            Span<long> Numbers = stackalloc long[1];

            Numbers[0] = number;

            return InternalEncode(Numbers, result, out charsWritten);
        }

        public bool TryEncode(long number, Span<char> result, out int charsWritten)
        {
            Span<long> Numbers = stackalloc long[1];

            Numbers[0] = number;

            return InternalEncode(Numbers, result, out charsWritten);
        }

        public bool TryEncode(ReadOnlySpan<long> numbers, Span<char> result, out int charsWritten)
        {
            // If there's no numbers, we don't write anything
            if (numbers.IsEmpty)
            {
                charsWritten = 0;

                return true;
            }

            return InternalEncode(numbers, result, out charsWritten);
        }

        private bool InternalEncode(ReadOnlySpan<long> numbers, Span<char> result, out int charsWritten)
        {
            charsWritten = 0;

            // Ensure there's space for the lottery and minimum size
            if (result.IsEmpty || result.Length < minimumHashLength)
                return false;

            var ResultLength = 0;

            // Copy the alphabet to a buffer we can shuffle around
            Span<char> Alphabet = stackalloc char[alphabet.Length];
            alphabet.Span.CopyTo(Alphabet);

            // Generate a hash to use for the rest of the calculation
            var NumbersHash = 0L;

            for (var Index = 0; Index < numbers.Length; Index++)
                NumbersHash += numbers[Index] % (Index + 100);

            var Lottery = Alphabet[(int)(NumbersHash % Alphabet.Length)];
            result[ResultLength++] = Lottery;

            // Generate the buffer we use to shuffle the alphabet with
            // Each iteration, we replace the end of it with the current alphabet
            // We only need it to be the same length as the Alphabet though
            Span<char> Buffer = stackalloc char[Alphabet.Length];
            Buffer[0] = Lottery;
            salt.Span.Slice(0, Math.Min(Alphabet.Length - 1, salt.Length)).CopyTo(Buffer.Slice(1));

            for (var Index = 0; Index < numbers.Length; Index++)
            {
                var Number = numbers[Index];

                // Fill the remainder of the shuffle buffer with the current alphabet
                if (salt.Length + 1 < Alphabet.Length)
                    Alphabet.Slice(0, Alphabet.Length - salt.Length - 1).CopyTo(Buffer.Slice(salt.Length + 1));

                ConsistentShuffle(Alphabet, Buffer);

                var StartOffset = ResultLength;

                if (!Hash(result, ref ResultLength, Number, Alphabet))
                    return false;

                // If we're not the last item, add a separator
                if (Index + 1 < numbers.Length)
                {
                    if (result.Length == ResultLength)
                        return false;

                    var SeparatorIndex = (int)(Number % ((int)result[StartOffset] + Index)) % separators.Length;

                    result[ResultLength++] = separators.Span[SeparatorIndex];
                }
            }
            
            if (ResultLength < minimumHashLength)
            {
                if (result.Length == ResultLength)
                    return false;

                var GuardIndex = (int)(NumbersHash + (int)result[0]) % guards.Length;
                var Guard = guards.Span[GuardIndex];

                // Move the result up, since we need to prepend a guard value
                result.Slice(0, ResultLength).CopyTo(result.Slice(1));
                result[0] = Guard;
                ResultLength++;

                if (ResultLength < minimumHashLength)
                {
                    if (result.Length == ResultLength)
                        return false;

                    GuardIndex = (int)(NumbersHash + (int)result[2]) % guards.Length;
                    Guard = guards.Span[GuardIndex];

                    result[ResultLength++] = Guard;
                }
            }

            var HalfLength = Alphabet.Length / 2;

            // No need for bounds checks here, since we know we have Minimum Hash Length in the output buffer
            while (ResultLength < minimumHashLength)
            {
                // We're doing an in-place shuffle, so we need to copy the Alphabet first
                Alphabet.CopyTo(Buffer);
                ConsistentShuffle(Alphabet, Buffer);

                // We append/prepend alphabet characters to the result until we reach the minimum length
                var FinalLength = ResultLength + Alphabet.Length;
                // We may have more alphabet characters than we need. How many are excess?
                var Excess = FinalLength - minimumHashLength;

                int LeftChars, RightChars;

                if (Excess > 0)
                {
                    // How many should we prepend?
                    LeftChars = Alphabet.Length - HalfLength - Excess / 2;
                    // How many should we append?
                    RightChars = minimumHashLength - ResultLength - LeftChars;
                }
                else
                {
                    // No excess, so the alphabet is shorter than our minimum hash length. We'll end up looping and doing this again
                    LeftChars = Alphabet.Length - HalfLength;
                    RightChars = HalfLength;
                }

                // Prepend LeftChars from the end of the alphabet
                if (LeftChars > 0)
                {
                    result.Slice(0, ResultLength).CopyTo(result.Slice(LeftChars));
                    Alphabet.Slice(Alphabet.Length - LeftChars).CopyTo(result);
                    ResultLength += LeftChars;
                }

                // Append RightChars from the start of the alphabet
                if (RightChars > 0)
                {
                    Alphabet.Slice(0, RightChars).CopyTo(result.Slice(ResultLength));
                    ResultLength += RightChars;
                }
            }

            charsWritten = ResultLength;

            return true;
        }

        private bool Hash(Span<char> output, ref int offset, long input, ReadOnlySpan<char> alphabet)
        {
            var StartOffset = offset;

            do
            {
                if (offset == output.Length)
                    return false;

                input = Math.DivRem(input, alphabet.Length, out var Remainder);
                output[offset++] = alphabet[(int)Remainder];
            } while (input > 0);

            // We wrote the hash backwards, so reverse it
            output.Slice(StartOffset, offset - StartOffset).Reverse();

            return true;
        }
    }
}
