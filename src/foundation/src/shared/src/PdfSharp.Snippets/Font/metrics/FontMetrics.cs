// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Resources;
using PdfSharp.Drawing;
using PdfSharp.Quality;

namespace PdfSharp.Snippets.Font
{
    public class FontMetrics : Feature
    {
        public static void TestFontMetrics()
        {
            var fonts = new List<string> { "Segoe WP", "Calibri", "Arial", "Times New Roman", "Segoe UI", "Consolas", "Verdana" };
            var systemFonts = new List<string>();
            var skippedFonts = new List<string>();

            // Load all system fonts.

            // Check fonts.
            bool allFonts = true;
            foreach (var fontName in systemFonts)
            {
                var emHeightPix = 16;
                XFont xFont;
                try
                {
                    xFont = new XFont(fontName, emHeightPix);
                }
                catch (Exception)
                {
                    if (!allFonts)
                        throw;
                    else // Suppress exceptions, if checking all system fonts, because of font containers, that are not supported. Add font to skippedFonts instead.
                    {
                        skippedFonts.Add(fontName);
                        continue;
                    }
                }

                // Basic PdfSharp metrics in xFont.Metrics.
                var xEmHeight = xFont.Metrics.UnitsPerEm;
                var xAscent = xFont.Metrics.Ascent;
                var xDescent = xFont.Metrics.Descent;
                var xLineSpacing = xFont.Metrics.LineSpacing;

                // Basic PdfSharp metrics from xFonts.FontFamily methods.
                var xfEmHeight = xFont.FontFamily.GetEmHeight(xFont.Style);
                var xfAscent = xFont.FontFamily.GetCellAscent(xFont.Style);
                var xfDescent = xFont.FontFamily.GetCellDescent(xFont.Style);
                var xflineSpacing = xFont.FontFamily.GetLineSpacing(xFont.Style);

                Debug.Assert(xEmHeight == xfEmHeight);
                Debug.Assert(xAscent == xfAscent);
                Debug.Assert(xDescent == xfDescent);
                Debug.Assert(xLineSpacing == xflineSpacing);

                // Calculate and check metrics relationships.
                var xCellHeight = xAscent + xDescent;
                var xInternalLeading = xCellHeight - xEmHeight;
                var xExternalLeading = xLineSpacing - xCellHeight;

                Debug.Assert(xLineSpacing == xCellHeight + xExternalLeading);
                Debug.Assert(xCellHeight == xInternalLeading + xEmHeight);
                Debug.Assert(xCellHeight == xAscent + xDescent);

                // Get design unit to pixel factor.
                var xEmHeightPix = xFont.Size;
                Debug.Assert(Math.Abs(xEmHeightPix - (xFont.Size)) < 0.00001);
                var xDesignToPixels = xEmHeightPix / xEmHeight;

                // Calculate Metrics in pixels and check relationships.
                var xAscentPix = xAscent * xDesignToPixels;
                var xDescentPix = xDescent * xDesignToPixels;
                var xCellHeightPix = xCellHeight * xDesignToPixels;
                var xInternalLeadingPix = xInternalLeading * xDesignToPixels;
                var xExternalLeadingPix = xExternalLeading * xDesignToPixels;
                var xLineSpacingPix = xLineSpacing * xDesignToPixels;

                Debug.Assert(Math.Abs(xLineSpacingPix - (xCellHeightPix + xExternalLeadingPix)) < 0.00001);
                Debug.Assert(Math.Abs(xCellHeightPix - (xInternalLeadingPix + xEmHeightPix)) < 0.00001);
                Debug.Assert(Math.Abs(xCellHeightPix - (xAscentPix + xDescentPix)) < 0.00001);

                // Returns the line spacing, in pixels, of this font. The line spacing is the vertical distance
                // between the base lines of two consecutive lines of text. Thus, the line spacing includes the
                // blank space between lines along with the height of the character itself.
                //   The Font::GetHeight method gets the line spacing, in pixels, of this font. 
                //   The line spacing is the vertical distance between the base lines of two consecutive lines of text.
                //   Thus, the line spacing includes the blank space between lines along with the height of the character itself.
                //   https://msdn.microsoft.com/en-us/library/windows/desktop/ms534437%28v=vs.85%29.aspxvar lineSpacing = xfont.GetHeight();
                var xGetLineSpacingPix = xFont.GetHeight();

                // xFont.GetHeight should match the calculated line spacing in pixels.
                Debug.Assert(Math.Abs(xLineSpacingPix - xGetLineSpacingPix) < 0.00001);

            }

            Debug.Assert(skippedFonts.Count == 0);
        }
    }
}
