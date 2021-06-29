# Proximity.HashID

A reimplementation of [Hashids.net](https://github.com/ullmark/hashids.net), offering span-based inputs and outputs.

## What is a Hash ID?

A Hash ID is a way to reversibly store integers in a user-readable string.

It is **not** a cryptographic hash, since it is reversible. It is **not** a cryptographically secure encryption algorithm either.

[http://www.hashids.org/net/](http://www.hashids.org/net/)

The Hash ID algorithm attempts to fulfil the following requirements:

1. Hashes must be unique and decryptable.
2. Hashes can contain multiple integers.
3. Hashes can have a minimum hash length.
4. Hashes should not contain basic English curse words.

## What is this library?

This library is designed to efficiently encode and decode Hash IDs.

1. Supports 32 and 64-bit integer inputs, both signed and unsigned.
2. Supports hexadecimal string and raw binary inputs.
3. Supports span-based inputs and outputs, allowing zero-allocation operations.
4. Supports .Net 4.5, and .Net Standard 2.0 and 2.1.
5. Thread safe, all methods can be called concurrently (except for `Dispose`).

Due to the Span-based implementation, this makes our minimum requirements the same as [System.Memory](https://www.nuget.org/packages/System.Memory/). For earlier frameworks, [Hashids.net](https://github.com/ullmark/hashids.net) may be more suitable.

## How do I install it?

The library can be installed with NuGet:

```
Install-Package Proximity.HashID
```

or

```
dotnet add package Proximity.HashID
```

## How do I use it?

### Basic Principles

The `HashIDService` is used to encode and decode Hash IDs. It takes a `salt` that is used to seed the algorithm, ensuring the generated Hash IDs are unique to that salt.

```c#
using Proximity.HashID;

using var Service = new HashIDService("this is my salt");
```

The `HashIDService` implements `IDisposable` as it rents some buffers from the `ArrayPool`. It also performs some pre-calculations, so we recommend creating the `HashIDService` once and reusing it.

### Encoding a single integer

A value can be encoded in several ways, depending on your requirements. Here, we pass an `Int32` and receive back a `String`:

```C#
var Hash = Service.Encode(12345);
```

This results in the hash: "**NkK9**". We also support `UInt32`, `Int64`, and `UInt64` values:

```C#
var Hash = Service.Encode(666555444333222L);
```

This results in the hash "**KVO9yy1oO5j**".

### Encoding multiple integers

Multiple values can be encoded into the same hash.

```C#
var Hash = Service.Encode(99, 25);
```

This results in the hash: "**97Jun**". We also support spans:

```C#
var Hash = Service.Encode(new [] { 1, 2, 3 }.AsSpan())
```

### Encoding without allocations

To reduce load on the garbage collector, we want to minimise or eliminate as many allocations as possible. Here, we pass an `Int32` and store it directly into a `Span<Char>`:

```C#
Span<char> Buffer = stackalloc char[16];

Service.TryEncode(123000, Buffer, out var CharsWritten);

var Hash = Buffer.Slice(0, CharsWritten);
```

This results in the characters "**58LzD**" being written into `Buffer`, with the value **5** in `CharsWritten`. We then generate a `Span<Char>` encompassing the Hash ID. No memory allocations are performed.

We also offer methods that take span inputs too:

```C#
Span<long> Input = new [] { 1, 2, 3 };
Span<char> Output = stackalloc char[16];

Service.TryEncode(Input, Output, out var CharsWritten);

var Hash = Output.Slice(0, CharsWritten);
```

Here, the C# compiler recognises that Input is a span, and allocates it on the stack.

This results in `Hash` referencing a Span containing "**laHquq**". Again, no memory allocations are performed.

### Encoding buffer sizes

If your output buffer isn't large enough, `TryEncode` methods will return **false**. Rather than guess at the maximum size of a Hash ID, you can call `MeasureEncode` to calculate the maximum size.

```C#
var MaxLength = Service.MeasureEncode(Input.Length);

Span<char> Output = stackalloc char[MaxLength];

Service.TryEncode(Input, Output, out var CharsWritten);

var Hash = Output.Slice(0, CharsWritten);
```

Here, we determine the maximum length of a hash generated from Input, then ensure our output buffer is at least that size.

### Encoding binary values

We support encoding of binary in two different formats. The first is hex-string encoding, compatible with Hashids.net.

```C#
var Hash = Service.EncodeHex("1d7f21dd38");
```

Here we encode the hexadecimal string and result in the hash "**4o6Z7KqxE**"

The second encoding is a slightly more efficient pure-binary encoding, which is compatible with EncodeHex for smaller values:

```C#
var Hash = Service.Encode(new byte [] { 0x1d, 0x7f, 0x21, 0xdd, 0x38 });
```

### Decoding a single integer

Decoding is done in a similar fashion to encoding. Here, we expect a single `Int32`:

```C#
var Value = Service.DecodeSingleInt32("NkK9");
```

This results in the value: **12345**. We also support `UInt32`, `Int64`, and `UInt64` values:

```C#
var Value = Service.DecodeSingleInt64("KVO9yy1oO5j");
```

This results in the value **666555444333222L**.

### Decoding multiple integers

Decoding can be performed into a number of array sizes:

```C#
var Value = Service.DecodeInt32("glSgV");
```

This results in an int array containing: **13**, **89**. We also support `UInt32`, `Int64`, and `UInt64` values:

```C#
var Value = Service.DecodeInt64("mPVbjj7yVMzCJL215n69");
```

This results in a long array containing: **666555444333222**, **12345678901112**.

### Decoding without allocations

We also support allocation-free decoding. Here, we pass a `ReadOnlySpan<char>` and store it directly into an Int32:

```C#
ReadOnlySpan<string> Input = "aBMswoO2UB3Sj";
Span<int> Output = stackalloc int[16];

Service.TryDecode(Input, Output, out var ValuesWritten);

var Value = Output.Slice(0, ValuesWritten);
```

This results in the array **683**, **94108**, **123**, **5** being written into `Output`, with **4** being stored into `ValuesWritten`.

### Decoding buffer sizes

We can quickly calculate the array size required to decode a hash using `MeasureDecode`:

```C#
var Length = Service.MeasureDecode("aBMswoO2UB3Sj");
```

We also support measuring of binary and hex string values, which return the maximum possible output size.

```C#
var Length = Service.MeasureDecodeHex("D9NPE");
```

Here, we receive the result of **12** as the maximum character buffer required.

```C#
var Length = Service.MeasureDecodeBinary("4o6Z7KqxE");
```

Here, we receive the result of **8** as the maximum byte buffer required.

### Decoding binary values

Decoding of binary occurs in two different formats. The first is hex-string encoding, compatible with Hashids.net.

```C#
var Hash = Service.EncodeHex("1d7f21dd38");
```

Here we encode the hexadecimal string and result in the hash "**4o6Z7KqxE**"

The second encoding is a slightly more efficient pure-binary encoding, which is compatible with EncodeHex for smaller values:

```C#
var Hash = Service.Encode(new byte [] { 0x1d, 0x7f, 0x21, 0xdd, 0x38 });
```

## Performance

This Hash ID library places an emphasis on performance and low garbage collection.

1. Stack allocated buffers are used rather than allocating small arrays for processing single numbers.
2. The only methods that perform allocations (excepting `ArrayPool` allocations) are ones that return `string` or array values.
3. Span-based methods are used internally, enabling the latest runtime performance improvements.
4. Minimal buffer copies result in less wasted CPU time.

