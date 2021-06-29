using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Proximity.HashID.Tests.Performance
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var Summary = BenchmarkRunner.Run<HashBenchmark>();


		}

		[MemoryDiagnoser]
		public class HashBenchmark
		{
			private readonly HashIDService _Service;
			private readonly int[] _IntValues = new [] { 12345, 1234567890, int.MaxValue };
			private readonly long[] _LongValues = new [] { 12345, 1234567890123456789, long.MaxValue };
			private readonly string _HexValue = "507f1f77bcf86cd799439011";
			private readonly byte[] _BinaryValue = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE };

			public HashBenchmark()
			{
				_Service = new HashIDService();
			}
			
			[Benchmark]
			public void RoundtripInts()
			{
				var Encoded = _Service.Encode(_IntValues);
				var Decoded = _Service.DecodeInt32(Encoded);
			}

			[Benchmark]
			public void RoundtripIntsNoAlloc()
			{
				Span<char> EncodedBuffer = stackalloc char[128];
				Span<int> DecodedBuffer = stackalloc int[3];

				_Service.TryEncode(_IntValues, EncodedBuffer, out var CharsWritten);
				_Service.TryDecode(EncodedBuffer.Slice(0, CharsWritten), DecodedBuffer, out var NumbersWritten);
			}
			
			[Benchmark]
			public void RoundtripLongs()
			{
				var Encoded = _Service.Encode(_LongValues);
				var Decoded = _Service.DecodeInt64(Encoded);
			}

			[Benchmark]
			public void RoundtripLongsNoAlloc()
			{
				Span<char> EncodedBuffer = stackalloc char[128];
				Span<long> DecodedBuffer = stackalloc long[3];

				_Service.TryEncode(_LongValues, EncodedBuffer, out var CharsWritten);
				_Service.TryDecode(EncodedBuffer.Slice(0, CharsWritten), DecodedBuffer, out var NumbersWritten);
			}
			
			[Benchmark]
			public void RoundtripHex()
			{
				var Encoded = _Service.EncodeHex(_HexValue);
				var Decoded = _Service.DecodeHex(Encoded);
			}

			[Benchmark]
			public void RoundtripHexNoAlloc()
			{
				Span<char> EncodedBuffer = stackalloc char[128];
				Span<char> DecodedBuffer = stackalloc char[_HexValue.Length];

				_Service.TryEncodeHex(_HexValue, EncodedBuffer, out var EncodeChars);
				_Service.TryDecodeHex(EncodedBuffer.Slice(0, EncodeChars), DecodedBuffer, out var NumbersWritten);
			}

			[Benchmark]
			public void RoundtripBinary()
			{
				var Encoded = _Service.Encode(_BinaryValue);
				var Decoded = _Service.DecodeHex(Encoded);
			}

			[Benchmark]
			public void RoundtripBinaryNoAlloc()
			{
				Span<char> EncodedBuffer = stackalloc char[128];
				Span<byte> DecodedBuffer = stackalloc byte[_BinaryValue.Length];

				_Service.TryEncode(_BinaryValue, EncodedBuffer, out var EncodeChars);
				_Service.TryDecodeBinary(EncodedBuffer.Slice(0, EncodeChars), DecodedBuffer, out var NumbersWritten);
			}
			
		}
	}
}
