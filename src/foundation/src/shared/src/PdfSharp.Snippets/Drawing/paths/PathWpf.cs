// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Windows;
using PdfSharp.Drawing;
using PdfSharp.Quality;

namespace PdfSharp.Snippets.Drawing
{
    public class PathWpf : Snippet
    {
        public PathWpf()
        {
            Title = "Path WPF";
        }

        public override void RenderSnippet(XGraphics gfx)
        {
            // Draw rectangle via path.
            BeginBox(gfx, 1, BoxOptions.Tile, "Core");
            {
                var path = new XGraphicsPath();
                path.AddRectangle(20, 20, 184, 104);
                gfx.DrawPath(XPens.DarkBlue, XBrushes.DarkOrange, path);
            }
            EndBox(gfx);

            // Draw rectangle via path.
            BeginBox(gfx, 2, BoxOptions.Tile, "WPF PathGeometry");
            {
            }
            EndBox(gfx);

            // Draw pentagram via path.
            BeginBox(gfx, 3, BoxOptions.Tile, "Core");
            {
                var path = new XGraphicsPath();
                path.AddPolygon(GetPentagram(52, BoxCenter));
                path.FillMode = XFillMode.Alternate;
                gfx.DrawPath(XPens.Black, XBrushes.Gray, path);
            }
            EndBox(gfx);

            // Draw pentagram via path.
            BeginBox(gfx, 4, BoxOptions.Tile, "WPF PathGeometry");
            {
            }
            EndBox(gfx);

            // 
            BeginBox(gfx, 5, BoxOptions.Tile);
            {
            }
            EndBox(gfx);

            // 
            BeginBox(gfx, 6, BoxOptions.Tile);
            {
            }
            EndBox(gfx);

            //
            BeginBox(gfx, 7, BoxOptions.Tile);
            {
            }
            EndBox(gfx);

            //
            BeginBox(gfx, 8, BoxOptions.Tile);
            {
            }
            EndBox(gfx);
        }
    }
}
