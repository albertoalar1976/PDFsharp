// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality;
using Xunit;

namespace PdfSharp.Tests.Fonts
{
    [Collection("PDFsharp")]
    public class SystemFontResolverTests : IDisposable
    {
        public SystemFontResolverTests()
        {
            PdfSharpCore.ResetAll();
            _tempFolder = Path.Combine(Path.GetTempPath(), "PdfSharp.Tests", "SystemFonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
            try { Directory.Delete(_tempFolder, true); }
            catch (IOException) { }
        }

        static string NotoFolder => IOUtility.GetAssetsPath("fonts/Noto/Noto_Sans/static")!;

        static string ArialFolder => IOUtility.GetAssetsPath("pdfsharp-6.x/fonts/Arial")!;

        [Theory]
        [InlineData(false, false, "NotoSans-Regular.ttf")]
        [InlineData(true, false, "NotoSans-Bold.ttf")]
        [InlineData(false, true, "NotoSans-Italic.ttf")]
        [InlineData(true, true, "NotoSans-BoldItalic.ttf")]
        public void Resolves_the_face_of_the_requested_style(bool bold, bool italic, string expectedFile)
        {
            var resolver = new SystemFontResolver([NotoFolder]);

            var info = resolver.ResolveTypeface("Noto Sans", bold, italic);

            FileOf(info).Should().Be(expectedFile);
            info!.MustSimulateBold.Should().BeFalse();
            info.MustSimulateItalic.Should().BeFalse();
        }

        [Fact]
        public void Family_names_are_case_insensitive()
        {
            var resolver = new SystemFontResolver([NotoFolder]);

            FileOf(resolver.ResolveTypeface("noto SANS condensed", true, false)).Should().Be("NotoSans_Condensed-Bold.ttf");
        }

        [Fact]
        public void Resolves_families_of_other_weights()
        {
            var resolver = new SystemFontResolver([NotoFolder]);

            FileOf(resolver.ResolveTypeface("Noto Sans SemiBold", false, false)).Should().Be("NotoSans-SemiBold.ttf");
            var black = resolver.ResolveTypeface("Noto Sans Black", true, false);
            FileOf(black).Should().Be("NotoSans-Black.ttf");
            black!.MustSimulateBold.Should().BeFalse("a black face is heavier than bold");
        }

        [Fact]
        public void Simulates_styles_that_the_family_does_not_provide()
        {
            CopyToTempFolder(NotoFolder, "NotoSans-Regular.ttf");
            var resolver = new SystemFontResolver([_tempFolder]);

            var info = resolver.ResolveTypeface("Noto Sans", true, true);

            FileOf(info).Should().Be("NotoSans-Regular.ttf");
            info!.MustSimulateBold.Should().BeTrue();
            info.MustSimulateItalic.Should().BeTrue();
        }

        [Fact]
        public void Never_uses_a_bold_face_for_a_regular_request_if_a_regular_face_exists()
        {
            CopyToTempFolder(NotoFolder, "NotoSans-Bold.ttf", "NotoSans-Regular.ttf");
            var resolver = new SystemFontResolver([_tempFolder]);

            FileOf(resolver.ResolveTypeface("Noto Sans", false, false)).Should().Be("NotoSans-Regular.ttf");
        }

        [Fact]
        public void Chooses_the_nearest_weight_of_a_typographic_family()
        {
            // Only "Noto Sans Light" (300) and "Noto Sans Black" (900) exist. Both belong to the typographic family "Noto Sans".
            CopyToTempFolder(NotoFolder, "NotoSans-Light.ttf", "NotoSans-Black.ttf");
            var resolver = new SystemFontResolver([_tempFolder]);

            FileOf(resolver.ResolveTypeface("Noto Sans", false, false)).Should().Be("NotoSans-Light.ttf");
            var bold = resolver.ResolveTypeface("Noto Sans", true, false);
            FileOf(bold).Should().Be("NotoSans-Black.ttf");
            bold!.MustSimulateBold.Should().BeFalse();
        }

        [Fact]
        public void Resolves_full_names()
        {
            var resolver = new SystemFontResolver([NotoFolder]);

            FileOf(resolver.ResolveTypeface("Noto Sans Bold Italic", false, false)).Should().Be("NotoSans-BoldItalic.ttf");
        }

        [Fact]
        public void Returns_null_for_unknown_families()
        {
            var resolver = new SystemFontResolver([NotoFolder]);

            resolver.ResolveTypeface("No Such Font Family", false, false).Should().BeNull();
        }

        [Theory]
        [InlineData("Helvetica")]
        [InlineData("Liberation Sans")]
        [InlineData("Arimo")]
        public void Uses_a_metric_compatible_family_if_a_family_is_not_installed(string familyName)
        {
            var resolver = new SystemFontResolver([ArialFolder]);

            FileOf(resolver.ResolveTypeface(familyName, true, false)).Should().Be("arialbd.ttf");
        }

        [Fact]
        public void Metric_compatible_substitutes_can_be_switched_off()
        {
            var resolver = new SystemFontResolver([ArialFolder]) { UseMetricCompatibleSubstitutes = false };

            resolver.ResolveTypeface("Helvetica", false, false).Should().BeNull();
            resolver.ResolveTypeface("Arial", false, false).Should().NotBeNull();
        }

        [Fact]
        public void Lists_the_family_names()
        {
            var resolver = new SystemFontResolver([NotoFolder, ArialFolder]);

            var names = resolver.GetFamilyNames();

            names.Should().Contain(["Arial", "Noto Sans", "Noto Sans Condensed", "Noto Sans SemiBold"]);
            names.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolves_and_extracts_faces_of_TrueType_collections()
        {
            var regular = File.ReadAllBytes(Path.Combine(NotoFolder, "NotoSans-Regular.ttf"));
            var bold = File.ReadAllBytes(Path.Combine(NotoFolder, "NotoSans-Bold.ttf"));
            File.WriteAllBytes(Path.Combine(_tempFolder, "NotoSans.ttc"), CreateCollection(regular, bold));
            var resolver = new SystemFontResolver([_tempFolder]);

            var info = resolver.ResolveTypeface("Noto Sans", true, false);
            info.Should().NotBeNull();
            info!.FaceName.Should().EndWith("NotoSans.ttc#1");
            info.MustSimulateBold.Should().BeFalse();

            // The extracted font is a valid standalone font with the tables of the original font.
            var extracted = resolver.GetFont(info.FaceName)!;
            FileChecksum(extracted).Should().Be(0xB1B0AFBA, "the checksum adjustment must be recalculated");
            GetTable(extracted, "glyf").SequenceEqual(GetTable(bold, "glyf")).Should().BeTrue();
            GetTable(extracted, "name").SequenceEqual(GetTable(bold, "name")).Should().BeTrue();

            var extractedFile = Path.Combine(_tempFolder, "extracted.ttf");
            File.WriteAllBytes(extractedFile, extracted);
            var face = SystemFontFileReader.ReadFaces(extractedFile, "").Single();
            face.FamilyNames.Should().Contain("Noto Sans");
            face.IsBold.Should().BeTrue();
        }

        [Fact]
        public void Ignores_damaged_files_and_fonts_with_CFF_outlines()
        {
            File.WriteAllBytes(Path.Combine(_tempFolder, "garbage.ttf"), Encoding.ASCII.GetBytes("This is no font file at all."));
            File.WriteAllBytes(Path.Combine(_tempFolder, "empty.ttf"), []);
            var truncated = File.ReadAllBytes(Path.Combine(NotoFolder, "NotoSans-Regular.ttf"))[..100];
            File.WriteAllBytes(Path.Combine(_tempFolder, "truncated.ttf"), truncated);
            var cff = new byte[64];
            Encoding.ASCII.GetBytes("OTTO").CopyTo(cff, 0);
            File.WriteAllBytes(Path.Combine(_tempFolder, "cff.otf"), cff);
            var collection = new byte[16];
            Encoding.ASCII.GetBytes("ttcf").CopyTo(collection, 0);
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(8), 1000);  // 1000 faces, but no data.
            File.WriteAllBytes(Path.Combine(_tempFolder, "damaged.ttc"), collection);
            var resolver = new SystemFontResolver([_tempFolder]);

            resolver.GetFamilyNames().Should().BeEmpty();
            resolver.ResolveTypeface("Noto Sans", false, false).Should().BeNull();
        }

        [Fact]
        public void Indexes_a_font_behind_symbolic_links_once_with_its_real_path()
        {
            var realFolder = Path.Combine(_tempFolder, "real");
            Directory.CreateDirectory(realFolder);
            var realFile = Path.Combine(realFolder, "NotoSans-Regular.ttf");
            File.Copy(Path.Combine(NotoFolder, "NotoSans-Regular.ttf"), realFile);
            try
            {
                // Sorts before the real file, like the aliases some Linux distributions install.
                File.CreateSymbolicLink(Path.Combine(_tempFolder, "a-alias.ttf"), realFile);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return;  // Creating symbolic links needs a privilege on Windows.
            }
            var resolver = new SystemFontResolver([_tempFolder]);

            FileOf(resolver.ResolveTypeface("Noto Sans", false, false)).Should().Be("NotoSans-Regular.ttf");
        }

        [Fact]
        public void Ignores_folders_that_do_not_exist()
        {
            var resolver = new SystemFontResolver([Path.Combine(_tempFolder, "does-not-exist"), NotoFolder]);

            resolver.ResolveTypeface("Noto Sans", false, false).Should().NotBeNull();
        }

        [Fact]
        public void Creates_a_PDF_file_with_fonts_from_a_font_folder_and_a_font_collection()
        {
            var regular = File.ReadAllBytes(Path.Combine(NotoFolder, "NotoSans-Regular.ttf"));
            var bold = File.ReadAllBytes(Path.Combine(NotoFolder, "NotoSans-Bold.ttf"));
            File.WriteAllBytes(Path.Combine(_tempFolder, "NotoSans.ttc"), CreateCollection(regular, bold));
            GlobalFontSettings.FontResolver = new SystemFontResolver([_tempFolder, NotoFolder]);

            var document = new PdfDocument();
            document.Options.CompressContentStreams = false;
            var page = document.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                gfx.DrawString("Regular from the collection", new XFont("Noto Sans", 12), XBrushes.Black, 50, 50);
                gfx.DrawString("Bold from the collection", new XFont("Noto Sans", 12, XFontStyleEx.Bold), XBrushes.Black, 50, 70);
                gfx.DrawString("SemiBold from the folder", new XFont("Noto Sans SemiBold", 12), XBrushes.Black, 50, 90);
            }
            var stream = new MemoryStream();
            document.Save(stream);

            // PDFsharp names the fonts like Microsoft Word: family name, and ",Bold" if the font itself is bold (OS/2 table).
            var pdf = Encoding.Latin1.GetString(stream.ToArray());
            var baseFonts = System.Text.RegularExpressions.Regex.Matches(pdf, @"/BaseFont\s*/(?:[A-Z]{6}\+)?([^\s/<>\[\]]+)")
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .ToList();
            baseFonts.Should().HaveCount(3).And.Contain(["Noto#20Sans#20Regular", "Noto#20Sans,Bold"]);
            pdf.Should().Contain("/FontFile2", "the fonts are embedded");
            // Bold is the real bold face of the collection: PDFsharp simulates bold with text rendering mode 2.
            UsesTextRenderingMode2(pdf).Should().BeFalse();
            using var reopened = PdfReader.Open(new MemoryStream(stream.ToArray()), PdfDocumentOpenMode.Import);
            reopened.PageCount.Should().Be(1);
        }

        [Fact]
        public void Creates_a_PDF_file_with_simulated_bold_if_the_family_has_no_bold_face()
        {
            CopyToTempFolder(NotoFolder, "NotoSans-Regular.ttf");
            GlobalFontSettings.FontResolver = new SystemFontResolver([_tempFolder]);

            var document = new PdfDocument();
            document.Options.CompressContentStreams = false;
            var page = document.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
                gfx.DrawString("Simulated bold", new XFont("Noto Sans", 12, XFontStyleEx.Bold), XBrushes.Black, 50, 50);
            var stream = new MemoryStream();
            document.Save(stream);

            UsesTextRenderingMode2(Encoding.Latin1.GetString(stream.ToArray())).Should().BeTrue("PDFsharp simulates bold with text rendering mode 2");
        }

        [Fact]
        public void Default_font_folders_are_absolute_and_contain_the_fonts_of_this_computer()
        {
            var folders = SystemFontResolver.GetDefaultFontFolders();

            folders.Should().NotBeEmpty();
            folders.Should().OnlyContain(folder => Path.IsPathRooted(folder));

            // Build servers and containers may have no fonts at all, so check this only if the folders contain fonts.
            var hasFontFiles = folders.Where(Directory.Exists).Any(folder =>
                Directory.EnumerateFiles(folder, "*.tt?", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Any());
            if (hasFontFiles)
                new SystemFontResolver().GetFamilyNames().Should().NotBeEmpty();
        }

        // Text rendering mode 2 (fill and stroke) is what PDFsharp uses to simulate bold.
        static bool UsesTextRenderingMode2(string pdf) => System.Text.RegularExpressions.Regex.IsMatch(pdf, @"(?<![\d.])2\s+Tr\b");

        void CopyToTempFolder(string folder, params string[] files)
        {
            foreach (var file in files)
                File.Copy(Path.Combine(folder, file), Path.Combine(_tempFolder, file));
        }

        static string? FileOf(FontResolverInfo? info)
        {
            if (info == null)
                return null;
            var hash = info.FaceName.LastIndexOf('#');
            return Path.GetFileName(hash > 0 && info.FaceName[(hash + 1)..].All(Char.IsAsciiDigit) ? info.FaceName[..hash] : info.FaceName);
        }

        /// <summary>
        /// Creates a TrueType collection. The table offsets of each font are made relative to the start of the collection.
        /// </summary>
        static byte[] CreateCollection(params byte[][] fonts)
        {
            int headerLength = 12 + 4 * fonts.Length;
            var offsets = new int[fonts.Length];
            int position = headerLength;
            for (int idx = 0; idx < fonts.Length; idx++)
            {
                offsets[idx] = position;
                position += (fonts[idx].Length + 3) & ~3;
            }

            var collection = new byte[position];
            Encoding.ASCII.GetBytes("ttcf").CopyTo(collection, 0);
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(4), 0x00010000);
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(8), (uint)fonts.Length);
            for (int idx = 0; idx < fonts.Length; idx++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(12 + 4 * idx), (uint)offsets[idx]);
                fonts[idx].CopyTo(collection, offsets[idx]);
                int tableCount = BinaryPrimitives.ReadUInt16BigEndian(fonts[idx].AsSpan(4));
                for (int table = 0; table < tableCount; table++)
                {
                    var offsetField = collection.AsSpan(offsets[idx] + 12 + 16 * table + 8, 4);
                    BinaryPrimitives.WriteUInt32BigEndian(offsetField, BinaryPrimitives.ReadUInt32BigEndian(offsetField) + (uint)offsets[idx]);
                }
            }
            return collection;
        }

        static byte[] GetTable(byte[] font, string tag)
        {
            int tableCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
            for (int idx = 0; idx < tableCount; idx++)
            {
                var record = font.AsSpan(12 + 16 * idx, 16);
                if (Encoding.ASCII.GetString(record[..4]) == tag)
                {
                    int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
                    int length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
                    return font.AsSpan(offset, length).ToArray();
                }
            }
            throw new InvalidOperationException($"Table '{tag}' not found.");
        }

        static uint FileChecksum(byte[] font)
        {
            uint sum = 0;
            for (int idx = 0; idx + 4 <= font.Length; idx += 4)
                sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(idx)));
            return sum;
        }

        readonly string _tempFolder;
    }
}
