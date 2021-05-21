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

            var Before = GC.GetAllocatedBytesForCurrentThread();

            for (var Counter = 1; Counter < 100001; Counter++)
            {
                InBuffer[0] = Counter;
                Service.TryEncode(InBuffer, OutBuffer, out _);
            }

            var After = GC.GetAllocatedBytesForCurrentThread();

            Console.WriteLine($"10 000 encodes: {After - Before}b");
        }
    }
}