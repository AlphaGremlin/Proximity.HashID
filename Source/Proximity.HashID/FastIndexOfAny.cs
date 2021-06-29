using System;
using System.Collections.Generic;
using System.Text;

namespace Proximity.HashID
{
	/// <summary>
	/// Provides a cached IndexOfAny utilising a probability map to quickly determine if a character is likely to exist in a pre-determined array
	/// </summary>
	internal readonly struct FastIndexOfAny
	{
		internal const int Length = 8;

		private readonly ReadOnlyMemory<char> _Separators;
		private readonly Memory<uint> _Buffer;

		// Implementation based on https://github.com/dotnet/runtime/blob/91599fff26619cb8bccb7b39c39590e7cd9a4379/src/libraries/System.Private.CoreLib/src/System/String.Searching.cs

		internal FastIndexOfAny(ReadOnlyMemory<char> separators, Memory<uint> buffer)
		{
			if (buffer.Length != Length)
				throw new ArgumentOutOfRangeException(nameof(buffer), "Invalid length");

			var Buffer = buffer.Span;
			var Separators = separators.Span;
			var HasAscii = false;

			for (var Index = 0; Index < Separators.Length; Index++)
			{
				int Character = Separators[Index];

				Buffer[Character & 0x7] |= 1u << ((byte)Character >> 3);

				Character >>= 8;

				if (Character == 0)
					HasAscii = true;
				else
					Buffer[Character & 0x7] |= 1u << (byte)(Character >> 3);
			}

			if (HasAscii)
				Buffer[0] |= 1u;

			_Separators = separators;
			_Buffer = buffer;
		}

		internal int In(ReadOnlySpan<char> source)
		{
			for (var Index = 0; Index < source.Length; Index++)
			{
				var Character = source[Index];

				if (IsSet((byte)Character) && IsSet((byte)(Character >> 8)) && _Separators.Span.IndexOf(Character) != -1)
					return Index;
			}

			return -1;
		}

		private bool IsSet(byte character) => (_Buffer.Span[character & 0x7] & (1u << (character >> 3))) != 0;
	}
}
