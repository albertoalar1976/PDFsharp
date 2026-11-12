// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

namespace PdfSharp.Pdf.IO
{
    /// <summary>
    /// Determines how a PDF document is opened. 
    /// </summary>
    public enum PdfDocumentOpenMode
    {
        /// <summary>
        /// The PDF stream is completely read into memory and can be modified. Pages can be deleted or
        /// inserted, but it is not possible to extract pages. This mode is useful for modifying an
        /// existing PDF document.
        /// </summary>
        Modify,

        /// <summary>
        /// The PDF stream is opened for importing pages from it. A document opened in this mode cannot
        /// be modified, but you can extract pages from it.
        /// </summary>
        Import,

    }
}
