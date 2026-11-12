// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Internal.Imaging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality;
using Xunit;

namespace PdfSharp.Tests.Drawing
{
    /// <summary>
    /// GIF and TIFF images. The images in TestImages were written by Pillow and ImageMagick (see generate.py),
    /// the expected pixels are those decoded by Pillow.
    /// </summary>
    [Collection("PDFsharp")]
    public class ImageFormatTests : IDisposable
    {
        public ImageFormatTests()
        {
            PdfSharpCore.ResetAll();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        public static TheoryData<string> Images()
        {
            var data = new TheoryData<string>();
            foreach (var entry in Manifest)
                data.Add(entry.File);
            return data;
        }

        [Theory]
        [MemberData(nameof(Images))]
        public void Decodes_the_pixels_like_Pillow(string file)
        {
            var entry = Manifest.Single(e => e.File == file);

            ToBeNamed.TryImportImage(File.ReadAllBytes(Path.Combine(Folder, file)), out var image).Should().BeTrue();

            image!.PixelWidth.Should().Be(entry.Width);
            image.PixelHeight.Should().Be(entry.Height);
            image.ImageFormat.ToString().Should().Be(entry.Format);
            if (image is ImportedCcittImage)
                return;  // The compressed data are written to PDF as they are, see the PDF test.

            var raster = (ImportedRasterImage)image;
            var expected = ReadExpected(file);
            if (entry.Mode == "CMYK")
            {
                raster.ExtractedImageData.Should().Equal(expected);
                return;
            }

            var actual = ToRgba(raster);
            actual.Length.Should().Be(expected.Length);
            // Premultiplied alpha loses precision for small alpha values, so compare only clearly visible pixels.
            bool associatedAlpha = file.Contains("associated", StringComparison.Ordinal);
            for (int idx = 0; idx < expected.Length; idx += 4)
            {
                int alpha = expected[idx + 3];
                Math.Abs(actual[idx + 3] - alpha).Should().BeLessThanOrEqualTo(1, $"alpha of pixel {idx / 4}");
                if (alpha == 0 || associatedAlpha && alpha < 128)
                    continue;
                int tolerance = associatedAlpha ? 2 : 1;
                for (int c = 0; c < 3; c++)
                    Math.Abs(actual[idx + c] - expected[idx + c]).Should().BeLessThanOrEqualTo(tolerance, $"component {c} of pixel {idx / 4}");
            }
        }

        [Theory]
        [MemberData(nameof(Images))]
        public void Writes_the_image_to_a_PDF_file(string file)
        {
            var entry = Manifest.Single(e => e.File == file);
            var document = new PdfDocument();
            var page = document.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
            using (var image = XImage.FromFile(Path.Combine(Folder, file)))
                gfx.DrawImage(image, 10, 10);
            var stream = new MemoryStream();
            document.Save(stream);

            using var pdf = PdfReader.Open(new MemoryStream(stream.ToArray()), PdfDocumentOpenMode.Import);
            var xObjects = pdf.Pages[0].Resources.Elements.GetDictionary("/XObject")!;
            var pdfImage = (PdfDictionary)((PdfReference)xObjects.Elements.Values.Single()!).Value;
            pdfImage.Elements.GetInteger("/Width").Should().Be(entry.Width);
            pdfImage.Elements.GetInteger("/Height").Should().Be(entry.Height);

            var colorSpace = pdfImage.Elements["/ColorSpace"];
            switch (entry.Format)
            {
                case "CcittFax":
                    {
                        pdfImage.Elements.GetName("/Filter").Should().Be("/CCITTFaxDecode");
                        pdfImage.Elements.GetInteger("/BitsPerComponent").Should().Be(1);
                        colorSpace.Should().BeOfType<PdfName>().Which.Value.Should().Be("/DeviceGray");
                        var parameters = pdfImage.Elements.GetDictionary("/DecodeParms")!;
                        parameters.Elements.GetInteger("/K").Should().Be(file.Contains("g4", StringComparison.Ordinal) ? -1 : 0);
                        parameters.Elements.GetInteger("/Columns").Should().Be(entry.Width);
                        parameters.Elements.GetInteger("/Rows").Should().Be(entry.Height);
                        parameters.Elements.GetBoolean("/EncodedByteAlign").Should().Be(file.Contains("-mh", StringComparison.Ordinal));
                        break;
                    }
                case "Cmyk32":
                    colorSpace.Should().BeOfType<PdfName>().Which.Value.Should().Be("/DeviceCMYK");
                    break;
                case "Gray8":
                    colorSpace.Should().BeOfType<PdfName>().Which.Value.Should().Be("/DeviceGray");
                    break;
                case "Bgr32":
                case "Bgra32":
                    colorSpace.Should().BeOfType<PdfName>().Which.Value.Should().Be("/DeviceRGB");
                    break;
                default:
                    colorSpace.Should().BeOfType<PdfArray>().Which.Elements.GetName(0).Should().Be("/Indexed");
                    break;
            }

            // A soft mask exists if and only if the image has transparent pixels.
            bool transparent = entry.Mode == "RGBA" && entry.Format != "CcittFax"
                               && ReadExpected(file).Where((_, idx) => idx % 4 == 3).Any(alpha => alpha != 255);
            pdfImage.Elements.ContainsKey("/SMask").Should().Be(transparent);
        }

        [Theory]
        [InlineData("Test.gif", 64, 64, "e54bbd5c2c3a25d3e7308adaf2c86e0e02d58744ddaa3bd6ac47116322c36b10")]
        [InlineData("image009.gif", 689, 91, "6a678bbdae0732d4d627aa5f03cca43f304494edcc0c1824d69fe96df1605f95")]
        [InlineData("Rose (RGB 8).tif", 100, 113, "d10d102ce8b33306c98ae316c2212f24e447a723181e923e400fd6311071a9cf")]
        public void Decodes_the_sample_images_like_Pillow(string file, int width, int height, string sha256)
        {
            // The hashes are those of the pixels decoded by Pillow, as RGBA with RGB set to 0 for transparent pixels.
            var path = IOUtility.GetAssetsPath("pdfsharp/images/samples/misc/" + file)!;

            ToBeNamed.TryImportImage(File.ReadAllBytes(path), out var image).Should().BeTrue();

            image!.PixelWidth.Should().Be(width);
            image.PixelHeight.Should().Be(height);
            Convert.ToHexStringLower(SHA256.HashData(ToRgba((ImportedRasterImage)image))).Should().Be(sha256);
        }

        [Fact]
        public void Rejects_images_that_are_too_large_without_allocating_them()
        {
            // GIF with a logical screen of 65535 × 65535 pixels, which would need 4 GB.
            var gif = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0x2C, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 8, 0 };
            ToBeNamed.TryImportImage(gif, out _).Should().BeFalse();

            // TIFF with 1,000,000 × 1,000,000 RGB pixels.
            var tiff = File.ReadAllBytes(Path.Combine(Folder, "tiff-rgb-none.tif"));
            SetTiffTag(tiff, 256, 1_000_000);
            SetTiffTag(tiff, 257, 1_000_000);
            ToBeNamed.TryImportImage(tiff, out _).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(Images))]
        public void Does_not_throw_for_truncated_files(string file)
        {
            var data = File.ReadAllBytes(Path.Combine(Folder, file));
            foreach (var length in new[] { 10, 50, data.Length / 3, data.Length / 2, data.Length - 1 })
            {
                var truncate = () => ToBeNamed.TryImportImage(data[..length], out _);
                truncate.Should().NotThrow();
            }
        }

        static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Drawing", "TestImages");

        sealed record ManifestEntry(string File, int Width, int Height, string Format, string Mode);

        static readonly IReadOnlyList<ManifestEntry> Manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(
            File.ReadAllText(Path.Combine(Folder, "manifest.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        static byte[] ReadExpected(string file)
        {
            using var gzip = new GZipStream(File.OpenRead(Path.Combine(Folder, file + ".expected.gz")), CompressionMode.Decompress);
            var result = new MemoryStream();
            gzip.CopyTo(result);
            return result.ToArray();
        }

        /// <summary>
        /// Converts the decoded image to RGBA with 8 bits per sample. RGB is 0 for transparent pixels.
        /// </summary>
        static byte[] ToRgba(ImportedRasterImage image)
        {
            int width = image.PixelWidth, height = image.PixelHeight;
            var data = image.ExtractedImageData!;
            var palette = image.PaletteData;
            var alpha = image.AlphaMaskData;
            int bits = image.ImageFormat switch
            {
                ImageFormats.Indexed1 => 1,
                ImageFormats.Indexed4 => 4,
                ImageFormats.Indexed8 => 8,
                _ => 0
            };
            int rowBytes = (width * bits + 7) / 8;
            var rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixel = y * width + x;
                    byte r, g, b;
                    switch (image.ImageFormat)
                    {
                        case ImageFormats.Gray8:
                            r = g = b = data[pixel];
                            break;
                        case ImageFormats.Bgr32:
                        case ImageFormats.Bgra32:
                            r = data[3 * pixel];
                            g = data[3 * pixel + 1];
                            b = data[3 * pixel + 2];
                            break;
                        default:
                            {
                                int bit = x * bits;
                                int index = (data[y * rowBytes + bit / 8] >> (8 - bits - bit % 8)) & ((1 << bits) - 1);
                                r = palette![3 * index];
                                g = palette[3 * index + 1];
                                b = palette[3 * index + 2];
                                break;
                            }
                    }
                    byte a = alpha?[pixel] ?? 255;
                    if (a != 0)
                    {
                        rgba[4 * pixel] = r;
                        rgba[4 * pixel + 1] = g;
                        rgba[4 * pixel + 2] = b;
                    }
                    rgba[4 * pixel + 3] = a;
                }
            }
            return rgba;
        }

        /// <summary>
        /// Sets the value of a SHORT or LONG tag in the first directory of a little-endian TIFF file.
        /// </summary>
        static void SetTiffTag(byte[] tiff, int tag, int value)
        {
            int directory = BinaryPrimitives.ReadInt32LittleEndian(tiff.AsSpan(4));
            int count = BinaryPrimitives.ReadUInt16LittleEndian(tiff.AsSpan(directory));
            for (int idx = 0; idx < count; idx++)
            {
                var entry = tiff.AsSpan(directory + 2 + 12 * idx, 12);
                if (BinaryPrimitives.ReadUInt16LittleEndian(entry) != tag)
                    continue;
                BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], 4);  // LONG
                BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 1);
                BinaryPrimitives.WriteInt32LittleEndian(entry[8..], value);
                return;
            }
            throw new InvalidOperationException($"Tag {tag} not found.");
        }
    }
}
