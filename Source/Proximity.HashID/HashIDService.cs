using System;
using System.Buffers;
using System.Globalization;
using System.Linq;

namespace Proximity.HashID
{
	/// <summary>
	/// Provides Hash ID encoding and decoding
	/// </summary>
	public partial class HashIDService : IHashIDService, IDisposable
	{
		private const string DefaultStringAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
		private const string DefaultAlphabet =       "abdegjklmnopqrvwxyzABDEGJKLMNOPQRVWXYZ1234567890";
		private const string DefaultSeparators = "cfhistuCFHISTU";
		private const int GuardDivisor = 12;
		private const int MinimumAlphabetLength = 4;
		private const int MaxStackBytes = 1024;

		private readonly ReadOnlyMemory<char> salt, alphabet, separators, guards;
		private readonly char[] backingBuffer;
		private readonly uint[] probabilityBuffer;
		private readonly int minimumHashLength, maximumSegmentLength, maximumHexSegmentLength;
		private readonly FastIndexOfAny separatorMap, guardMap;

		/// <summary>
		/// Creates a new unsalted HashID service
		/// </summary>
		public HashIDService() : this(ReadOnlySpan<char>.Empty)
		{
		}

		/// <summary>Creates a new HashID service</summary>
		/// <param name="salt">The salt to use for HashID encoding and decoding</param>
		/// <param name="alphabet">The alphabet to use for HashID values.</param>
		/// <param name="separators">The separators to use for HashID values. Characters must be included in the <paramref name="alphabet" />.</param>
		/// <param name="minimumHashLength">The minimum length of generated hashses. Shorter values will be padded accordingly.</param>
		/// <remarks>This constructor is designed to mimic HashIds.Net and will perform allocations. For zero-allocations, use the ReadOnlySpan-based constructor.</remarks>
		public HashIDService(string? salt, string? alphabet = DefaultAlphabet, string? separators = DefaultSeparators, int minimumHashLength = 0) : this(
			salt.AsSpan(),
			(alphabet ?? DefaultAlphabet).Distinct().Except(separators ?? DefaultSeparators).ToArray(),
			(separators ?? DefaultSeparators).Intersect(alphabet ?? DefaultAlphabet).ToArray(),
			minimumHashLength
			)
		{
			// Hashids.Net uses the following rules for alphabet and separators:
			// Final Alphabet = Alphabet Except Separators
			// Final Separators = Separators Intersect Alphabet
		}

		/// <summary>Creates a new HashID service</summary>
		/// <param name="salt">The salt to use for HashID encoding and decoding</param>
		/// <param name="alphabet">The alphabet to use for HashID values. Characters must be unique</param>
		/// <param name="separators">The separators to use for HashID values. Characters must be unique and not included in <paramref name="alphabet" /></param>
		/// <param name="minimumHashLength">The minimum length of generated hashses. Shorter values will be padded accordingly.</param>
		/// <remarks>To support a zero-allocation constructor, we impose some constraints on the alphabet and separators. For a more flexible constructor, use the string-based constructor.</remarks>
		public HashIDService(ReadOnlySpan<char> salt, ReadOnlySpan<char> alphabet = default, ReadOnlySpan<char> separators = default, int minimumHashLength = 0)
		{
			if (alphabet.IsEmpty)
			{
				alphabet = DefaultAlphabet.AsSpan();

				if (separators.IsEmpty)
					separators = DefaultSeparators.AsSpan();
			}

			if (alphabet.Length < MinimumAlphabetLength)
				throw new ArgumentOutOfRangeException(nameof(alphabet), $"Must have at least {MinimumAlphabetLength} characters in the alphabet");

			var Valid = false;

			try
			{
				// Copy the input values to a buffer we can manipulate and keep for later.
				backingBuffer = ArrayPool<char>.Shared.Rent(salt.Length + alphabet.Length + separators.Length);
				probabilityBuffer = ArrayPool<uint>.Shared.Rent(FastIndexOfAny.Length * 2);

				// Separators go first, so we can append values from the start of the alphabet. Salt goes at the end.
				var Separators = backingBuffer.AsMemory(0, separators.Length);
				var Alphabet = backingBuffer.AsMemory(separators.Length, alphabet.Length);
				var Salt = backingBuffer.AsMemory(alphabet.Length + separators.Length, salt.Length);

				alphabet.CopyTo(Alphabet.Span);
				separators.CopyTo(Separators.Span);
				salt.CopyTo(Salt.Span);

				// Ensure there are no duplicate characters in the alphabet, by sorting and then searching for adjacent copies.
				// We require the supplied alphabet be distinct here, which avoids the allocations for LINQ or HashSet<char>
				Alphabet.Span.Sort();

				for (var Index = 1; Index < Alphabet.Span.Length; Index++)
				{
					if (Alphabet.Span[Index - 1] == Alphabet.Span[Index])
						throw new ArgumentException("Alphabet has duplicate characters", nameof(alphabet));
				}

				// Ensure there are no duplicate characters in the separators
				// We require the supplied separators to be distinct and exclusive of the alphabet.
				Separators.Span.Sort();

				for (var Index = 1; Index < Separators.Span.Length; Index++)
				{
					if (Separators.Span[Index - 1] == Separators.Span[Index])
						throw new ArgumentException("Separators have duplicate characters", nameof(separators));
				}

				// Ensure the separator characters are not in the alphabet
				foreach (var Char in Separators.Span)
				{
					if (Alphabet.Span.BinarySearch(Char) >= 0)
						throw new ArgumentException("Separators include alphabet characters", nameof(separators));
				}

				// Reset the ordering back to the original for the shuffle operations
				alphabet.CopyTo(Alphabet.Span);
				separators.CopyTo(Separators.Span);

				// Shuffle the various characters based on the salt
				ConsistentShuffle(Separators.Span, salt);

				// We want to ensure a ratio of at most 3.5 alphabet characters per separator
				// Avoid floating point and use integer math to calculate the result
				if (Alphabet.Length * 2 > Separators.Length * 7)
				{
					var TargetLength = Math.DivRem(Alphabet.Length * 2, 7, out var AlphabetRemainder);

					if (AlphabetRemainder > 0)
						TargetLength++;

					// Shift some characters over from the alphabet to the separators
					// Since this is done between shuffling separators and alphabet, we actually append unshuffled the alphabet to the shuffled separators.
					// Seems weird, but we want to maintain compatibility with Hashids.Net
					Separators = backingBuffer.AsMemory(0, TargetLength);
					Alphabet = backingBuffer.AsMemory(TargetLength, alphabet.Length + separators.Length - TargetLength);
				}

				ConsistentShuffle(Alphabet.Span, salt);

				// Select a subset of characters to use as guards
				var GuardCount = Math.DivRem(Alphabet.Length, GuardDivisor, out var GuardLeftover);

				if (GuardLeftover > 0)
					GuardCount++;

				Memory<char> Guards;

				if (Alphabet.Length < 3)
				{
					Guards = Separators.Slice(0, GuardCount);
					Separators = Separators.Slice(GuardCount);
				}
				else
				{
					Guards = Alphabet.Slice(0, GuardCount);
					Alphabet = Alphabet.Slice(GuardCount);
				}

				// Calculate our IndexOfAny tables for fast lookups
				separatorMap = new FastIndexOfAny(Separators, probabilityBuffer.AsMemory(0, FastIndexOfAny.Length));
				guardMap = new FastIndexOfAny(Guards, probabilityBuffer.AsMemory(FastIndexOfAny.Length, FastIndexOfAny.Length));

				// Ready to go
				this.salt = Salt;
				this.alphabet = Alphabet;
				this.separators = Separators;
				this.guards = Guards;
				this.minimumHashLength = minimumHashLength;
				this.maximumSegmentLength = MeasureHash(ulong.MaxValue, (uint)Alphabet.Length);
				this.maximumHexSegmentLength = MeasureHash(0x1FFFFFFFFFFFF, (uint)Alphabet.Length);

				//Console.WriteLine("Salt: {0}", this.salt.ToString());
				//Console.WriteLine("Alphabet: {0}", this.alphabet.ToString());
				//Console.WriteLine("Separators: {0}", this.separators.ToString());
				//Console.WriteLine("Guards: {0}", this.guards.ToString());

				Valid = true;
			}
			finally
			{
				if (!Valid)
				{
					if (backingBuffer != null)
						ArrayPool<char>.Shared.Return(backingBuffer);

					if (probabilityBuffer != null)
						ArrayPool<uint>.Shared.Return(probabilityBuffer);
				}
			}
		}

		/// <summary>Releases any rented buffers back to the shared ArrayPool</summary>
		public void Dispose()
		{
			ArrayPool<char>.Shared.Return(backingBuffer);
			ArrayPool<uint>.Shared.Return(probabilityBuffer);
		}

		/// <summary>
		/// Gets the complete alphabet of characters being used
		/// </summary>
		public ReadOnlyMemory<char> Alphabet => backingBuffer.AsMemory(0, salt.Length + alphabet.Length + separators.Length + guards.Length);

		/// <summary>
		/// Gets the alphabet being used for encoding values
		/// </summary>
		public ReadOnlyMemory<char> EncodingAlphabet => alphabet;

		/// <summary>
		/// Gets the alphabet being used for separating values
		/// </summary>
		public ReadOnlyMemory<char> SeparatorAlphabet => separators;

		/// <summary>
		/// Gets/Sets whether to verify decoded values. Defaults to True
		/// </summary>
		/// <remarks>Disable to improve decode performance at the cost of potentially incorrect HashIDs being successfully decoded</remarks>
		public bool VerifyOnDecode { get; set; } = true;

		private void ConsistentShuffle(Span<char> target, ReadOnlySpan<char> salt)
		{
			if (salt.IsEmpty)
				return;

			for (int Index = target.Length - 1, ShuffleSource = 0, Accumulator = 0; Index > 0; Index--, ShuffleSource++)
			{
				ShuffleSource %= salt.Length;
				int NextChar = salt[ShuffleSource];
				Accumulator += NextChar;
				var ShuffleTarget = (NextChar + ShuffleSource + Accumulator) % Index;

				(target[Index], target[ShuffleTarget]) = (target[ShuffleTarget], target[Index]);
			}
		}

		private int MeasureHash(ulong input, uint alphabetLength)
		{
			var Length = 0;

			do
			{
				Length++;
				input /= alphabetLength;
			}
			while (input > 0);

			return Length;
		}

#if NETSTANDARD
		private static readonly int[] LeadingZeroes = {
			 0, 63,  5, 62,  4, 16, 10, 61,
			 3, 24, 15, 36,  9, 30, 21, 60,
			 2, 12, 26, 23, 14, 45, 35, 43,
			 8, 33, 29, 52, 20, 49, 41, 59,
			 1,  6, 17, 11, 25, 37, 31, 22,
			13, 27, 46, 44, 34, 53, 50, 42,
			 7, 18, 38, 32, 28, 47, 54, 51,
			19, 39, 48, 55, 40, 56, 57, 58
		};

		private static int LeadingZeroCount(ulong value)
		{
			if (value == 0)
				return 64;

			value |= value >> 1;
			value |= value >> 2;
			value |= value >> 4;
			value |= value >> 8;
			value |= value >> 16;
			value |= value >> 32;

			return LeadingZeroes[((value - (value >> 1)) * 0x07EDD5E59A4E28C2) >> 58];
		}
#endif
	}
}
