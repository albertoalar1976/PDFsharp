// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Text;
using FluentAssertions;
using PdfSharp.Pdf.Filters;
using Xunit;

namespace PdfSharp.Tests.Filters
{
    /// <summary>
    /// Tests for the LZWDecode and FlateDecode filters: correctness, thread-safety and
    /// protection against decompression bombs.
    /// </summary>
    [Collection("PDFsharp")]
    public class DecodeFilterTests
    {
        [Fact]
        public void LzwDecode_decodes_example_from_PDF_reference()
        {
            // PDF Reference 1.7, 3.3.3 LZWDecode and FlateDecode Filters, Example 3.4:
            // The sequence '-----A---B' is encoded as 80 0B 60 50 22 0C 0C 85 01.
            byte[] encoded = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

            var decoded = new LzwDecode().Decode(encoded);

            Encoding.ASCII.GetString(decoded).Should().Be("-----A---B");
        }

        [Fact]
        public void LzwDecode_decodes_data_from_test_encoder()
        {
            var original = CreateCompressibleData(50_000, seed: 42);

            var decoded = new LzwDecode().Decode(TestLzwEncoder.Encode(original));

            decoded.Should().Equal(original);
        }

        [Fact]
        public void LzwDecode_returns_empty_array_for_empty_input()
        {
            // The former implementation threw an IndexOutOfRangeException.
            var decoded = new LzwDecode().Decode([]);

            decoded.Should().BeEmpty();
        }

        [Fact]
        public void LzwDecode_singleton_is_thread_safe()
        {
            // Filtering.GetFilter returns one LzwDecode instance for the whole process.
            // The former implementation kept its decoding state in instance fields, so parallel
            // decoding of different streams corrupted the results or threw exceptions.
            var filter = Filtering.GetFilter("/LZWDecode");
            filter.Should().BeSameAs(Filtering.GetFilter("/LZWDecode"));

            var inputs = Enumerable.Range(0, 8)
                .Select(i => CreateCompressibleData(20_000 + 1_000 * i, seed: i))
                .ToArray();
            var encodedInputs = inputs.Select(TestLzwEncoder.Encode).ToArray();

            var failures = 0;
            Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 8 }, iteration =>
            {
                var index = iteration % inputs.Length;
                try
                {
                    var decoded = filter.Decode(encodedInputs[index]);
                    if (!decoded.SequenceEqual(inputs[index]))
                        Interlocked.Increment(ref failures);
                }
                catch
                {
                    Interlocked.Increment(ref failures);
                }
            });

            failures.Should().Be(0);
        }

        [Fact]
        public void LzwDecode_stops_at_MaxDecodedLength()
        {
            var encoded = TestLzwEncoder.Encode(new byte[100_000]);

            var decode = () => new LzwDecode().Decode(encoded, new FilterParms(null) { MaxDecodedLength = 10_000 });

            decode.Should().Throw<PdfSharpException>().WithMessage("*exceeds the limit of 10000 bytes*");
        }

        [Fact]
        public void FlateDecode_stops_at_MaxDecodedLength()
        {
            // 10 MiB of zeros compress to a few kilobytes: a small decompression bomb.
            var bomb = new FlateDecode().Encode(new byte[10 * 1024 * 1024]);
            bomb.Length.Should().BeLessThan(100_000);

            var decodeWithLimit = () => new FlateDecode().Decode(bomb, new FilterParms(null) { MaxDecodedLength = 1024 * 1024 });
            decodeWithLimit.Should().Throw<PdfSharpException>().WithMessage("*FlateDecode*exceeds the limit*");

            // The default limit (256 MiB) is large enough for this stream.
            var decoded = new FlateDecode().Decode(bomb);
            decoded.Length.Should().Be(10 * 1024 * 1024);
        }

        [Fact]
        public void FlateDecode_writes_zlib_stream_with_Adler32_checksum()
        {
            // RFC 1950: a zlib stream ends with the Adler-32 checksum of the uncompressed data.
            // The former implementation omitted it.
            var data = CreateCompressibleData(10_000, seed: 3);

            var encoded = new FlateDecode().Encode(data);

            uint a = 1, b = 0;
            foreach (var value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            var adler32 = (b << 16) | a;
            var trailer = (uint)(encoded[^4] << 24 | encoded[^3] << 16 | encoded[^2] << 8 | encoded[^1]);
            trailer.Should().Be(adler32);

            // A strict decoder that verifies the checksum accepts the stream.
            using var zlib = new System.IO.Compression.ZLibStream(new MemoryStream(encoded), System.IO.Compression.CompressionMode.Decompress);
            var decoded = new MemoryStream();
            zlib.CopyTo(decoded);
            decoded.ToArray().Should().Equal(data);
        }

        [Fact]
        public void FlateDecode_roundtrip_is_unchanged()
        {
            var original = CreateCompressibleData(100_000, seed: 7);
            var filter = new FlateDecode();

            filter.Decode(filter.Encode(original)).Should().Equal(original);
        }

        [Fact]
        public void MaxDecodedLength_must_be_positive()
        {
            var set = () => Filtering.MaxDecodedLength = 0;

            set.Should().Throw<ArgumentOutOfRangeException>();
            Filtering.MaxDecodedLength.Should().Be(Filtering.DefaultMaxDecodedLength);
        }

        /// <summary>
        /// Creates text-like data with many repetitions, so that LZW creates long table entries.
        /// </summary>
        static byte[] CreateCompressibleData(int length, int seed)
        {
            var random = new Random(seed);
            string[] words = ["PDFsharp", "MigraDoc", "stream", "endstream", "obj", "endobj", " ", "\n", "0 0 612 792", "/Type /Page"];
            var result = new byte[length];
            var position = 0;
            while (position < length)
            {
                var word = words[random.Next(words.Length)];
                for (var i = 0; i < word.Length && position < length; i++)
                    result[position++] = (byte)word[i];
            }
            return result;
        }

        /// <summary>
        /// A minimal LZW encoder that produces data PDFsharp’s LZWDecode filter can read.
        /// To keep it simple, it always writes 9-bit codes and emits a clear-table code
        /// before the table would need 10-bit codes.
        /// </summary>
        static class TestLzwEncoder
        {
            const int ClearTable = 256;
            const int EndOfData = 257;
            const int FirstFreeCode = 258;
            const int LastUsableCode = 510;  // Keeps the decoder below 511 entries, i.e. at 9-bit codes.

            public static byte[] Encode(byte[] data)
            {
                var writer = new BitWriter();
                var table = new Dictionary<string, int>();
                var nextCode = FirstFreeCode;
                writer.Write(ClearTable);

                if (data.Length == 0)
                {
                    writer.Write(EndOfData);
                    return writer.ToArray();
                }

                var current = ((char)data[0]).ToString();
                for (var i = 1; i < data.Length; i++)
                {
                    var next = current + (char)data[i];
                    if (table.ContainsKey(next))
                    {
                        current = next;
                        continue;
                    }

                    writer.Write(CodeOf(current));
                    if (nextCode < LastUsableCode)
                    {
                        table.Add(next, nextCode++);
                    }
                    else
                    {
                        writer.Write(ClearTable);
                        table.Clear();
                        nextCode = FirstFreeCode;
                    }
                    current = ((char)data[i]).ToString();
                }
                writer.Write(CodeOf(current));
                writer.Write(EndOfData);
                return writer.ToArray();

                int CodeOf(string s) => s.Length == 1 ? s[0] : table[s];
            }

            sealed class BitWriter
            {
                public void Write(int code)
                {
                    for (var bit = 8; bit >= 0; bit--)
                    {
                        _current = (_current << 1) | ((code >> bit) & 1);
                        if (++_bitCount == 8)
                        {
                            _bytes.Add((byte)_current);
                            _current = 0;
                            _bitCount = 0;
                        }
                    }
                }

                public byte[] ToArray()
                {
                    if (_bitCount > 0)
                        _bytes.Add((byte)(_current << (8 - _bitCount)));
                    return [.. _bytes];
                }

                readonly List<byte> _bytes = [];
                int _current;
                int _bitCount;
            }
        }
    }
}
