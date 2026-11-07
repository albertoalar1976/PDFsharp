// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Internal.Threading;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines an abstract base class for pixel-based images.
    /// </summary>
    public abstract class XBitmapSource : XImage
    {
        // TODO_OLD: Move code from XImage to this class.

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public override int PixelWidth
        {
            get
            {
                if (_importedImage!= null)
                    return _importedImage.PixelWidth;

                return 100;
            }
        }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public override int PixelHeight
        {
            get
            {
                if (_importedImage != null)
                    return _importedImage.PixelHeight;

                return 100;
            }
        }
    }
}
