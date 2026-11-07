// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality.Testing;
using PdfSharp.Quality.Testing.TestModel;
using Xunit;

namespace PdfSharp.Tests.Pdf.ObjectModel
{
    [Collection("PDFsharp")]
    public class LayoutTests : ObjectModelTestsBase
    {
        private readonly string _tempRoot = GetTempRoot(typeof(LayoutTests));

        [Fact]
        public void Test_Verbose_layout_arrays_in_arrays()
        {
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Compact_layout_bug));

            var catalog = rwh.Document.Catalog;
            var dict = new PdfDictionary(rwh.Document, true);
            catalog.Elements.Add("/TestStuff", dict);

            var array = new PdfArray();
            dict.Elements.Add("/DirectArray", array);

            array.Elements.Add(new PdfInteger(111));
            array.Elements.Add(new PdfArray(rwh.Document, new PdfInteger(7)));
            array.Elements.Add(new PdfArray());
            array.Elements.Add(PdfNull.Value);

            rwh.Save(nameof(Test_Verbose_layout_arrays_in_arrays), PdfWriterLayout.Verbose);
            rwh.Reload();
            rwh.Resave(PdfWriterLayout.Compact);
        }

        [Fact]
        public void Test_Compact_layout_bug()
        {
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Compact_layout_bug));

            var catalog = rwh.Document.Catalog;
            var dict = new PdfDictionary(rwh.Document, true);
            catalog.Elements.Add("/TestStuff", dict);

            var array = new PdfArray();
            dict.Elements.Add("/DirectArray", array);

            array.Elements.Add(new PdfInteger(33));
            array.Elements.Add(new PdfInteger(55));
            array.Elements.Add(new PdfInteger(-55));
            array.Elements.Add(new PdfInteger(-773));
            array.Elements.Add(new PdfReal(Math.PI));
            array.Elements.Add(new PdfReal(-Math.PI));

            rwh.Save(nameof(Test_Compact_layout_bug), PdfWriterLayout.Compact);
            rwh.Reload();
            rwh.Resave(PdfWriterLayout.Compact);
        }

        [Fact]
        public void Test_Compact_layout()
        {
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Compact_layout));

            CreateStuff(rwh.Document);

            rwh.Save(nameof(Test_Compact_layout), PdfWriterLayout.Compact);
            rwh.Reload();
            rwh.Resave(PdfWriterLayout.Compact);
        }

        [Fact]
        public void Test_Standard_layout()
        {
            // TODO: US265
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Standard_layout));

            CreateStuff(rwh.Document);

            rwh.Save(nameof(Test_Standard_layout), PdfWriterLayout.Standard);
            rwh.Reload();
            rwh.Resave(PdfWriterLayout.Standard);
        }

        public void Test_Indented_layout()
        {
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Indented_layout));
            // TODO: US265
        }

        [Fact]
        public void Test_Verbose_layout()
        {
            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Verbose_layout));

            CreateStuff(rwh.Document);

            rwh.Save(nameof(Test_Verbose_layout), PdfWriterLayout.Verbose);
            rwh.Reload();
            rwh.Resave(PdfWriterLayout.Verbose);
        }

        [Fact]
        public void Test_empty_name()
        {
            const PdfWriterLayout layout = PdfWriterLayout.Compact;

            var rwh = new ReadWriteHelper();
            rwh.CreateDocument(typeof(LayoutTests), nameof(Test_Compact_layout));

            var catalog = rwh.Document.Catalog;
            var dict = new PdfDictionary(rwh.Document, true);
            catalog.Elements.Add("/TestStuff", dict);

            var array1 = new PdfArray();
            array1.Elements.Add(new PdfName());
            array1.Elements.Add(new PdfInteger(123));
            array1.Elements.Add(new PdfName());
            array1.Elements.Add(new PdfInteger(123));
            array1.Elements.Add(new PdfName());
            array1.Elements.Add(new PdfName());
            array1.Elements.Add(new PdfName());

            dict.Elements.Add("/DirectArray", array1);

            var dict1 = new PdfDictionary();
            dict1.Elements.Add("/1", new PdfName());
            dict1.Elements.Add("/2", new PdfInteger(123));
            dict1.Elements.Add("/3", new PdfName());
            dict1.Elements.Add("/4", new PdfInteger(123));
            dict1.Elements.Add("/5", new PdfName());
            dict1.Elements.Add("/6", new PdfName());

            dict.Elements.Add("/DirectDictionary", dict1);

            var array2 = new PdfArray();
            array2.Elements.Add(new PdfNameObject(rwh.Document, "/"));
            array2.Elements.Add(new PdfIntegerObject(rwh.Document, 123));
            array2.Elements.Add(new PdfNameObject(rwh.Document, "/"));
            array2.Elements.Add(new PdfIntegerObject(rwh.Document, 123));
            array2.Elements.Add(new PdfNameObject(rwh.Document, "/"));
            array2.Elements.Add(new PdfNameObject(rwh.Document, "/"));
            array2.Elements.Add(new PdfNameObject(rwh.Document, "/"));

            dict.Elements.Add("/IndirectArray", array2);

            var dict2 = new PdfDictionary();
            dict2.Elements.Add("/1", new PdfNameObject(rwh.Document, "/"));
            dict2.Elements.Add("/2", new PdfInteger(123));
            dict2.Elements.Add("/3", new PdfNameObject(rwh.Document, "/"));
            dict2.Elements.Add("/4", new PdfInteger(123));
            dict2.Elements.Add("/5", new PdfNameObject(rwh.Document, "/"));
            dict2.Elements.Add("/6", new PdfNameObject(rwh.Document, "/"));

            dict.Elements.Add("/DirectDictionary", dict2);
            //var array2 = new PdfArray(Document, true);
            //PopulateArray(array2);
            //dict.Elements.Add("/IndirectArray", array2);

            //var dict1 = new PdfDictionary();
            //PopulateDictionary(dict1);
            //dict.Elements.Add("/DirectDictionary", dict1);

            //var dict2 = new PdfDictionary();
            //PopulateDictionary(dict2);

            rwh.Save(nameof(Test_empty_name), layout);
            rwh.Reload();
            rwh.Resave(layout);
        }


        void CreateStuff(PdfDocument document)
        {
            var catalog = document.Catalog;
            var dict = new PdfDictionary(document, true);
            catalog.Elements.Add("/TestStuff", dict);

            var array1 = new PdfArray();
            new ObjectModelTestHelper(document).PopulateArray(array1);
            dict.Elements.Add("/DirectArray", array1);

            var array2 = new PdfArray(document, true);
            new ObjectModelTestHelper(document).PopulateArray(array2);
            dict.Elements.Add("/IndirectArray", array2);

            var dict1 = new PdfDictionary();
            new ObjectModelTestHelper(document).PopulateDictionary(dict1);
            dict.Elements.Add("/DirectDictionary", dict1);

            var dict2 = new PdfDictionary();
            new ObjectModelTestHelper(document).PopulateDictionary(dict2);
            dict.Elements.Add("/IndirectDictionary", dict2);
        }

    }
}
