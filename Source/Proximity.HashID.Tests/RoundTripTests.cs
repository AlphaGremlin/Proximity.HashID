using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
    [TestFixture]
    public class RoundTripTests : TestsBase
    {
        [TestCase(new [] { 0, 1, 2})]
        [TestCase(new [] { 1, 2, 0})]
        public void EncodeDecodeZeros(int[] values)
        {
            using var Service = new HashIDService(salt);
            
            CollectionAssert.AreEqual(values, Service.DecodeInt32(Service.Encode(values)));
        }

        [TestCase(new byte [] { 0, 1, 2, 3, 4, 5, 6})]
        [TestCase(new byte [] { 6, 5, 4, 3, 2, 1, 0})]
        [TestCase(new byte [] { 0, 1, 2, 3, 4, 5, 6, 7})]
        [TestCase(new byte [] { 7, 6, 5, 4, 3, 2, 1, 0})]
        [TestCase(new byte [] { 8, 7, 6, 5, 4, 3, 2, 1, 0})]
        [TestCase(new byte [] { 0, 1, 2, 3, 4, 5, 6, 7, 8})]
        public void EncodeDecodeBinary(byte[] values)
        {
            using var Service = new HashIDService(salt);
            
            CollectionAssert.AreEqual(values, Service.DecodeBinary(Service.Encode(values)));
        }
    }
}