using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
    [TestFixture]
    public class SortTests
    {
        [Test]
        public void SortInt()
        {
            var Random = new Random(123456789);
            var Source = new int[100];
            
            for (var Index = 0; Index < Source.Length; Index++)
                Source[Index] = Random.Next();

            var Copy = Source.ToArray();

            Array.Sort(Copy);
            Source.AsSpan().Sort();

            CollectionAssert.AreEqual(Copy, Source);
        }
    }
}