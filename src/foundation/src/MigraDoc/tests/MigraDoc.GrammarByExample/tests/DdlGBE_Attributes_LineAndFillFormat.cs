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
    public class DdlGBE_Attributes_LineAndFillFormat : DdlGbeTestBase, IClassFixture<GbeFixture>
    {
        public DdlGBE_Attributes_LineAndFillFormat(GbeFixture fixture)
        {
            _fixture = fixture;
        }

        public override void TestInitialize()
        {
            InitializeTest(_fixture, "Attributes-LineAndFillFormat", 2, 0);
        }

        [SkippableFact]
        public void DDL_Grammar_By_Example_Attributes_LineAndFillFormat()
        {
            Skip.If(SkippableTests.SkipSlowTests());
            RunTest();
        }
        // ReSharper restore InconsistentNaming

        readonly GbeFixture _fixture;
    }
}
