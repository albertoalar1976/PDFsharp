// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf;
using PdfSharp.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Quality;
using Xunit;
using FluentAssertions;
using PdfSharp.Quality.Testing;

namespace PdfSharp.Tests.Pdf.Objects
{
    // TODO: Replace obsolete properties with new properties.

    [Collection("PDFsharp")]
    public class PdfPageTests : PdfSharpTestBase
    {
        readonly string _tempRoot = "unit-tests/" + typeof(PdfPageTests).Namespace + "/";

        public PdfPageTests()
        {
            PdfSharpCore.ResetAll();
            GlobalFontSettings.FontResolver = new UnitTestFontResolver();
        }

        protected override void Dispose(bool _)
        {
            PdfSharpCore.ResetAll();
        }

        [Fact]
        public void Test_create_CropBox()
        {
            var document = new PdfDocument();
            document.Info.Title = nameof(Test_create_CropBox);

            var page = document.AddPage();

            var mediaBox = page.MediaBox;
            mediaBox.Should().NotBeNull();
            mediaBox.Should().NotBe(XRect.Empty);
            mediaBox.IsZero.Should().BeFalse();
            // Get an XGraphics object for drawing on this page.
            var gfx = XGraphics.FromPdfPage(page);

            // Draw two lines with a red default pen.
            var width = page.Width.Point;
            var height = page.Height.Point;
            gfx.DrawLine(XPens.Red, 0, 0, width, height);
            gfx.DrawLine(XPens.Red, width, 0, 0, height);

            // Draw a circle with a red pen which is 1.5 point thick.
            var r = width / 6;
            gfx.DrawEllipse(new XPen(XColors.Red, 1.5), XBrushes.White,
                new XRect(width / 2 - r, height / 2 - r, 2 * r, 2 * r));

            // Create a font.
            var font = new XFont("Times New Roman", 10, XFontStyleEx.BoldItalic);

            // Draw the text.
            gfx.DrawString("Hello, World!", font, XBrushes.Black,
                new XRect(0, 0, width, height), XStringFormats.Center);

            // Save the document…
            string filename = PdfFileUtility.GetTempPdfFullFileName(_tempRoot+nameof(Test_create_CropBox));
            document.Save(filename);
            // … and start a viewer.
            PdfFileUtility.ShowDocumentIfDebugging(filename);
        }

        [Fact]
        public void Test_create_all_boxes()
        {
            var document = new PdfDocument();
            document.Info.Title =nameof(Test_create_all_boxes);

            var page = document.AddPage();
            var pageWidth = page.Width.Point;
            var pageHeight = page.Height.Point;
            var pageRectangle = new PdfRectangle(0, 0, pageWidth, pageHeight);
            var defaultRectangle = new PdfRectangle();

            // Get an XGraphics object for drawing on this page.
            var gfx = XGraphics.FromPdfPage(page);

            // Draw two lines with a red default pen.
            var width = page.Width.Point;
            var height = page.Height.Point;
            gfx.DrawLine(XPens.Red, 0, 0, width, height);
            gfx.DrawLine(XPens.Red, width, 0, 0, height);

            // Draw a circle with a red pen which is 1.5 point thick.
            var r = width / 6;
            gfx.DrawEllipse(new XPen(XColors.Red, 1.5), XBrushes.White,
                new XRect(width / 2 - r, height / 2 - r, 2 * r, 2 * r));

            // Create a font.
            var font = new XFont("Times New Roman", 10, XFontStyleEx.BoldItalic);

            // Draw the text.
            gfx.DrawString("Hello, World!", font, XBrushes.Black,
                new XRect(0, 0, width, height), XStringFormats.Center);

            // Save the document…
            string filename = PdfFileUtility.GetTempPdfFullFileName(_tempRoot + nameof(Test_create_all_boxes));
            document.Save(filename);
            // … and start a viewer.
            PdfFileUtility.ShowDocumentIfDebugging(filename);
        }
    }
}
