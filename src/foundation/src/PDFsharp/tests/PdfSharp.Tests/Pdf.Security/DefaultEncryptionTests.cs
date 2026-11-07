// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Text;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PdfSharp.Tests.Pdf.Security
{
    [Collection("PDFsharp")]
    public class DefaultEncryptionTests : IDisposable
    {
        public DefaultEncryptionTests()
        {
            PdfSharpCore.ResetAll();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        [Fact]
        public void Default_encryption_is_AES_256()
        {
            // Setting a password without choosing a method uses V5 (AES-256, revision 6) now.
            // The former default V4 derives the key with MD5/RC4, which PDF 2.0 deprecates.
            var document = new PdfDocument();
            document.AddPage();
            document.SecuritySettings.UserPassword = "user";
            document.SecuritySettings.OwnerPassword = "owner";

            var stream = new MemoryStream();
            document.Save(stream);
            var pdf = Encoding.ASCII.GetString(stream.ToArray());

            pdf.Should().StartWith("%PDF-2.0");
            pdf.Should().Contain("/V 5").And.Contain("/R 6");

            using var reopened = PdfReader.Open(new MemoryStream(stream.ToArray()), "user", PdfDocumentOpenMode.Import);
            reopened.PageCount.Should().Be(1);
        }
    }
}
