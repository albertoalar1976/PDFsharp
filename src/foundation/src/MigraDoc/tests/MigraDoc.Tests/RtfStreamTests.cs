// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

#if WPF
using System.IO;
#endif
using System.IO.Compression;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.RtfRendering;
using Xunit;
using FluentAssertions;

namespace MigraDoc.Tests
{
    [Collection("PDFsharp")]
    public class RtfStreamTests
    {
        [Fact]
        public void Rtf_can_be_rendered_to_a_stream_that_cannot_seek()
        {
            // GZipStream cannot seek. RtfDocumentRenderer reset Stream.Position after rendering
            // and threw a NotSupportedException for such streams.
            var document = new Document();
            document.AddSection().AddParagraph("Hello, RTF!");

            var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                new RtfDocumentRenderer().Render(document, gzip, closeStream: false, "");
            }

            compressed.Position = 0;
            using var reader = new StreamReader(new GZipStream(compressed, CompressionMode.Decompress), Encoding.Default);
            var rtf = reader.ReadToEnd();
            rtf.Should().StartWith(@"{\rtf1").And.Contain("Hello, RTF!");
        }
    }
}
