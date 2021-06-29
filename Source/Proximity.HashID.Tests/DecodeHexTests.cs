using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class DecodeHexTests : TestsBase
	{
		[TestCase("lzY", "FA")]
		[TestCase("eBMrb", "FF1A")]
		[TestCase("D9NPE", "12ABC")]
		public void DecodeHex(string hash, string hexString)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(hexString, Service.DecodeHex(hash));
		}

		[TestCase("lzY", "FA")]
		[TestCase("eBMrb", "FF1A")]
		[TestCase("D9NPE", "12ABC")]
		public void TryDecodeHex(string hash, string hexString)
		{
			using var Service = new HashIDService(salt);

			Assert.IsTrue(Service.TryDecodeHex(hash, out var Value));

			Assert.AreEqual(hexString, Value);
		}

		[TestCase("D9NPE", 12)]
		public void MeasureDecodeHex(string input, int expected)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(expected, Service.MeasureDecodeHex(input));
		}
	}
}
