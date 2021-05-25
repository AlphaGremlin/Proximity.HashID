using System;
using System.Linq;
using NUnit.Framework;

namespace Proximity.HashID.Tests
{
	[TestFixture]
	public class EncodeBinaryTests : TestsBase
	{
		[TestCase(new byte[] { 0xFA }, "lzY")] // "lzY", ==
		[TestCase(new byte[] { 0x00 }, "6m")] // "4Y6", -1
		[TestCase(new byte[] { 0x01 }, "yD")] // "8K", -1
		[TestCase(new byte[] { 0x26, 0xDD }, "ORk2")] // "rD7D", ==
		[TestCase(new byte[] { 0xFF, 0x1A }, "eBMrb")] // "eBMrb", ==
		[TestCase(new byte[] { 0x1, 0x2A, 0xBC }, "2noaO")] // "D9NPE", ==
		[TestCase(new byte[] { 0x1, 0x85, 0xB0 }, "34MkJ")] // "9OyNW", ==
		[TestCase(new byte[] { 0x1, 0x7B, 0x8D }, "WOjK1")] // "MRWNE", ==
		[TestCase(new byte[] { 0x1D, 0x7F, 0x21, 0xDD, 0x38 }, "DZaK2yDZ")] // "4o6Z7KqxE", -1
		[TestCase(new byte[] { 0x2, 0x00, 0x15, 0x11, 0x1D }, "xl1rQYjk")] // "ooweQVNB", ==
		[TestCase(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x12, 0x34, 0x56, }, "VNBXry3JYlR")] // "YgKO1WoErqCMK", -2
		[TestCase(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x12, 0x34, 0x56, 0x78 }, "18ZEyDa1z59w6")] // "3ZlR7kV3DJIll6a", -2
		[TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, "n81WpyvKpk9R8H3")] // "MVb2ybYrWvSY4a8", ==
		[TestCase(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x12, 0x34, 0x56, 0x78, 0x9A }, "ZxLWwLgazBW9kuMy")] // "jmqEe34yKZsXR8QM", ==
		[TestCase(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE }, "ZxLWwLgDkM2KKtMj5eWeJVa")] // "zr3neyWPbKuB9QwWojg3fkPKP", -2
		public void EncodeBinary(byte[] binary, string expected)
		{
			using var Service = new HashIDService(salt);

			var Encoded = Service.Encode(binary);

			Console.WriteLine("Encoded: {0}", Encoded);

			Assert.AreEqual(expected, Encoded);
		}

		[TestCase(new long[] { 0x1FA }, "lzY")] // Identical to HexString
		[TestCase(new long[] { 0x126DD }, "MemE")] // "MemE"
		[TestCase(new long[] { 0x1FF1A }, "eBMrb")] // "eBMrb"
		[TestCase(new long[] { 0x112ABC }, "D9NPE")] // "D9NPE"
		[TestCase(new long[] { 0x1185B0 }, "9OyNW")] // "9OyNW"
		[TestCase(new long[] { 0x117B8D }, "MRWNE")] // "MRWNE"
		[TestCase(new long[] { 0x11D7F21DD38 }, "4o6Z7KqxE")] // "4o6Z7KqxE"
		[TestCase(new long[] { 0x120015111D }, "ooweQVNB")] // "ooweQVNB"
		public void EncodeEquivalentBinary(long[] values, string expected)
		{
			using var Service = new HashIDService(salt);

			var Encoded = Service.Encode(values);

			Console.WriteLine("Encoded: {0}", Encoded);

			Assert.AreEqual(expected, Encoded);
		}
	}
}
