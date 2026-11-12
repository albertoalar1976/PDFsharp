// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using MigraDoc.GrammarByExample;
using Xunit;

namespace GdiGrammarByExample
{
    /// <summary>
    /// Grammar by example unit test class.
    /// </summary>
    // ReSharper disable InconsistentNaming
    [Collection("PDFsharp")]
    public class DdlGBE_Paragraph_SpecialCharacters : DdlGbeTestBase, IClassFixture<GbeFixture>
    {
        public DdlGBE_Paragraph_SpecialCharacters(GbeFixture fixture)
        {
            _fixture = fixture;
        }

        public override void TestInitialize()
        {
            InitializeTest(_fixture, "Paragraph-SpecialCharacters", 1, 0);
        }

        [SkippableFact]
        public void DDL_Grammar_By_Example_Paragraph_SpecialCharacters()
        {
            Skip.If(SkippableTests.SkipSlowTests());
            RunTest();
        }
        // ReSharper restore InconsistentNaming

        readonly GbeFixture _fixture;
    }
}
