// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using System.Text;
using FluentAssertions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using Xunit;

namespace MigraDoc.DocumentObjectModel.Tests
{
    [Collection("PDFsharp")]
    public class CloneAndParserLimitTests
    {
        [Fact]
        public void Cloned_hyperlink_has_its_own_font()
        {
            var paragraph = new Document().AddSection().AddParagraph();
            var hyperlink = paragraph.AddHyperlink("target");
            hyperlink.Font.Bold = true;

            var clone = hyperlink.Clone();
            clone.Font.Bold = false;

            // The former implementation shared the font, so changing the clone changed the original.
            hyperlink.Font.Bold.Should().BeTrue();
            clone.Font.Should().NotBeSameAs(hyperlink.Font);
            clone.Font.Parent.Should().BeSameAs(clone);
        }

        [Fact]
        public void Cloned_document_has_its_own_embedded_files()
        {
            var document = new Document();
            document.AddSection().AddParagraph("Text");
            document.AddEmbeddedFile("first", "first.txt");

            var clone = document.Clone();
            clone.AddEmbeddedFile("second", "second.txt");

            document.EmbeddedFiles.Count.Should().Be(1);
            clone.EmbeddedFiles.Count.Should().Be(2);
        }

        [Fact]
        public void Deeply_nested_formatted_text_in_DDL_does_not_crash_the_process()
        {
            // Without a limit, 100,000 nested '\bold{' end in an uncatchable StackOverflowException.
            const int depth = 100_000;
            var ddl = new StringBuilder(@"\document{\section{\paragraph{");
            for (var i = 0; i < depth; i++)
                ddl.Append(@"\bold{");
            ddl.Append('x');
            ddl.Append('}', depth);
            ddl.Append("}}}");

            var errors = new DdlReaderErrors();
            try
            {
                new DdlReader(new StringReader(ddl.ToString()), errors).ReadDocument();
            }
            catch (Exception)
            {
                // Reading malformed DDL may end with an exception, as before. It must not crash the process.
                // The error recovery (AdjustToNextBlock) was recursive, too, and overflowed the stack.
            }

            errors.Cast<DdlReaderError>().Should().Contain(e => e.ErrorMessage.Contains("nested deeper than 256 levels"));
        }
    }
}
