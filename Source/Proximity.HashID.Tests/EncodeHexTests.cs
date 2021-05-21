using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
    [TestFixture]
    public class EncodeHexTests : TestsBase
    {
        [TestCase("FA", "lzY")]
        [TestCase("26dd", "MemE")]
        [TestCase("FF1A", "eBMrb")]
        [TestCase("12abC", "D9NPE")]
        [TestCase("185b0", "9OyNW")]
        [TestCase("17b8d", "MRWNE")]
        [TestCase("1d7f21dd38", "4o6Z7KqxE")]
        [TestCase("20015111d", "ooweQVNB")]
        public void EncodeHex(string hexString, string expected)
        {
            using var Service = new HashIDService(salt);
            
            Assert.AreEqual(expected, Service.EncodeHex(hexString));
        }

        public void EncodeInvalidHex()
        {
            using var Service = new HashIDService();
            
            Assert.AreEqual("", Service.EncodeHex("XYZ123"));
        }
    }
}