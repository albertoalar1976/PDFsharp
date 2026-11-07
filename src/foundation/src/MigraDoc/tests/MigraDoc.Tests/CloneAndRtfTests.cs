// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;
using PdfSharp.Diagnostics;
#if CORE
using PdfSharp.Fonts;
using PdfSharp.Quality;
#endif
using Xunit;
using FluentAssertions;

namespace MigraDoc.Tests
{
    [Collection("PDFsharp")]
    public class CloneAndRtfTests : IDisposable
    {
        public CloneAndRtfTests()
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

        [Fact]
        public void Clone_of_a_rendered_document_can_be_rendered()
        {
            // BindToRenderer tells users to render a clone if a document was already rendered.
            // But the clone was still bound to the first renderer and rendering it threw.
            var document = new Document();
            document.AddSection().AddParagraph("Hello");
            new PdfDocumentRenderer { Document = document }.RenderDocument();

            var clone = document.Clone();
            var renderer = new PdfDocumentRenderer { Document = clone };
            var render = () => renderer.RenderDocument();

            render.Should().NotThrow();
            renderer.PdfDocument.PageCount.Should().Be(1);
        }

        [Fact]
        public void Rtf_writes_unicode_escapes_as_signed_16_bit_numbers()
        {
            // RTF parameters are signed 16-bit integers. U+8A9E must be written with the parameter -30050,
            // not 35486. U+4E2D is below U+8000 and keeps the positive parameter 20013.
            var document = new Document();
            document.AddSection().AddParagraph("中語");

            var rtf = new RtfDocumentRenderer().RenderToString(document, "");

            const string Backslash = "\\";
            rtf.Should().Contain(Backslash + "u20013?").And.Contain(Backslash + "u-30050?").And.NotContain(Backslash + "u35486");
        }
    }
}
