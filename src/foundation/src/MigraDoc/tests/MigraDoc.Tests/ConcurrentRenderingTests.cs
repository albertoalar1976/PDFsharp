// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

#if WPF
using System.IO;
#endif
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
    /// <summary>
    /// Server applications (ASP.NET Core, Blazor Server) create documents for several requests at the same time.
    /// Each request uses its own renderer, so the renderers must not share mutable state.
    /// </summary>
    [Collection("PDFsharp")]
    public class ConcurrentRenderingTests : IDisposable
    {
        public ConcurrentRenderingTests()
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
        public void Rtf_list_numbering_does_not_leak_into_the_next_document()
        {
            // The list numbering state was static. RtfDocumentRenderer reset it before each document,
            // but forgot the previous list. A document whose first list continues the previous list
            // then referred to a list of the document rendered before, which its RTF does not define.
            var alone = new RtfDocumentRenderer().RenderToString(CreateDocumentWithContinuedList(), "");

            new RtfDocumentRenderer().RenderToString(CreateDocumentWithLists(3), "");
            var afterAnotherDocument = new RtfDocumentRenderer().RenderToString(CreateDocumentWithContinuedList(), "");

            afterAnotherDocument.Should().Contain(@"\listsimple", "the list must be defined in the list table of its own document");
            afterAnotherDocument.Should().Be(alone);
        }

        [Fact]
        public void Rtf_rendering_in_parallel_gives_the_same_result_as_sequential_rendering()
        {
            const int documentCount = 8;
            var expected = Enumerable.Range(0, documentCount)
                .Select(i => new RtfDocumentRenderer().RenderToString(CreateDocumentWithLists(i + 1), ""))
                .ToArray();

            var actual = new string[documentCount * 8];
            Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                i => actual[i] = new RtfDocumentRenderer().RenderToString(CreateDocumentWithLists(i % documentCount + 1), ""));

            for (var i = 0; i < actual.Length; i++)
                actual[i].Should().Be(expected[i % documentCount], $"document {i % documentCount} rendered in parallel must not differ");
        }

        [Fact]
        public void Pdf_rendering_in_parallel_gives_the_same_result_as_sequential_rendering()
        {
            // The documents switch fonts with every paragraph, so the renderer converts MigraDoc fonts
            // to XFonts all the time. FontHandler cached the last conversion in two static fields that were
            // written one after the other, so a parallel rendering could pick up the XFont of another document.
            const int documentCount = 6;
            var expected = Enumerable.Range(0, documentCount)
                .Select(i => RenderPdfContent(CreateDocumentWithFonts(i)))
                .ToArray();

            var actual = new byte[documentCount * 4][];
            Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                i => actual[i] = RenderPdfContent(CreateDocumentWithFonts(i % documentCount)));

            for (var i = 0; i < actual.Length; i++)
                actual[i].Should().Equal(expected[i % documentCount], $"document {i % documentCount} rendered in parallel must not differ");
        }

        static Document CreateDocumentWithLists(int listCount)
        {
            var document = new Document();
            var section = document.AddSection();
            for (var list = 0; list < listCount; list++)
            {
                section.AddParagraph($"List {list + 1}");
                for (var item = 0; item < 3; item++)
                {
                    var paragraph = section.AddParagraph($"Item {item + 1} of list {list + 1}");
                    paragraph.Format.ListInfo.ListType = (list % 2) == 0 ? ListType.BulletList1 : ListType.NumberList1;
                    paragraph.Format.ListInfo.ContinuePreviousList = item > 0;
                }
            }
            return document;
        }

        static Document CreateDocumentWithContinuedList()
        {
            var document = new Document();
            var section = document.AddSection();
            var paragraph = section.AddParagraph("The first list of this document continues a list that does not exist.");
            paragraph.Format.ListInfo.ListType = ListType.NumberList1;
            paragraph.Format.ListInfo.ContinuePreviousList = true;
            return document;
        }

        static Document CreateDocumentWithFonts(int variant)
        {
            string[] fontNames = ["Arial", "Times New Roman", "Courier New", "Verdana"];
            var document = new Document();
            var section = document.AddSection();
            for (var i = 0; i < 60; i++)
            {
                var paragraph = section.AddParagraph($"Paragraph {i} of variant {variant}: The quick brown fox jumps over the lazy dog.");
                paragraph.Format.Font.Name = fontNames[(i + variant) % fontNames.Length];
                paragraph.Format.Font.Size = 8 + (i * 3 + variant) % 9;
                paragraph.Format.Font.Bold = (i + variant) % 3 == 0;
            }
            return document;
        }

        /// <summary>
        /// Renders the document and returns the content streams of all pages. Unlike the PDF file,
        /// the content streams do not contain time stamps or IDs, so they can be compared.
        /// </summary>
        static byte[] RenderPdfContent(Document document)
        {
            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            using var content = new MemoryStream();
            foreach (var page in renderer.PdfDocument.Pages)
            {
                var bytes = page.Contents.CreateSingleContent().Stream!.Value;
                content.Write(bytes, 0, bytes.Length);
            }
            return content.ToArray();
        }
    }
}
