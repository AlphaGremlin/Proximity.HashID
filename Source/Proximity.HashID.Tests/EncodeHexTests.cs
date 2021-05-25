using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class EncodeHexTests : TestsBase
	{
		[TestCase("FA", "lzY")]
		[TestCase("00", "4Y6")]
		[TestCase("1", "8K")]
		[TestCase("26dd", "MemE")]
		[TestCase("FF1A", "eBMrb")]
		[TestCase("12abC", "D9NPE")]
		[TestCase("185b0", "9OyNW")]
		[TestCase("17b8d", "MRWNE")]
		[TestCase("1d7f21dd38", "4o6Z7KqxE")]
		[TestCase("20015111d", "ooweQVNB")]
		[TestCase("FFFFFFFFFFFFFFFF", "MVb2ybYrWvSY4a8")]
		[TestCase("12345678123456", "YgKO1WoErqCMK")]
		[TestCase("1234567812345678", "3ZlR7kV3DJIll6a")]
		[TestCase("12345678123456789A", "jmqEe34yKZsXR8QM")]
		[TestCase("123456789ABCDE123456789ABCDE", "zr3neyWPbKuB9QwWojg3fkPKP")]
		public void EncodeHex(string hexString, string expected)
		{
			using var Service = new HashIDService(salt);

			var Encoded = Service.EncodeHex(hexString);

			Console.WriteLine("Encoded: {0}", Encoded);

			Assert.AreEqual(expected, Encoded);
		}

		public void EncodeInvalidHex()
		{
			using var Service = new HashIDService();

			Assert.AreEqual("", Service.EncodeHex("XYZ123"));
		}
	}
}
