using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

			Span<ulong> Values = stackalloc ulong[Length];

			if (!InternalDecode(hash, Values, out var ValuesWritten))
				return Array.Empty<int>();

			if (ValuesWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			var Results = new int[Length];

			for (var Index = 0; Index < Length; Index++)
			{
				var Value = (long)Values[Index];

				if (Value > int.MaxValue || Value < int.MinValue)
					return Array.Empty<int>();

				Results[Index] = (int)Value;
			}

			return Results;
		}

		public int[] DecodeUInt32(string hash) => DecodeUInt32(hash.AsSpan());

		public int[] DecodeUInt32(ReadOnlySpan<char> hash)
		{
			var Length = MeasureDecode(hash);

			if (Length == 0)
				return Array.Empty<int>();

			Span<ulong> Values = stackalloc ulong[Length];

			if (!InternalDecode(hash, Values, out var ValuesWritten))
				return Array.Empty<int>();

			if (ValuesWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			var Results = new int[Length];

			for (var Index = 0; Index < Length; Index++)
			{
				var Value = (long)Values[Index];

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

			if (!InternalDecode(hash, MemoryMarshal.Cast<long, ulong>(Results), out var ValuesWritten))
				return Array.Empty<long>();

			if (ValuesWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			return Results;
		}

		public ulong[] DecodeUInt64(string hash) => DecodeUInt64(hash.AsSpan());

		public ulong[] DecodeUInt64(ReadOnlySpan<char> hash)
		{
			var Length = MeasureDecode(hash);

			if (Length == 0)
				return Array.Empty<ulong>();

			var Results = new ulong[Length];

			if (!InternalDecode(hash, Results, out var ValuesWritten))
				return Array.Empty<ulong>();

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

		public uint DecodeSingleUInt32(string hash) => DecodeSingleUInt32(hash.AsSpan());

		public uint DecodeSingleUInt32(ReadOnlySpan<char> hash)
		{
			if (!TryDecodeSingleUInt32(hash, out var Value))
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

		public ulong DecodeSingleUInt64(string hash) => DecodeSingleUInt64(hash.AsSpan());

		public ulong DecodeSingleUInt64(ReadOnlySpan<char> hash)
		{
			if (!TryDecodeSingleUInt64(hash, out var Value))
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

			for (; ; )
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

			for (; ; )
			{
				Values += 12; // Measure returns the worst-case

				var NextIndex = hash.IndexOfAny(separators.Span);

				if (NextIndex == -1)
					break;

				hash = hash.Slice(NextIndex + 1);
			}

			return Values;
		}

		public bool TryDecodeBinary(ReadOnlySpan<char> hash, out byte[] binary)
		{
			var Length = MeasureDecode(hash);

			if (Length == 0)
			{
				binary = Array.Empty<byte>();

				return true;
			}

			Span<byte> Result = stackalloc byte[Length * 8];

			if (!TryDecodeBinary(hash, Result, out var BytesWritten))
			{
				binary = Array.Empty<byte>();

				return false;
			}

			binary = Result.Slice(0, BytesWritten).ToArray();

			return true;
		}

		public bool TryDecodeBinary(ReadOnlySpan<char> hash, Span<byte> bytes, out int bytesWritten)
		{
			if (hash.IsEmpty)
			{
				bytesWritten = 0;

				return true;
			}

			var Length = MeasureDecode(hash);
			Span<ulong> Values = stackalloc ulong[Length];

			// First decode the hash into its component longs
			if (!InternalDecode(hash, Values, out var ValuesWritten))
			{
				bytesWritten = 0;

				return false;
			}

			if (ValuesWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			bytesWritten = 0;

			for (var Index = 0; Index < Values.Length - 1; Index++)
			{
				var Value = Values[Index];

				if (bytes.Length < 8)
				{
					// We need more space to write this segment
					bytesWritten = 0;

					return false;
				}

				// Read the full long directly into our output
				BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);

				bytes = bytes.Slice(8);
				bytesWritten += 8;
			}

			// Handle the final value differently
			{
				var Value = Values[Values.Length - 1];

				// Find the highest bit set in the value
#if NETSTANDARD
				var LeadingBits = LeadingZeroCount((ulong)Value);
#else
				var LeadingBits = System.Numerics.BitOperations.LeadingZeroCount((ulong)Value);
#endif

				if (LeadingBits == 64)
				{
					// The final value cannot be zero
					bytesWritten = 0;

					return false;
				}

				// Encoding sets the highest bit, so unset it
				Value &= ~(1UL << (63 - LeadingBits));

				//  Calculate how many bytes are left. Partial bytes count as full bytes
				var UsedBytes = Math.DivRem(63 - LeadingBits, 8, out var Remainder);

				if (Remainder > 0)
					UsedBytes++;

				// If the final value isn't used for data, we can exit early
				if (UsedBytes == 0)
					return true;

				if (bytes.Length >= UsedBytes)
				{
					if (UsedBytes == 8)
					{
						// Read the value in directly
						BinaryPrimitives.WriteUInt64BigEndian(bytes, Value);
					}
					else
					{
						Span<byte> TempSegment = stackalloc byte[8];

						// Read the value into a temp buffer and copy what we need
						BinaryPrimitives.WriteUInt64BigEndian(TempSegment, Value);

						TempSegment.Slice(8 - UsedBytes).CopyTo(bytes);
					}

					bytesWritten += UsedBytes;
				}
				else
				{
					// Not enough remaining space in the buffer
					bytesWritten = 0;

					return false;
				}
			}

			return true;
		}

		public bool TryDecodeHex(string hash, out string hexString) => TryDecodeHex(hash.AsSpan(), out hexString);

		public bool TryDecodeHex(ReadOnlySpan<char> hash, out string hexString)
		{
			if (hash.IsEmpty)
			{
				hexString = string.Empty;

				return true;
			}

			var Length = MeasureDecodeHex(hash);

			Span<char> Result = stackalloc char[Length];

			if (!TryDecodeHex(hash, Result, out var CharsWritten))
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
			Span<ulong> Values = stackalloc ulong[Length];

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
			if (hash.IsEmpty)
			{
				value = 0;

				return true;
			}

			Span<ulong> Values = stackalloc ulong[1];

			if (!InternalDecode(hash, Values, out var Written))
			{
				value = default;
				return false;
			}

			value = unchecked((int)Values[0]);

			return true;
		}

		public bool TryDecodeSingleUInt32(string hash, out uint value) => TryDecodeSingleUInt32(hash.AsSpan(), out value);

		public bool TryDecodeSingleUInt32(ReadOnlySpan<char> hash, out uint value)
		{
			if (TryDecodeSingleUInt64(hash, out var Value) && Value <= uint.MaxValue)
			{
				value = (uint)Value;

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

			Span<ulong> Values = stackalloc ulong[1];

			if (!InternalDecode(hash, Values, out var Written))
			{
				value = default;
				return false;
			}

			value = unchecked((long)Values[0]);

			return true;
		}

		public bool TryDecodeSingleUInt64(string hash, out ulong value) => TryDecodeSingleUInt64(hash.AsSpan(), out value);

		public bool TryDecodeSingleUInt64(ReadOnlySpan<char> hash, out ulong value)
		{
			if (hash.IsEmpty)
			{
				value = 0;

				return true;
			}

			Span<ulong> Values = stackalloc ulong[1];

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

			if (!InternalDecode(hash, MemoryMarshal.Cast<long, ulong>(Results), out var NumbersWritten))
			{
				numbers = null!;

				return false;
			}

			if (NumbersWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			numbers = Results;

			return true;
		}

		public bool TryDecode(ReadOnlySpan<char> hash, Span<int> numbers, out int numbersWritten) => TryDecode(hash, MemoryMarshal.Cast<int, uint>(numbers), out numbersWritten);

		public bool TryDecode(ReadOnlySpan<char> hash, Span<uint> numbers, out int numbersWritten)
		{
			numbersWritten = 0;

			var Length = MeasureDecode(hash);

			if (Length == 0)
				return true;

			Span<ulong> Values = stackalloc ulong[Length];

			if (!InternalDecode(hash, Values, out var ValuesWritten))
				return false;

			if (ValuesWritten != Length)
				throw new InvalidOperationException("Did not measure correctly");

			var Results = new int[Length];

			for (var Index = 0; Index < Length; Index++)
			{
				var Value = Values[Index];

				if (Value > uint.MaxValue || Value < uint.MinValue)
					return false;

				Results[Index] = (int)Value;
			}

			return true;
		}

		public bool TryDecode(ReadOnlySpan<char> hash, Span<long> numbers, out int numbersWritten) => TryDecode(hash, MemoryMarshal.Cast<long, ulong>(numbers), out numbersWritten);

		public bool TryDecode(ReadOnlySpan<char> hash, Span<ulong> numbers, out int numbersWritten)
		{
			if (hash.IsEmpty)
			{
				numbersWritten = 0;

				return true;
			}

			return InternalDecode(hash, numbers, out numbersWritten);
		}

		private bool InternalDecode(ReadOnlySpan<char> hash, Span<ulong> numbers, out int numbersWritten)
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

			for (; ; )
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

		private bool TryUnhash(ReadOnlySpan<char> input, Span<char> alphabet, out ulong number)
		{
			ulong Number = 0;

			foreach (var Char in input)
			{
				var Position = alphabet.IndexOf(Char);

				// Is this character within the alphabet?
				if (Position == -1)
				{
					number = 0;
					return false;
				}

				Number = Number * (ulong)alphabet.Length + (ulong)Position;
			}

			number = Number;

			return true;
		}
	}
}
