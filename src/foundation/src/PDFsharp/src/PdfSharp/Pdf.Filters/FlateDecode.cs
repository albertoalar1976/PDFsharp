// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO.Compression;
using Microsoft.Extensions.Logging;
using PdfSharp.Logging;

namespace PdfSharp.Pdf.Filters
{
    /// <summary>
    /// Implements the FlateDecode filter by wrapping SharpZipLib.
    /// </summary>
    public class FlateDecode : Filter
    {
        // Reference: 3.3.3  LZWDecode and FlateDecode Filters / Page 71

        /// <summary>
        /// Encodes the specified data.
        /// </summary>
        public override byte[] Encode(byte[] data)
        {
            return Encode(data, PdfFlateEncodeMode.Default);
        }

        /// <summary>
        /// Encodes the specified data.
        /// </summary>
        public byte[] Encode(byte[] data, PdfFlateEncodeMode mode)
        {
            using var ms = new MemoryStream();

            CompressionLevel level;
            switch (mode)
            {
                case PdfFlateEncodeMode.BestCompression:
                    level = CompressionLevel.SmallestSize;
                    break;
                case PdfFlateEncodeMode.BestSpeed:
                    level = CompressionLevel.Fastest;
                    break;
                default:
                    level = CompressionLevel.Optimal;
                    break;
            }

            // ZLibStream writes the zlib header (RFC 1950) and, at the end, the Adler-32 checksum of the
            // uncompressed data. The former implementation wrote a hand-made header followed by a raw
            // DeflateStream without the checksum, so strict decoders could reject the streams.
            using (var zlib = new ZLibStream(ms, level, leaveOpen: true))
                zlib.Write(data, 0, data.Length);

            return ms.ToArray();
        }

        /// <summary>
        /// Decodes the specified data.
        /// </summary>
        public override byte[] Decode(byte[] data, FilterParms? parms)
        {
            using var msInput = new MemoryStream(data);
            using var msOutput = new MemoryStream();
#if true
            // CMF (Compression Method Flags) must be deflated.
            byte byte1 = (byte)msInput.ReadByte();
            if ((byte1 & 0xF) != 0x8)
                PdfSharpLogHost.Logger.LogWarning("Cannot decode stream. Compression method must be deflate.");


            // Flags.
            byte byte2 = (byte)msInput.ReadByte();
            if ((byte2 & 0x20) != 0)
                PdfSharpLogHost.Logger.LogWarning("Cannot decode stream. DeflateStream does not support Adler32.");
#else
            // ReSharper disable once RedundantAssignment
            var header = new byte[]
            {
                (byte)msInput.ReadByte(), // CMF (Compression Method and flags)
                (byte)msInput.ReadByte()  // Flags
            };

            if ((header[0] & 0xF) != 0x8)
                PdfSharpLogHost.Logger.LogWarning("Can’t decode stream: Wrong header: Compression method must be deflate.");

            if ((header[1] & 0x20) != 0)
                PdfSharpLogHost.Logger.LogWarning("Can’t decode stream: Wrong header: DeflateStream does not support Adler32.");
#endif

            using var zip = new DeflateStream(msInput, CompressionMode.Decompress, true);
            CopyWithLimit(zip, msOutput, parms?.MaxDecodedLength ?? Filtering.MaxDecodedLength);
            msOutput.Flush();

            if (msOutput.Length >= 0)
            {
                msOutput.Capacity = (int)msOutput.Length;
                if (parms?.DecodeParms != null)
                    return StreamDecoder.Decode(msOutput.GetBuffer(), parms.DecodeParms);
            }
            return msOutput.GetBuffer();
        }

        /// <summary>
        /// Copies the decompressed data and stops with an exception as soon as more than
        /// maxLength bytes are produced. Unlike Stream.CopyTo, a decompression bomb cannot
        /// make the output grow until the process runs out of memory.
        /// </summary>
        static void CopyWithLimit(Stream source, MemoryStream destination, int maxLength)
        {
            var buffer = new byte[81920];  // Same buffer size as Stream.CopyTo.
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (destination.Length + bytesRead > maxLength)
                    Filtering.ThrowMaxDecodedLengthExceeded(PdfFilterNames.FlateDecode, maxLength);
                destination.Write(buffer, 0, bytesRead);
            }
        }
    }
}
