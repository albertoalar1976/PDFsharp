// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

// Show box.
#define BOX1
#define BOX2
#define BOX3
#define BOX4
#define BOX5
#define BOX6_
#define BOX7_
#define BOX8_

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Drawing;
using PdfSharp.Drawing;
using PdfSharp.Internal;
using PdfSharp.Pdf;
using PdfSharp.Quality;

namespace PdfSharp.Snippets.Font
{
    public class PrivateFontCollection1Snippet : Snippet
    {
#if true
        readonly XPdfFontOptions _fontOptions = new XPdfFontOptions(PdfFontEncoding.WinAnsi);
#else
        readonly XPdfFontOptions _fontOptions = new XPdfFontOptions(PdfFontEncoding.Unicode);
#endif

        const string FamilyNameA = "Arial";
        const string FamilyNameT = "Tahoma";
        const double EmSize = 12;

        public PrivateFontCollection1Snippet()
        {
            //NoText = true;
            //Cleanroom = true;
            Title = "Private Font Selection (GDI only)";
            NoText = true;
        }

        public override void RenderSnippet(XGraphics gfx)
        {
        }
        internal static double GetDescent(XFont font)
        {
            double descent = font.Metrics.Descent;
            descent *= font.Size;
            descent /= font.FontFamily.GetEmHeight(font.Style);
            return descent;
        }
    }
}
