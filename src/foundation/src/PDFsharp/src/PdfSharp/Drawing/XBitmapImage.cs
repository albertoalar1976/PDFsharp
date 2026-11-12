// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


using PdfSharp.Internal;
using PdfSharp.Internal.Threading;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a pixel-based bitmap image.
    /// </summary>
    public sealed class XBitmapImage : XBitmapSource
    {
        // TODO_OLD: Move code from XImage to this class.

        /// <summary>
        /// Initializes a new instance of the <see cref="XBitmapImage"/> class.
        /// </summary>
        internal XBitmapImage(int width, int height)
        {
            Initialize();
        }

        /// <summary>
        /// Creates a default 24-bit ARGB bitmap with the specified pixel size.
        /// </summary>
        public static XBitmapSource CreateBitmap(int width, int height)
        {
            // Create a default 24-bit ARGB bitmap.
            return new XBitmapImage(width, height);
        }
    }
}
