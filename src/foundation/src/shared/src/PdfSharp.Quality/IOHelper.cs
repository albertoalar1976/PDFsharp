// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Reflection;
using PdfSharp.Pdf;

namespace PdfSharp.Quality
{
    /// <summary>
    /// Static helper functions for file IO.
    /// </summary>
    [Obsolete("Use IOUtility.")]
    public static class IOHelper  // #DELETE
    // ReSharper restore InconsistentNaming
    {


        //#if NET/FX_CORE || WUI || DNC10
        //        static async Task<string> SaveAndShowDocumentAsync(PdfDocument document, string filenameTag)
        //        {
        //            // Save the PDF document...
        //            string filename = await SaveDocumentAsync(document, filenameTag).ConfigureAwait(false);

        //            // ... and start a viewer.
        //            //Process.St7art(filename);
        //            return filename;
        //        }
        //#endif

        //#if true && (CORE || GDI || WPF || WUI)
        //#if true && (NET/FX_CORE || WUI)
        //        static async Task<string> SaveDocumentAsync(PdfDocument document, string filenameTag)
        //        {
        //            var filename = String.Format("{0:N}_{1}_tempfile.pdf", Guid.NewGuid(), filenameTag);
        //            document.Save(filename);
        //            await Task.Factory.StartNew(() => { }).ConfigureAwait(false);
        //            return filename;
        //        }
        //#endif


    }
}
