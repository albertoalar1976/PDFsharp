// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using System.Text;
using FluentAssertions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Diagnostics;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality;
using Xunit;

namespace MigraDoc.Tests
{
    [Collection("PDFsharp")]
    public class EmbeddedFileTests : IDisposable
    {
        public EmbeddedFileTests()
        {
            PdfSharpCore.ResetAll();
            GlobalFontSettings.FontResolver = new UnitTestFontResolver();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        [Fact]
        public void Embedded_files_are_written_to_the_PDF_file_and_closed_after_reading()
        {
            var content = Encoding.UTF8.GetBytes("Invoice data: 42 items, 1234.56 EUR");
            var path = Path.Combine(Path.GetTempPath(), $"MigraDoc-embedded-{Guid.NewGuid():N}.txt");
            File.WriteAllBytes(path, content);

            var document = new Document();
            document.AddSection().AddParagraph("A document with an embedded file.");
            document.EmbeddedFiles.Add("invoice-data.txt", path);
            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();
            var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);

            // The file is closed after it was read. Formerly, it stayed open, so it could not be deleted on Windows.
            var delete = () => File.Delete(path);
            delete.Should().NotThrow();

            using var pdf = PdfReader.Open(new MemoryStream(stream.ToArray()), PdfDocumentOpenMode.Import);
            var embedded = pdf.Internals.GetAllObjects().OfType<PdfDictionary>()
                .Where(dict => dict.Elements.GetName("/Type") == "/EmbeddedFile")
                .ToList();
            embedded.Should().ContainSingle();
            embedded[0].Stream!.UnfilteredValue.Should().Equal(content);
            Encoding.Latin1.GetString(stream.ToArray()).Should().Contain("invoice-data.txt");
        }
    }
}
