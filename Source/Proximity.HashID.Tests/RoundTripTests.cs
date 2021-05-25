using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class RoundTripTests : TestsBase
	{
		[TestCase(new[] { 0, 1, 2 })]
		[TestCase(new[] { 1, 2, 0 })]
		public void EncodeDecodeZeros(int[] values)
		{
			using var Service = new HashIDService(salt);

			CollectionAssert.AreEqual(values, Service.DecodeInt32(Service.Encode(values)));
		}

		[TestCase("ABCDEF")]
		[TestCase("012345")]
		[TestCase("8")]
		[TestCase("AAAAAAAAAAA")]
		[TestCase("AAAAAAAAAAAB")]
		public void EncodeDecodeHex(string value)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(value, Service.DecodeHex(Service.EncodeHex(value)));
		}

		[TestCase(-1)]
		[TestCase(-255)]
		[TestCase(-256)]
		[TestCase(int.MinValue)]
		public void EncodeDecodeNegativeInt(int value)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(value, Service.DecodeSingleInt32(Service.Encode(value)));
		}

		[TestCase(-1)]
		[TestCase(-255)]
		[TestCase(-256)]
		[TestCase(int.MinValue)]
		[TestCase(long.MinValue)]
		public void EncodeDecodeNegativeLong(long value)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(value, Service.DecodeSingleInt64(Service.Encode(value)));
		}

		[TestCase(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })]
		//[TestCase(new byte[] { 0, 2, 2, 3, 4, 5, 6 })]
		[TestCase(new byte[] { 0, 3, 2, 3, 4, 5, 6, 7 })]
		[TestCase(new byte[] { 4, 7, 6, 5, 4, 3, 2, 1, 0 })]
		//[TestCase(new byte[] { 5, 5, 4, 3, 2, 1, 0 })]
		[TestCase(new byte[] { 6, 6, 5, 4, 3, 2, 1, 0 })]
		public void EncodeDecodeBinary(byte[] values)
		{
			using var Service = new HashIDService(salt);

			var Encoded = Service.Encode(values);

			Console.WriteLine("Encoded: {0}", Encoded);

			var Decoded = Service.DecodeBinary(Encoded);

			CollectionAssert.AreEqual(values, Decoded);
		}
	}
}
