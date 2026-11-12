// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Quality;

namespace PdfSharp.Snippets.Drawing
{
    public class PathText : Snippet
    {
        // Some chars with ascenders and descenders.
        const string TestChars = " Îygp";

        public PathText()
        {
            Title = "Text as Path";
        }

        public override void RenderSnippet(XGraphics gfx)
        {
            // Glyph as path.
            BeginBox(gfx, 1, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);

            // Glyph as path with alignment at rect.
            BeginBox(gfx, 2, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);

            // Glyph as path with alignment at point (top).
            BeginBox(gfx, 3, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);

            // Glyph as path with alignment at point (center).
            BeginBox(gfx, 4, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);

            // Glyph as path with alignment at point (bottom).
            BeginBox(gfx, 5, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);

            // Glyph as path with alignment at point (baseLine).
            BeginBox(gfx, 6, BoxOptions.Tile);
            {
                DrawNotSupportedFeature(gfx);
            }
            EndBox(gfx);
        }

        static readonly XRect RectCenterVLine = new XRect(0, BoxHeight / 2, BoxWidth, 0);
    }
}
