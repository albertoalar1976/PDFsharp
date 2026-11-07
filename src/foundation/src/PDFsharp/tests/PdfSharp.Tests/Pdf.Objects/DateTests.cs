// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf.Metadata;
using Xunit;
using FluentAssertions;
using PdfSharp.Quality.Testing;

namespace PdfSharp.Tests.Pdf.Objects
{
    [Collection("PDFsharp")]
    public class DateTests : PdfSharpTestBase
    {
        readonly string _tempRoot = GetTempRoot(typeof(DateTests));

        [Fact]
        public void Test_MetadataManager_ToXmpDateString()
        {
            DateTimeOffset? date1 = null;
            var str1 = MetadataManager.ToXmpDateString(date1);
            str1.Should().BeEmpty();

            var date2 = new DateTimeOffset(2025, 9, 22, 15, 16, 17, TimeSpan.Zero);
            var str2 = MetadataManager.ToXmpDateString(date2);
            str2.Should().Be("2025-09-22T15:16:17+00:00");

            var date3 = new DateTimeOffset(2025, 9, 22, 15, 16, 17, new TimeSpan(2, 30, 0));
            var str3 = MetadataManager.ToXmpDateString(date3);
            str3.Should().Be("2025-09-22T15:16:17+02:30");

            var date4 = new DateTimeOffset(2025, 9, 22, 15, 16, 17, -new TimeSpan(2, 45, 0));
            var str4 = MetadataManager.ToXmpDateString(date4);
            str4.Should().Be("2025-09-22T15:16:17-02:45");
        }
    }
}
