// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using PdfSharp.Internal.OpenType;
using PdfSharp.Drawing;

namespace PdfSharp.Fonts.Internal
{
    /// <summary>
    /// A bunch of internal functions that do not have a better place.
    /// </summary>
    static class FontHelper
    {
        /// <summary>
        /// Measure string directly from font data.
        /// This function expects that the code run is ready to be measured.
        /// The RenderEvent is not invoked.
        /// </summary>
        public static XSize MeasureString(CodePointGlyphIndexPair[] codeRun, XFont font)
        {
            var length = codeRun.Length;
            if (length == 0)
                return new(); // not XSize.Empty !

            var descriptor = font.OpenTypeDescriptor;
            var size = new XSize
            {
                // Height is the sum of ascender and descender.
                Height = (descriptor.Ascender + descriptor.Descender) * font.Size / font.UnitsPerEm
            };
            Debug.Assert(descriptor.Ascender > 0, "Ascender must be greater than 0.");

            int width = 0;
            //var items = codeRun..Items;
            for (int idx = 0; idx < length; idx++)
            {
                var glyphIndex = codeRun[idx].GlyphIndex;
                width += descriptor.GlyphIndexToWidth(glyphIndex);
            }
            size.Width = width * font.Size / descriptor.UnitsPerEm;

            // Adjust bold simulation.
            if ((font.GlyphTypeface.StyleSimulations & XStyleSimulations.BoldSimulation) == XStyleSimulations.BoldSimulation)
            {
                // Add 2% of the em-size for each character.
                // Unsure how to deal with white-space. Currently count as regular character.
                size.Width += length * font.Size * Const.BoldEmphasis;
            }
            return size;
        }

        /// <summary>
        /// Creates a typeface from XFontStyleEx.
        /// </summary>
        public static XTypeface CreateTypeface(XFontFamily family, XFontStyleEx style)
        {
            // Does not work with fonts that have others than the four default styles.
            XFontStyle fontStyle = XFontStyle.FromGdiFontStyle((XFontStyleEx)style);
            XFontWeight fontWeight = XFontWeight.FromGdiFontStyle((XFontStyleEx)style);
            var typeface = new XTypeface(family, fontStyle, fontWeight, XFontStretches.Normal);
            return typeface;
        }

        public static XFontStyleEx CreateStyle(bool isBold, bool isItalic)
            => (isBold ? XFontStyleEx.Bold : 0) | (isItalic ? XFontStyleEx.Italic : 0);

        public static int CountGlyphs(XFont font)
        {
            int counter = 0;
            for (int codePoint = 0; codePoint < 0x10FFFF; codePoint++)
            {
                // Skip surrogates.
                if (codePoint is >= 0xD000 and <= 0xDFFF)
                    continue;

                var glyphIndex = GlyphHelper.GlyphIndexFromCodePoint(codePoint, font);
                if (glyphIndex != 0)
                    counter++;
            }
            return counter;
        }
    }
}
