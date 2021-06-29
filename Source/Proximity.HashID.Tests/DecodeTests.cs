using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class DecodeTests : TestsBase
	{
		[TestCase("NkK9", 12345)]
		[TestCase("5O8yp5P", 666555444)]
		[TestCase("Wzo", 1337)]
		[TestCase("DbE", 808)]
		[TestCase("yj8", 303)]
		public void DecodeSingleInt32(string input, int expected)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(expected, Service.DecodeSingleInt32(input));
		}

		[TestCase("K4", 22u)]
		[TestCase("5O8yp5P", 666555444u)]
		[TestCase("j4r6j8Y", uint.MaxValue)]
		public void DecodeSingleUInt32(string input, uint expected)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(expected, Service.DecodeSingleUInt32(input));
		}

		[TestCase("NkK9", 12345)]
		[TestCase("5O8yp5P", 666555444)]
		[TestCase("Wzo", 1337)]
		[TestCase("DbE", 808)]
		[TestCase("yj8", 303)]
		public void TryDecodeSingleInt32(string input, int expected)
		{
			using var Service = new HashIDService(salt);

			Assert.IsTrue(Service.TryDecodeSingleInt32(input, out var Value));

			Assert.AreEqual(expected, Value);
		}

		[TestCase("NV", 1L)]
		[TestCase("21OjjRK", 2147483648L)]
		[TestCase("D54yen6", 4294967296L)]
		[TestCase("KVO9yy1oO5j", 666555444333222L)]
		[TestCase("4bNP1L26r", 12345678901112L)]
		[TestCase("jvNx4BjM5KYjv", long.MaxValue)]
		public void DecodeSingleInt64(string input, long expected)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(expected, Service.DecodeSingleInt64(input));
		}

		[TestCase("NV", 1L)]
		[TestCase("21OjjRK", 2147483648L)]
		[TestCase("D54yen6", 4294967296L)]
		[TestCase("KVO9yy1oO5j", 666555444333222L)]
		[TestCase("4bNP1L26r", 12345678901112L)]
		[TestCase("jvNx4BjM5KYjv", long.MaxValue)]
		public void TryDecodeSingleInt64(string input, long expected)
		{
			using var Service = new HashIDService(salt);

			Assert.IsTrue(Service.TryDecodeSingleInt64(input, out var Value));

			Assert.AreEqual(expected, Value);
		}

		[TestCase("1gRYUwKxBgiVuX", new[] { 66655, 5444333, 2, 22 })]
		[TestCase("aBMswoO2UB3Sj", new[] { 683, 94108, 123, 5 })]
		[TestCase("jYhp", new[] { 3, 4 })]
		[TestCase("k9Ib", new[] { 6, 5 })]
		[TestCase("EMhN", new[] { 31, 41 })]
		[TestCase("glSgV", new[] { 13, 89 })]
		public void DecodeInt32(string input, int[] expected)
		{
			using var Service = new HashIDService(salt);

			CollectionAssert.AreEqual(expected, Service.DecodeInt32(input));
		}

		[TestCase("mPVbjj7yVMzCJL215n69", new[] { 666555444333222L, 12345678901112L })]
		public void DecodeInt64(string input, long[] expected)
		{
			using var Service = new HashIDService(salt);

			CollectionAssert.AreEqual(expected, Service.DecodeInt64(input));
		}

		[TestCase("mPVbjj7yVMzCJL215n69", new[] { 666555444333222L, 12345678901112L })]
		public void TryDecodeInt64(string input, long[] expected)
		{
			using var Service = new HashIDService(salt);

			Assert.IsTrue(Service.TryDecode(input, out var Value));

			CollectionAssert.AreEqual(expected, Value);
		}

		[TestCase("mPVbjj7yVMzCJL215n69", new[] { 666555444333222L, 12345678901112L })]
		public void TryDecodeInt64Span(string input, long[] expected)
		{
			using var Service = new HashIDService(salt);

			var Values = new long[expected.Length];

			Assert.IsTrue(Service.TryDecode(input.AsSpan(), Values, out var ValuesWritten));

			Assert.AreEqual(expected.Length, ValuesWritten);

			CollectionAssert.AreEqual(expected, Values);
		}

		[Test]
		public void DecodeIncorrectSalt()
		{
			using (var Service = new HashIDService(salt))
				Assert.AreEqual(12345, Service.DecodeSingleInt32("NkK9"));

			using (var Service = new HashIDService("this is my pepper"))
				Assert.AreEqual(0, Service.DecodeSingleInt32("NkK9"));
		}

		[TestCase("gB0NV05e", new[] { 1 })]
		[TestCase("mxi8XH87", new[] { 25, 100, 950 })]
		[TestCase("KQcmkIW8hX", new[] { 5, 200, 195, 1 })]
		public void DecodeWithMinimumLength(string input, int[] expected)
		{
			using var Service = new HashIDService(salt, minimumHashLength: 8);

			CollectionAssert.AreEqual(expected, Service.DecodeInt32(input));
		}

		[TestCase("DZaK2yDZ", new byte[] { 0x1d, 0x7f, 0x21, 0xdd, 0x38 })]
		public void DecodeBinary(string input, byte[] expected)
		{
			using var Service = new HashIDService(salt);

			CollectionAssert.AreEqual(expected, Service.DecodeBinary(input));
		}

		[TestCase("aBMswoO2UB3Sj", 4)]
		public void MeasureDecode(string input, int expected)
		{
			using var Service = new HashIDService(salt);

			Assert.AreEqual(expected, Service.MeasureDecode(input));
		}
	}
}
