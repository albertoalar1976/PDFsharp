// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Fonts;
using PdfSharp.Quality;
using Xunit;

namespace PdfSharp.Tests.Basics
{
    /// <summary>
    /// PDF files must not depend on the culture of the machine or thread that creates them.
    /// </summary>
    [Collection("PDFsharp")]
    public class CultureIndependenceTests : IDisposable
    {
        public CultureIndependenceTests()
        {
            PdfSharpCore.ResetAll();
            GlobalFontSettings.FontResolver = new UnitTestFontResolver();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        [Theory]
        [InlineData("th-TH")]  // Thai Buddhist calendar
        [InlineData("ar-SA")]  // Hijri calendar
        [InlineData("tr-TR")]
        [InlineData("es-ES")]
        public void PdfDate_uses_the_gregorian_calendar(string culture)
        {
            using var _ = new CultureScope(culture);

            var date = new PdfDate(new DateTimeOffset(2026, 11, 7, 10, 30, 0, TimeSpan.FromHours(1)));

            // The former implementation wrote D:25691107103000 under th-TH and D:14480527103000 under ar-SA.
            date.ToString().Should().Be("D:20261107103000+01'00'");
        }

        [Fact]
        public void XUnit_parses_upper_case_units_under_Turkish_culture()
        {
            // Under tr-TR, "IN".ToLower() is "ın" (dotless i), so the unit was not recognized.
            using var _ = new CultureScope("tr-TR");

            XUnit.Parse("2IN").Inch.Should().Be(2);
            XUnit.Parse("1.5in").Inch.Should().Be(1.5);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("ar-SA")]
        [InlineData("tr-TR")]
        public void Page_content_does_not_depend_on_culture(string culture)
        {
            var expected = CreatePageContent();

            byte[] actual;
            using (new CultureScope(culture))
                actual = CreatePageContent();

            actual.Should().Equal(expected);
        }

        static byte[] CreatePageContent()
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                var font = new XFont("Arial", 10.5);
                gfx.DrawString("Value 1.5 at 10.25/20.75", font, XBrushes.Black, 10.25, 20.75);
                gfx.DrawLine(new XPen(XColors.Red, 0.333), 0.5, 1.5, 100.125, 200.875);
                gfx.DrawRectangle(XBrushes.Blue, new XRect(12.5, 30.25, 40.5, 10.75));
            }
            return page.Contents.CreateSingleContent().Stream!.Value;
        }

        /// <summary>
        /// Sets the culture of the current thread and restores it when disposed.
        /// </summary>
        sealed class CultureScope : IDisposable
        {
            public CultureScope(string culture)
            {
                _culture = CultureInfo.CurrentCulture;
                _uiCulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture = _culture;
                CultureInfo.CurrentUICulture = _uiCulture;
            }

            readonly CultureInfo _culture;
            readonly CultureInfo _uiCulture;
        }
    }
}
