using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
    [TestFixture]
    public class EncodeTests : TestsBase
    {
        [Test]
        public void EncodeDefaultSalt()
        {
            using var Service = new HashIDService();
            
            Assert.AreEqual("o2fXhV", Service.Encode(1,2,3));
        }


        [TestCase(22       , "K4")]
        [TestCase(333      , "OqM")]
        [TestCase(9999     , "kQVg")]
        [TestCase(123000   , "58LzD")]
        [TestCase(456000000, "5gn6mQP")]
        [TestCase(987654321, "oyjYvry")]
        public void EncodeInt32(int input, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual(expected, Service.Encode(input));
        }

        [TestCase(1L              , "NV")]
        [TestCase(2147483648L     , "21OjjRK")]
        [TestCase(4294967296L     , "D54yen6")]
        [TestCase(666555444333222L, "KVO9yy1oO5j")]
        [TestCase(12345678901112L , "4bNP1L26r")]
        [TestCase(long.MaxValue   , "jvNx4BjM5KYjv")]
        public void EncodeInt64(long input, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual(expected, Service.Encode(input));
        }


        [TestCase(new [] { 1, 2, 3                                           }, "laHquq")]
        [TestCase(new [] { 2,4,6                                             }, "44uotN")]
        [TestCase(new [] { 99,25                                             }, "97Jun")]
        [TestCase(new [] { 1337,42,314                                       }, "7xKhrUxm")]
        [TestCase(new [] { 683, 94108, 123, 5                                }, "aBMswoO2UB3Sj")]
        [TestCase(new [] { 547, 31, 241271, 311, 31397, 1129, 71129          }, "3RoSDhelEyhxRsyWpCx5t1ZK")]
        [TestCase(new [] { 21979508, 35563591, 57543099, 93106690, 150649789 }, "p2xkL3CK33JjcrrZ8vsw4YRZueZX9k")]
        public void EncodeInt32List(int[] input, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual(expected, Service.Encode(input));
        }

        [TestCase(new [] { 666555444333222L, 12345678901112L }, "mPVbjj7yVMzCJL215n69")]
        public void EncodeInt64List(long[] input, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual(expected, Service.Encode(input));
        }

        [TestCase(new [] { 666555444333222L, 12345678901112L }, "mPVbjj7yVMzCJL215n69")]
        public void TryEncodeInt64List(long[] input, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.IsTrue(Service.TryEncode(input, out var Value));

            Assert.AreEqual(expected, Value);
        }

        [Test]
        public void EncodeEmpty()
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual("", Service.Encode());
        }

        [TestCase(new [] { 1 }, "aJEDngB0NV05ev1WwP")]
        [TestCase(new [] { 4140, 21147, 115975, 678570, 4213597, 27644437 }, "pLMlCWnJSXr1BSpKgqUwbJ7oimr7l6")]
        public void EncodeMinimumLength(int[] input, string expected)
        {
            using var Service = new HashIDService(salt, minimumHashLength: 18);

            Assert.AreEqual(expected, Service.Encode(input));
        }

        [Test]
        public void EncodeCustomAlphabet()
        {
            using var Service = new HashIDService(salt, "ABCDEFGhijklmn34567890-:");

            Assert.AreEqual("6nhmFDikA0", Service.Encode(new [] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void EncodeRepeatingValuesTogether()
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual("1Wc8cwcE", Service.Encode(5, 5, 5, 5));
        }

        [Test]
        public void EncodeIncrementingValuesTogether()
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual("kRHnurhptKcjIDTWC3sx", Service.Encode(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));
        }

        [TestCase(1, "NV")]
        [TestCase(2, "6m")]
        [TestCase(3, "yD")]
        [TestCase(4, "2l")]
        [TestCase(5, "rD")]
        public void EncodeIncrementingValuesAsDistinct(int value, string expected)
        {
            using var Service = new HashIDService(salt);

            Assert.AreEqual(expected, Service.Encode(value));
        }

        [TestCase(new byte [] { 0x1d, 0x7f, 0x21, 0xdd, 0x38 }, "8Bmnwjbq1")]
        public void EncodeBinary(byte[] bytes, string expected)
        {
            using var Service = new HashIDService(salt);
            
            Assert.AreEqual(expected, Service.Encode(bytes));
        }
    }
}