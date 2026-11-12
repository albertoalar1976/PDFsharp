// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


namespace PdfSharp.Drawing
{
    /// <summary>
    /// Provides functionality to load a bitmap image encoded in a specific format.
    /// </summary>
    public class XBitmapDecoder
    {
        internal XBitmapDecoder()
        { }

        /// <summary>
        /// Gets a new instance of the PNG image decoder.
        /// </summary>
        public static XBitmapDecoder GetPngDecoder()
        {
            return new XPngBitmapDecoder();
        }
    }

    sealed class XPngBitmapDecoder : XBitmapDecoder
    {
        internal XPngBitmapDecoder()
        { }
    }
}
