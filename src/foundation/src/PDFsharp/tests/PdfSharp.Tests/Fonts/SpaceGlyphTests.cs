// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
#if CORE
using PdfSharp.Quality;
#endif
using Xunit;

namespace PdfSharp.Tests.Fonts
{
    [Collection("PDFsharp")]
    public class SpaceGlyphTests : IDisposable
    {
        public SpaceGlyphTests()
        {
            PdfSharpCore.ResetAll();
#if CORE
            GlobalFontSettings.FontResolver = new UnitTestFontResolver();
#endif
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        [Theory]
        [InlineData("Arial")]
        [InlineData("Courier New")]
        [InlineData("Times New Roman")]
        public void Space_separators_never_map_to_the_missing_glyph(string familyName)
        {
            // Many fonts lack glyphs for special spaces like U+202F, which ICU uses in en-US times
            // ("4:05:06 AM"). Formerly, such spaces were rendered as the 'missing glyph' box.
            var font = new XFont(familyName, 10);
            var spaces = Enumerable.Range(0x2000, 0x3001 - 0x2000)
                .Where(cp => CharUnicodeInfo.GetUnicodeCategory((char)cp) == UnicodeCategory.SpaceSeparator)
                .Select(cp => (char)cp)
                .ToArray();
            spaces.Should().Contain((char)0x202F);

            foreach (var space in spaces)
            {
                var pairs = GlyphHelper.GlyphIndicesFromString("a" + space + "b", font);
                pairs[1].GlyphIndex.Should().NotBe(0, $"U+{(int)space:X4} must be rendered as a space");
            }
        }
    }
}
