using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class PerformanceTests
	{
		[Test]
		public void EncodeSingle()
		{
			using var Service = new HashIDService();

			Service.Encode(1); // Pre-JIT

			var Timer = new Stopwatch();

			Timer.Start();
			Timer.Stop();
			Timer.Reset();

			var Before = GC.GetAllocatedBytesForCurrentThread();

			Timer.Start();

			for (var Counter = 1; Counter < 100001; Counter++)
			{
				Service.Encode(Counter);
			}

			Timer.Stop();

			var After = GC.GetAllocatedBytesForCurrentThread();

			Console.WriteLine($"10 000 encodes: {Timer.ElapsedMilliseconds}ms, {After - Before}b");
		}

		[Test]
		public void EncodeZeroAlloc()
		{
			using var Service = new HashIDService();

			var InBuffer = new long[1];
			var OutBuffer = new char[1024];

			Service.TryEncode(InBuffer, OutBuffer, out _); // Pre-JIT

			var Before = GC.GetAllocatedBytesForCurrentThread();

			for (var Counter = 1; Counter < 100001; Counter++)
			{
				InBuffer[0] = Counter;
				Service.TryEncode(InBuffer, OutBuffer, out _);
			}

			var After = GC.GetAllocatedBytesForCurrentThread();

			Console.WriteLine($"10 000 encodes: {After - Before}b");
		}

		[Test]
		public void DecodeZeroAlloc()
		{
			using var Service = new HashIDService();

			var InBuffer = new char[6];
			var OutBuffer = new long[1];
			var Seed = Environment.TickCount;
			var Random = new Random(Seed);
			var Alphabet = Service.EncodingAlphabet.Span;

			Console.WriteLine("Seed: {0}", Seed);

			"5O8yp5".AsSpan().CopyTo(InBuffer);

			Service.TryDecode(InBuffer, OutBuffer, out _); // Pre-JIT

			var Before = GC.GetAllocatedBytesForCurrentThread();

			for (var Counter = 1; Counter < 100001; Counter++)
			{
				// Generate a random HashID
				for (var Index = 0; Index < 6; Index++)
					InBuffer[Index] = Alphabet[Random.Next(Alphabet.Length)];

				Service.TryDecode(InBuffer, OutBuffer, out _);
			}

			var After = GC.GetAllocatedBytesForCurrentThread();

			Console.WriteLine($"10 000 decodes: {After - Before}b");
		}
	}
}
