// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using FluentAssertions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using Xunit;

namespace MigraDoc.DocumentObjectModel.Tests
{
    /// <summary>
    /// MigraDoc documents and their MDDDL representation must not depend on the culture of the current thread.
    /// </summary>
    [Collection("PDFsharp")]
    public class CultureIndependenceTests
    {
        [Fact]
        public void Unit_parses_upper_case_units_under_Turkish_culture()
        {
            // Under tr-TR, "IN".ToLower() is "ın" (dotless i), so the unit was not recognized.
            using var _ = new CultureScope("tr-TR");

            Unit.Parse("2IN").Inch.Should().Be(2);
            Unit unit = "1.5in";
            unit.Inch.Should().Be(1.5);
        }

        [Theory]
        [InlineData("es-ES")]
        [InlineData("de-DE")]
        [InlineData("ar-SA")]
        [InlineData("tr-TR")]
        public void Ddl_does_not_depend_on_culture(string culture)
        {
            var expected = DdlWriter.WriteToString(CreateDocument());

            string actual;
            using (new CultureScope(culture))
            {
                actual = DdlWriter.WriteToString(CreateDocument());

                // The DDL can be read back under the same culture.
                var document = DdlReader.DocumentFromString(actual);
                var paragraph = (Paragraph)document.Sections[0].Elements[0]!;
                paragraph.Format.Font.Size.Point.Should().Be(10.5);
                paragraph.Format.LeftIndent.Centimeter.Should().BeApproximately(1.5, 1e-6);
            }

            actual.Should().Be(expected);
            actual.Should().Contain("10.5").And.Contain("1.5cm").And.NotContain("10,5").And.NotContain("1,5cm");
        }

        static Document CreateDocument()
        {
            var document = new Document();
            var section = document.AddSection();
            var paragraph = section.AddParagraph("Culture test");
            paragraph.Format.Font.Size = 10.5;
            paragraph.Format.LeftIndent = Unit.FromCentimeter(1.5);
            paragraph.Format.SpaceBefore = Unit.FromPoint(2.25);
            return document;
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
