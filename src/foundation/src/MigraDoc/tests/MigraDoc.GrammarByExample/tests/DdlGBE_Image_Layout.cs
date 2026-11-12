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
    public class DdlGBE_Image_Layout : DdlGbeTestBase, IClassFixture<GbeFixture>
    {
        public DdlGBE_Image_Layout(GbeFixture fixture)
        {
            _fixture = fixture;
        }

        public override void TestInitialize()
        {
            InitializeTest(_fixture, "Image-Layout", 2, 0);
        }

        [SkippableFact]
        public void DDL_Grammar_By_Example_Image_Layout()
        {
            Skip.If(SkippableTests.SkipSlowTests());
            RunTest();
        }
        // ReSharper restore InconsistentNaming

        readonly GbeFixture _fixture;
    }
}
