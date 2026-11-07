// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

#if WPF
using System.IO;
#endif
using System.Text;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PdfSharp.Tests.IO
{
    /// <summary>
    /// Opens small hand-made PDF files that try to crash, hang, or exhaust the memory of the reader.
    /// Each file must either open or fail with an exception. None may take the process down.
    /// </summary>
    [Collection("PDFsharp")]
    public class HostilePdfTests : IDisposable
    {
        public HostilePdfTests()
        {
            PdfSharpCore.ResetAll();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        // PdfReader drops objects that are not reachable from the catalog, so the catalog references the test objects 3 to 5.
        const string Catalog = "<< /Type /Catalog /Pages 2 0 R /TestObjects [3 0 R 4 0 R 5 0 R] >>";
        const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

        // ----- Nesting depth ------------------------------------------------------------------------

        [Fact]
        public void Deeply_nested_arrays_throw_instead_of_crashing_the_process()
        {
            // Without a limit, 100,000 nested arrays end in an uncatchable StackOverflowException.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Nested(100_000));

            var open = () => Open(pdf);

            open.Should().Throw<PdfReaderException>().WithMessage("*nested deeper than 256 levels*");
        }

        [Fact]
        public void Deeply_nested_dictionaries_throw_instead_of_crashing_the_process()
        {
            var nested = new StringBuilder();
            for (var i = 0; i < 100_000; i++)
                nested.Append("<< /A ");
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, nested.ToString());

            var open = () => Open(pdf);

            open.Should().Throw<PdfReaderException>().WithMessage("*nested deeper than*");
        }

        [Fact]
        public void Nesting_depth_limit_is_configurable()
        {
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Nested(10));

            // 10 levels are fine with a limit of 10...
            using (var document = Open(pdf, new PdfReaderOptions { MaxNestingDepth = 10 }))
                document.Should().NotBeNull();

            // ...but not with a limit of 9.
            var open = () => Open(pdf, new PdfReaderOptions { MaxNestingDepth = 9 });
            open.Should().Throw<PdfReaderException>().WithMessage("*nested deeper than 9 levels*");

            // Moderate nesting works with the default limit.
            using var document2 = Open(PdfBuilder.WithClassicXRef(Catalog, Pages, Nested(200)));
            document2.Should().NotBeNull();
        }

        [Fact]
        public void MaxNestingDepth_must_be_positive()
        {
            var set = () => new PdfReaderOptions { MaxNestingDepth = 0 };

            set.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Deeply_nested_arrays_in_content_stream_throw_instead_of_crashing_the_process()
        {
            var content = Encoding.ASCII.GetBytes(new string('[', 100_000));

            var read = () => ContentReader.ReadContent(content);

            read.Should().Throw<ContentReaderException>().WithMessage("*nested deeper than*");
        }

        // ----- Cross-reference chain ----------------------------------------------------------------

        [Fact]
        public void Prev_entry_pointing_to_itself_does_not_hang()
        {
            // The trailer’s /Prev points to the cross-reference section it belongs to.
            var pdf = PdfBuilder.WithClassicXRef([Catalog, Pages], trailerExtra: "/Prev {XREF}");

            using var document = OpenWithTimeout(pdf);

            document.PageCount.Should().Be(0);
        }

        [Fact]
        public void Prev_entries_forming_a_loop_do_not_hang()
        {
            // Two cross-reference sections whose /Prev entries point to each other.
            var pdf = PdfBuilder.WithTwoXRefSectionsInALoop(Catalog, Pages);

            using var document = OpenWithTimeout(pdf);

            document.PageCount.Should().Be(0);
        }

        // ----- Stream lengths -----------------------------------------------------------------------

        [Fact]
        public void Huge_Length_does_not_allocate_huge_buffer()
        {
            // A /Length of 2 GB in a tiny file. The reader must not try to allocate 2 GB.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Stream("/Length 2000000000", "Hello"));

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
        }

        [Fact]
        public void Negative_Length_is_handled_like_a_missing_Length()
        {
            // The former implementation threw an OverflowException.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Stream("/Length -5", "Hello"));

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
        }

        [Fact]
        public void Length_referring_to_its_own_stream_object_does_not_crash()
        {
            // Without protection, reading the /Length of object 3 reads object 3 again,
            // which recurses until the process crashes with a StackOverflowException.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Stream("/Length 3 0 R", "Hello"));

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
        }

        [Fact]
        public void Length_referring_to_streams_in_a_cycle_does_not_crash()
        {
            // Object 3 takes its length from object 4 and vice versa.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages,
                Stream("/Length 4 0 R", "Hello"),
                Stream("/Length 3 0 R", "World"));

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
            StreamContent(document, 4).Should().Be("World");
        }

        [Fact]
        public void Length_referring_to_a_non_integer_object_is_handled_like_a_missing_Length()
        {
            // The former implementation threw an InvalidCastException.
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Stream("/Length 4 0 R", "Hello"), "/NotAnInteger");

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
        }

        [Fact]
        public void Valid_indirect_Length_still_works()
        {
            var pdf = PdfBuilder.WithClassicXRef(Catalog, Pages, Stream("/Length 4 0 R", "Hello"), "5");

            using var document = Open(pdf);

            StreamContent(document, 3).Should().Be("Hello");
        }

        // ----- Object streams -----------------------------------------------------------------------

        [Fact]
        public void Object_stream_with_huge_N_throws_instead_of_allocating_huge_array()
        {
            // /N 2147483647 in an object stream of 10 bytes.
            var pdf = PdfBuilder.WithObjectStream(objectStreamDictionary: "/Type /ObjStm /N 2147483647 /First 4",
                objectStreamContent: "5 0 << >>");

            var open = () => Open(pdf);

            open.Should().Throw<Exception>()
                .Where(ex => ContainsPdfReaderExceptionWithMessage(ex, "/N"));
        }

        [Fact]
        public void Valid_object_stream_still_works()
        {
            var pdf = PdfBuilder.WithObjectStream(objectStreamDictionary: "/Type /ObjStm /N 1 /First 4",
                objectStreamContent: "5 0 << /Test (Hello) >>");

            using var document = Open(pdf);

            // PDFsharp renumbers the objects when opening a file, so get object 5 via the catalog.
            var obj = document.Internals.Catalog.Elements.GetArray("/TestObjects")?.Elements.GetDictionary(0);
            obj.Should().NotBeNull();
            obj!.Elements.GetString("/Test").Should().Be("Hello");
        }

        // ----- Helpers ------------------------------------------------------------------------------

        static PdfDocument Open(byte[] pdf, PdfReaderOptions? options = null)
            => PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify, options);

        /// <summary>
        /// Opens the PDF on another thread so that a regression makes the test fail instead of hanging forever.
        /// </summary>
        static PdfDocument OpenWithTimeout(byte[] pdf)
        {
            var task = Task.Run(() => Open(pdf));
            task.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("opening the file must not loop forever");
            return task.Result;
        }

        static string Nested(int depth) => new string('[', depth) + new string(']', depth);

        static string Stream(string dictionaryEntries, string content)
            => $"<< {dictionaryEntries} >>\nstream\n{content}\nendstream";

        static string StreamContent(PdfDocument document, int objectNumber)
        {
            var dict = (PdfDictionary)document.Internals.GetObject(new PdfObjectID(objectNumber))!;
            return Encoding.ASCII.GetString(dict.Stream!.Value).TrimEnd('\r', '\n');
        }

        static bool ContainsPdfReaderExceptionWithMessage(Exception? ex, string text)
        {
            for (; ex != null; ex = ex.InnerException)
            {
                if (ex is PdfReaderException && ex.Message.Contains(text))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Builds minimal PDF files byte by byte, with correct cross-reference offsets.
        /// </summary>
        static class PdfBuilder
        {
            static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

            /// <summary>
            /// Creates a PDF file with a classic cross-reference table.
            /// objects[i] is the content of object i + 1. Object 1 must be the catalog.
            /// The placeholder {XREF} in trailerExtra is replaced by the position of the cross-reference table.
            /// </summary>
            public static byte[] WithClassicXRef(string[] objects, string trailerExtra = "")
            {
                var file = new StringBuilder("%PDF-1.7\n");
                var offsets = AppendObjects(file, objects, 1);
                var xrefPosition = file.Length;
                AppendXRefTable(file, offsets);
                file.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R {trailerExtra.Replace("{XREF}", xrefPosition.ToString())} >>\n");
                file.Append($"startxref\n{xrefPosition}\n%%EOF\n");
                return Latin1.GetBytes(file.ToString());
            }

            public static byte[] WithClassicXRef(params string[] objects) => WithClassicXRef(objects, "");

            /// <summary>
            /// Creates a PDF file with two cross-reference sections whose /Prev entries point to each other.
            /// </summary>
            public static byte[] WithTwoXRefSectionsInALoop(params string[] objects)
            {
                var file = new StringBuilder("%PDF-1.7\n");
                var offsets = AppendObjects(file, objects, 1);

                // Section A: The complete table. Its /Prev points to section B.
                var positionA = file.Length;
                AppendXRefTable(file, offsets);
                // Positions are written with 10 digits, so the length of the text does not depend on their values.
                const string placeholderB = "@@@@@@@@@@";  // Exactly 10 characters, like the value replacing it.
                file.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Prev {placeholderB} >>\n");

                // Section B: An empty update. Its /Prev points to section A.
                var positionB = file.Length;
                file.Append("xref\n0 1\n0000000000 65535 f \n");
                file.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Prev {positionA:D10} >>\n");
                file.Append($"startxref\n{positionB}\n%%EOF\n");

                file.Replace(placeholderB, positionB.ToString("D10"));
                return Latin1.GetBytes(file.ToString());
            }

            /// <summary>
            /// Creates a PDF file with a cross-reference stream and one object stream (object 3)
            /// that contains object 5. Object 4 is the cross-reference stream.
            /// </summary>
            public static byte[] WithObjectStream(string objectStreamDictionary, string objectStreamContent)
            {
                var file = new StringBuilder("%PDF-1.7\n");
                var offsets = AppendObjects(file,
                [
                    "<< /Type /Catalog /Pages 2 0 R /TestObjects [5 0 R] >>",
                    Pages,
                    $"<< {objectStreamDictionary} /Length {objectStreamContent.Length} >>\nstream\n{objectStreamContent}\nendstream"
                ], 1);

                // The cross-reference stream with /W [1 4 2], i.e. 7 bytes per entry.
                var xrefPosition = file.Length;
                var entries = new StringBuilder();
                AppendEntry(entries, 0, 0, 65535);           // 0: free
                AppendEntry(entries, 1, offsets[0], 0);      // 1: catalog
                AppendEntry(entries, 1, offsets[1], 0);      // 2: pages
                AppendEntry(entries, 1, offsets[2], 0);      // 3: object stream
                AppendEntry(entries, 1, xrefPosition, 0);    // 4: cross-reference stream
                AppendEntry(entries, 2, 3, 0);               // 5: object 0 in object stream 3
                file.Append($"4 0 obj\n<< /Type /XRef /Size 6 /W [1 4 2] /Root 1 0 R /Length {entries.Length} >>\nstream\n");
                file.Append(entries);
                file.Append("\nendstream\nendobj\n");
                file.Append($"startxref\n{xrefPosition}\n%%EOF\n");
                return Latin1.GetBytes(file.ToString());

                static void AppendEntry(StringBuilder sb, int type, int field2, int field3)
                {
                    sb.Append((char)type);
                    for (var shift = 24; shift >= 0; shift -= 8)
                        sb.Append((char)((field2 >> shift) & 0xFF));
                    sb.Append((char)((field3 >> 8) & 0xFF));
                    sb.Append((char)(field3 & 0xFF));
                }
            }

            static List<int> AppendObjects(StringBuilder file, string[] objects, int firstObjectNumber)
            {
                var offsets = new List<int>();
                for (var i = 0; i < objects.Length; i++)
                {
                    offsets.Add(file.Length);
                    file.Append($"{firstObjectNumber + i} 0 obj\n{objects[i]}\nendobj\n");
                }
                return offsets;
            }

            static void AppendXRefTable(StringBuilder file, List<int> offsets)
            {
                file.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
                foreach (var offset in offsets)
                    file.Append($"{offset:D10} 00000 n \n");
            }
        }
    }
}
