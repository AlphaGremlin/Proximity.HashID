using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Proximity.HashID
{
	public partial class HashIDService
	{
		public string Encode(int number)
		{
			Span<ulong> Numbers = stackalloc ulong[] { unchecked((uint)number) };

			return Encode(Numbers);
		}

		public string Encode(uint number)
		{
			Span<ulong> Numbers = stackalloc ulong[] { number };

			return Encode(Numbers);
		}

		public string Encode(ReadOnlySpan<int> numbers) => Encode(MemoryMarshal.Cast<int, uint>(numbers));

		public string Encode(ReadOnlySpan<uint> numbers)
		{
			// If there's no numbers, we don't write anything
			if (numbers.IsEmpty)
				return string.Empty;

			var MaximumLength = MeasureEncode(numbers.Length);

			var InBuffer = ArrayPool<ulong>.Shared.Rent(numbers.Length);
			var OutBuffer = ArrayPool<char>.Shared.Rent(MaximumLength);

			try
			{
				for (var Index = 0; Index < numbers.Length; Index++)
					InBuffer[Index] = numbers[Index];

				if (!InternalEncode(InBuffer.AsSpan(0, numbers.Length), OutBuffer, out var ActualLength))
					throw new InvalidOperationException("Did not measure correctly");

				return OutBuffer.AsSpan(0, ActualLength).ToString();
			}
			finally
			{
				ArrayPool<ulong>.Shared.Return(InBuffer);
				ArrayPool<char>.Shared.Return(OutBuffer);
			}
		}

		public string Encode(long number)
		{
			Span<ulong> Numbers = stackalloc ulong[] { unchecked((ulong)number) };

			return Encode(Numbers);
		}

		public string Encode(ulong number)
		{
			Span<ulong> Numbers = stackalloc ulong[] { number };

			return Encode(Numbers);
		}

		public string Encode(params long[] numbers) => Encode(numbers.AsSpan());

		public string Encode(ReadOnlySpan<long> numbers) => Encode(MemoryMarshal.Cast<long, ulong>(numbers));

		public string Encode(ReadOnlySpan<ulong> numbers)
		{
			// If there's no numbers, we don't write anything
			if (numbers.IsEmpty)
				return string.Empty;

			var MaximumLength = MeasureEncode(numbers.Length);

			var OutBuffer = ArrayPool<char>.Shared.Rent(MaximumLength);

			try
			{
				if (!InternalEncode(numbers, OutBuffer, out var ActualLength))
					throw new InvalidOperationException("Did not measure correctly");

				return OutBuffer.AsSpan(0, ActualLength).ToString();
			}
			finally
			{
				ArrayPool<char>.Shared.Return(OutBuffer);
			}
		}

		public string Encode(ReadOnlySpan<byte> bytes)
		{
			// If there's no bytes, we don't write anything
			if (bytes.IsEmpty)
				return string.Empty;

			var MaximumLength = MeasureEncode((bytes.Length / 8) + 1);

			var OutBuffer = ArrayPool<char>.Shared.Rent(MaximumLength);

			try
			{
				if (!TryEncode(bytes, OutBuffer, out var ActualLength))
					throw new InvalidOperationException("Did not measure correctly");

				return OutBuffer.AsSpan(0, ActualLength).ToString();
			}
			finally
			{
				ArrayPool<char>.Shared.Return(OutBuffer);
			}
		}

		public string EncodeHex(string hexString) => EncodeHex(hexString.AsSpan());

		public string EncodeHex(ReadOnlySpan<char> hexString)
		{
			if (hexString.IsEmpty)
				return string.Empty;

			var MaximumLength = MeasureEncodeHex(hexString.Length);

			var OutBuffer = ArrayPool<char>.Shared.Rent(MaximumLength);

			try
			{
				if (!TryEncodeHex(hexString, OutBuffer, out var ActualLength))
					return string.Empty;

				return OutBuffer.AsSpan(0, ActualLength).ToString();
			}
			finally
			{
				ArrayPool<char>.Shared.Return(OutBuffer);
			}
		}

		public int MeasureEncode(int numbers)
		{
			if (numbers < 0)
				throw new ArgumentOutOfRangeException(nameof(numbers));

			if (numbers == 0)
				return 0;

			var Length = 1; // Lottery

			// The maximum size of each value
			Length += maximumSegmentLength * numbers;

			// Number of separator characters
			Length += numbers - 1;

			// Minimum hash length
			return Math.Max(Length, minimumHashLength);
		}

		public int MeasureEncode(int[] numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(uint[] numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(long[] numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(ulong[] numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(ReadOnlySpan<int> numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(ReadOnlySpan<uint> numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(ReadOnlySpan<long> numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncode(ReadOnlySpan<ulong> numbers) => MeasureEncode(numbers.Length);

		public int MeasureEncodeHex(int hexStringLength)
		{
			if (hexStringLength < 0)
				throw new ArgumentOutOfRangeException(nameof(hexStringLength));

			if (hexStringLength == 0)
				return 0;

			var Length = 1; // Lottery
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

			var HexBlocks = ((hexString.Length - 1) / 12) + 1;
			var InBuffer = ArrayPool<ulong>.Shared.Rent(HexBlocks);
			var Index = 0;

			try
			{
				do
				{
					// We only use 12 out of the possible 15 (+1 for padding) hex characters that fit in a long
					var BlockSize = Math.Min(hexString.Length, 12);

#if NETSTANDARD2_0
					// TODO: Write our own span-based hex number parser
					if (!ulong.TryParse(hexString.Slice(0, BlockSize).ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var BlockValue))
#else
					if (!ulong.TryParse(hexString.Slice(0, BlockSize), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var BlockValue))
#endif
					{
						charsWritten = 0;

						return false;
					}

					// Hex values need to be prepended with 1. Since we're parsing in-place, we need to do a little bit-shifting to stick the 1 at the correct spot
					InBuffer[Index++] = BlockValue | (1UL << (4 * BlockSize));

					// Are there any more hex characters to include?
					hexString = hexString.Slice(BlockSize);
				}
				while (!hexString.IsEmpty);

				return InternalEncode(InBuffer.AsSpan(0, HexBlocks), hash, out charsWritten);
			}
			finally
			{
				ArrayPool<ulong>.Shared.Return(InBuffer);
			}
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

			var InBuffer = ArrayPool<ulong>.Shared.Rent(HexBlocks);

			try
			{
				Span<byte> TempBuffer = stackalloc byte[8];

				while (bytes.Length > 8)
				{
					// We use all 8 bytes that fit in a long
					InBuffer[Index++] = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));

					bytes = bytes.Slice(8);
				}

				// Read in the final number
				var BlockSize = bytes.Length;
				var BlockLead = 8 - BlockSize;

				bytes.Slice(0, BlockSize).CopyTo(TempBuffer.Slice(BlockLead));

				if (BlockLead > 0)
					TempBuffer.Slice(0, BlockLead).Clear();

				var FinalValue = BinaryPrimitives.ReadUInt64BigEndian(TempBuffer);

				// Find the highest bit set in the value
#if NETSTANDARD
				var LeadingBits = LeadingZeroCount(FinalValue);
#else
				var LeadingBits = System.Numerics.BitOperations.LeadingZeroCount((ulong)FinalValue);
#endif

				if (LeadingBits == 0)
				{
					InBuffer[Index++] = FinalValue;

					// We're a multiple of eight with the high bit set, so we have a final number set to one.
					InBuffer[Index++] = 1;
				}
				else
				{
					// We only want to set the top bit in the last byte
					FinalValue |= 1UL << Math.Max((BlockSize - 1) * 8 + 1, 64 - LeadingBits);

					InBuffer[Index++] = FinalValue;
				}

				return InternalEncode(InBuffer.AsSpan(0, Index), hash, out charsWritten);

			}
			finally
			{
				ArrayPool<ulong>.Shared.Return(InBuffer);
			}
		}

		public bool TryEncode(long[] numbers, out string hash) => TryEncode(MemoryMarshal.Cast<long, ulong>(numbers.AsSpan()), out hash);

		public bool TryEncode(ulong[] numbers, out string hash) => TryEncode(numbers.AsSpan(), out hash);

		public bool TryEncode(ReadOnlySpan<long> numbers, out string hash) => TryEncode(MemoryMarshal.Cast<long, ulong>(numbers), out hash);

		public bool TryEncode(ReadOnlySpan<ulong> numbers, out string hash)
		{
			if (numbers.IsEmpty)
			{
				hash = string.Empty;

				return true;
			}

			var MaximumLength = MeasureEncode(numbers.Length);

			var OutBuffer = ArrayPool<char>.Shared.Rent(MaximumLength);

			try
			{
				if (!InternalEncode(numbers, OutBuffer, out var ActualLength))
				throw new InvalidOperationException("Did not measure correctly");

				hash = OutBuffer.AsSpan(0, ActualLength).ToString();

				return true;
			}
			finally
			{
				ArrayPool<char>.Shared.Return(OutBuffer);
			}
		}

		public bool TryEncode(int number, Span<char> result, out int charsWritten) => TryEncode(unchecked((ulong)(uint)number), result, out charsWritten);

		public bool TryEncode(uint number, Span<char> result, out int charsWritten) => TryEncode((ulong)number, result, out charsWritten);

		public bool TryEncode(long number, Span<char> result, out int charsWritten) => TryEncode(unchecked((ulong)number), result, out charsWritten);

		public bool TryEncode(ulong number, Span<char> result, out int charsWritten)
		{
			Span<ulong> Numbers = stackalloc ulong[] { number };

			return InternalEncode(Numbers, result, out charsWritten);
		}

		public bool TryEncode(ReadOnlySpan<int> numbers, Span<char> result, out int charsWritten) => TryEncode(MemoryMarshal.Cast<int, uint>(numbers), result, out charsWritten);

		public bool TryEncode(ReadOnlySpan<uint> numbers, Span<char> result, out int charsWritten)
		{
			// If there's no numbers, we don't write anything
			if (numbers.IsEmpty)
			{
				charsWritten = 0;

				return true;
			}

			var InBuffer = ArrayPool<ulong>.Shared.Rent(numbers.Length);

			try
			{
				for (var Index = 0; Index < numbers.Length; Index++)
					InBuffer[Index] = numbers[Index];

				return InternalEncode(InBuffer.AsSpan(0, numbers.Length), result, out charsWritten);
			}
			finally
			{
				ArrayPool<ulong>.Shared.Return(InBuffer);
			}
		}

		public bool TryEncode(ReadOnlySpan<long> numbers, Span<char> result, out int charsWritten) => TryEncode(MemoryMarshal.Cast<long, ulong>(numbers), result, out charsWritten);

		public bool TryEncode(ReadOnlySpan<ulong> numbers, Span<char> result, out int charsWritten)
		{
			// If there's no numbers, we don't write anything
			if (numbers.IsEmpty)
			{
				charsWritten = 0;

				return true;
			}

			return InternalEncode(numbers, result, out charsWritten);
		}

		private bool InternalEncode(ReadOnlySpan<ulong> numbers, Span<char> result, out int charsWritten)
		{
			charsWritten = 0;

			// Ensure there's space for the lottery and minimum size
			if (result.IsEmpty || result.Length < minimumHashLength)
				return false;

			var ResultLength = 0;

			// Generate a hash to use for the rest of the calculation
			var NumbersHash = 0;

			for (var Index = 0; Index < numbers.Length; Index++)
				NumbersHash += (int)(numbers[Index] % (ulong)(Index + 100));

			// Copy the alphabet to a buffer we can shuffle around
			var AlphabetBuffer = ArrayPool<char>.Shared.Rent(alphabet.Length);
			var ShuffleBuffer = ArrayPool<char>.Shared.Rent(alphabet.Length);

			try
			{
				var Alphabet = AlphabetBuffer.AsSpan(0, alphabet.Length);
				var ShuffleOffset = salt.Length + 1;
				var SubAlphabet = (ShuffleOffset < Alphabet.Length) ? Alphabet.Slice(0, Alphabet.Length - ShuffleOffset) : default;
				var Shuffle = ShuffleBuffer.AsSpan(0, alphabet.Length);
				var SubShuffle = Shuffle.Slice(ShuffleOffset);

				alphabet.Span.CopyTo(Alphabet);

				var Lottery = Alphabet[NumbersHash % Alphabet.Length];
				result[ResultLength++] = Lottery;

				// Generate the buffer we use to shuffle the alphabet with
				// Each iteration, we replace the end of it with the current alphabet
				// We only need it to be the same length as the Alphabet though
				Shuffle[0] = Lottery;
				salt.Span.Slice(0, Math.Min(Alphabet.Length - 1, salt.Length)).CopyTo(Shuffle.Slice(1));

				for (var Index = 0; Index < numbers.Length; Index++)
				{
					var Number = numbers[Index];

					// Fill the remainder of the shuffle buffer with the current alphabet
					SubAlphabet.CopyTo(SubShuffle);
					ConsistentShuffle(Alphabet, Shuffle);

					var StartOffset = ResultLength;

					if (!Hash(result, ref ResultLength, Number, Alphabet))
						return false;

					// If we're not the last item, add a separator
					if (Index + 1 < numbers.Length)
					{
						if (result.Length == ResultLength)
							return false;

						var SeparatorIndex = (int)(Number % (ulong)(result[StartOffset] + Index)) % separators.Length;

						result[ResultLength++] = separators.Span[SeparatorIndex];
					}
				}

				if (ResultLength < minimumHashLength)
				{
					if (result.Length == ResultLength)
						return false;

					var GuardIndex = (NumbersHash + result[0]) % guards.Length;
					var Guard = guards.Span[GuardIndex];

					// Move the result up, since we need to prepend a guard value
					result.Slice(0, ResultLength).CopyTo(result.Slice(1));
					result[0] = Guard;
					ResultLength++;

					if (ResultLength < minimumHashLength)
					{
						if (result.Length == ResultLength)
							return false;

						GuardIndex = (NumbersHash + result[2]) % guards.Length;
						Guard = guards.Span[GuardIndex];

						result[ResultLength++] = Guard;
					}
				}

				var HalfLength = Alphabet.Length / 2;

				// No need for bounds checks here, since we know we have Minimum Hash Length in the output buffer
				while (ResultLength < minimumHashLength)
				{
					// We're doing an in-place shuffle, so we need to copy the Alphabet first
					Alphabet.CopyTo(Shuffle);
					ConsistentShuffle(Alphabet, Shuffle);

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
			finally
			{
				ArrayPool<char>.Shared.Return(ShuffleBuffer);
				ArrayPool<char>.Shared.Return(AlphabetBuffer);
			}
		}

		private bool Hash(Span<char> output, ref int offset, ulong input, ReadOnlySpan<char> alphabet)
		{
			var StartOffset = offset;
			var Length = (ulong)alphabet.Length;

			do
			{
				if (offset == output.Length)
					return false;

				// TODO: Use the ulong DivRem override when available in .Net 6.0
				// input = Math.DivRem(input, Length, out var Remainder);
				//output[offset++] = alphabet[(int)Remainder];
				output[offset++] = alphabet[(int)(input % Length)];
				input /= Length;
			} while (input > 0);

			// We wrote the hash backwards, so reverse it
			output.Slice(StartOffset, offset - StartOffset).Reverse();

			return true;
		}
	}
}
