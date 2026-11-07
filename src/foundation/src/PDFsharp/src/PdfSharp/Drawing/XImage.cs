// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using Microsoft.Extensions.Logging;
using PdfSharp.Internal;
using PdfSharp.Internal.Threading;
using PdfSharp.Internal.Imaging;
using PdfSharp.Logging;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Advanced;
// Nothing to import.

namespace PdfSharp.Drawing
{
    [Flags]
    enum XImageState
    {
        UsedInDrawingContext = 0x00000001,

        StateMask = 0x0000FFFF,
    }

    /// <summary>
    /// Defines an object used to draw image files (bmp, png, jpeg, gif) and PDF forms.
    /// An abstract base class that provides functionality for the Bitmap and Metafile descended classes.
    /// </summary>
    public class XImage : IDisposable
    {
        // The hierarchy is adapted to WPF
        //
        // XImage                       <-- ImageSource
        //   XForm
        //   PdfForm
        //   XBitmapSource              <-- BitmapSource
        //     XBitmapImage             <-- BitmapImage

        // ???
        //public bool Disposed
        //{
        //    get { return _disposed; }
        //    set { _disposed = value; }
        //}

        /// <summary>
        /// Initializes a new instance of the <see cref="XImage"/> class.
        /// </summary>
        protected XImage()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XImage"/> class from an image read by ImageImporter.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <exception cref="System.ArgumentNullException">image</exception>
        XImage(ImportedImage image)
        {
            _importedImage = image ?? throw new ArgumentNullException(nameof(image));
            Initialize();
        }





        // Useful stuff here: http://stackoverflow.com/questions/350027/setting-wpf-image-source-in-code
        XImage(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new FileNotFoundException(PsMsgs.FileNotFound(path));
            //throw new FileNotFoundException(PsMgs.FileNotFound(path), path);
            _path = path;

            // Use ImageImporter for supported formats.
            if (TryImportImage(path, out var image))
            {
                _importedImage = image;
                Initialize();
                return;
            }


            Initialize();
        }

        XImage(Stream stream)
        {
            // Use ImageImporter for supported formats.
            if (TryImportImage(stream, out var image))
            {
                _importedImage = image;
                // We do not store stream here because ImageImporter gets all information we need.
                Initialize();
                return;
            }

            //// Create a dummy unique path.
            //_path = "*" + Guid.NewGuid().ToString("B");

            // TODO_OLD: Create a fingerprint of the bytes in the stream to identify identical images.

            // Must assign _stream before Initialize().
            _stream = stream;
            Initialize();
        }






        /// <summary>
        /// Creates an image from the specified file.
        /// </summary>
        /// <param name="path">The path to a BMP, PNG, JPEG, or PDF file.</param>
        public static XImage FromFile(string path)
        {
            if (PdfReader.TestPdfFile(path) > 0)
                return new XPdfForm(path);

            PdfSharp.Internal.Imaging.ImportedImage ii;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // We can use FromStream, now that we have a stream.
                var success = ToBeNamed.TryImportImage(stream, out ii!);
                if (!success)
                    throw new InvalidOperationException("Unsupported image format.");
            }

            var image = new XImage(ii);
            image._path = path;
            return image;
        }

        /// <summary>
        /// Creates an image from the specified stream.<br/>
        /// </summary>
        /// <param name="stream">The stream containing a BMP, PNG, JPEG, or PDF file.</param>
        public static XImage FromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanSeek)
                throw new InvalidOperationException("The stream is not seekable. If your stream is not a PDF file use FromBitmapImageStreamThatCannotSeek instead.");

            // Test for PDF requires a seekable stream.
            if (PdfReader.TestPdfFile(stream) > 0)
                return new XPdfForm(stream);

            var success = ToBeNamed.TryImportImage(stream, out var ii);
            if (!success)
                throw new InvalidOperationException("Unsupported image format.");

            XImage image = new XImage(ii!);
            image._stream = stream;
            return image;
        }

        /// <summary>
        /// Creates an image from the specified stream.<br/>
        /// </summary>
        /// <param name="stream">The stream containing a BMP, PNG, JPEG, but not PDF file.</param>
        public static XImage FromBitmapImageStreamThatCannotSeek(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek)
                throw new InvalidOperationException("Use this function only for streams that do not support Seek.");

            var worker = new StreamReaderWorker(stream);
            var success = ToBeNamed.TryImportImage(worker.Data, out var i);
            if (!success)
                throw new InvalidOperationException("Unsupported image format.");

            XImage image = new XImage(i!);
            image._stream = stream;
            return image;
        }


#if DEBUG
        /// <summary>
        /// Creates an image.
        /// </summary>
        /// <param name="image">The imported image.</param>
        [Obsolete("For internal tests only. Not available in Release build.")]
        internal static XImage FromImportedImage(ImportedImage image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            return new XImage(image);
        }
#endif

        /// <summary>
        /// Tests if a file exist. Supports PDF files with page number suffix.
        /// </summary>
        /// <param name="path">The path to a BMP, PNG, GIF, JPEG, TIFF, or PDF file.</param>
        public static bool ExistsFile(string path)
        {
            // Support for "base64:" pseudo protocol is a MigraDoc feature, currently completely implemented in MigraDoc files.
            //if (path.StartsWith("base64:", StringComparison.Ordinal)) // The Image is stored in the string here, so the file exists.
            //    return true;

            if (PdfReader.TestPdfFile(path) > 0)
                return true;
            return File.Exists(path);
        }

        internal XImageState XImageState { get; set; }

        internal void Initialize()
        {
            if (_importedImage != null)
            {
                // In PDF there are two formats: JPEG and PDF bitmap.
                if (_importedImage is ImportedJpegImage)
                    _format = XImageFormat.Jpeg;
                else
                    _format = XImageFormat.Png;
                return;
            }

        }



        /// <summary>
        /// Under construction
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes underlying GDI+ object.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
                _disposed = true;

            {
                _importedImage = null;
            }
            // The stream belongs to the caller, so it is not closed here. But the image must not keep it,
            // e.g. a large MemoryStream, alive after it was disposed.
            _stream = null!;

        }

        bool _disposed;

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        [Obsolete("Use either PixelWidth or PointWidth. Temporarily obsolete because of rearrangements for WPF. Currently same as PixelWidth, but will become PointWidth in future releases of PDFsharp.")]
        public virtual double Width
        {
            get
            {
                if (_importedImage != null)
                    return _importedImage.PixelWidth;
                return 100;
                //#if GDI && !WPF
                //                return _gdiImage.Width;
                //#endif
            }
        }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        [Obsolete("Use either PixelHeight or PointHeight. Temporarily obsolete because of rearrangements for WPF. Currently same as PixelHeight, but will become PointHeight in future releases of PDFsharp.")]
        public virtual double Height
        {
            get
            {
                if (_importedImage != null)
                    return _importedImage.PixelHeight;
                return 100;
                //#if GDI && !WPF
                //                return _gdiImage.Height;
                //#endif
            }
        }

        /// <summary>
        /// The factor for conversion from DPM to PointWidth or PointHeight.
        /// 72 points per inch, 1000 mm per meter, 25.4 mm per inch => 72 * 1000 / 25.4.
        /// </summary>
        const double FactorDPM72 = 72000 / 25.4d;

        /// <summary>
        /// The factor for conversion from DPM to DPI.
        /// 1000 mm per meter, 25.4 mm per inch => 1000 / 25.4.
        /// </summary>
        const double FactorDPM = 1000 / 25.4d;

        /// <summary>
        /// Gets the width of the image in point.
        /// </summary>
        public virtual double PointWidth
        {
            get
            {
                if (_importedImage != null)
                {
                    return _importedImage.Width;
                }
                return 100;
            }
        }

        /// <summary>
        /// Gets the height of the image in point.
        /// </summary>
        public virtual double PointHeight
        {
            get
            {
                if (_importedImage != null)
                {
                    return _importedImage.Height;
                }
                return 100;
            }
        }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public virtual int PixelWidth
        {
            get
            {
                if (_importedImage != null)
                    return (int)_importedImage.PixelWidth;
                return 100;
            }
        }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public virtual int PixelHeight
        {
            get
            {
                if (_importedImage != null)
                    return (int)_importedImage.PixelHeight;
                return 100;
                //#if GDI && !WPF
                //                return _gdiImage.Height;
                //#endif
            }
        }

        /// <summary>
        /// Gets the size in point of the image.
        /// </summary>
        public virtual XSize Size => new XSize(PointWidth, PointHeight);

        /// <summary>
        /// Gets the horizontal resolution of the image.
        /// </summary>
        public virtual double HorizontalResolution
        {
            get
            {
                if (_importedImage != null)
                {
                    return _importedImage.DpiX;
                }
                return 96;
                //#if GDI && !WPF
                //                return _gdiImage.HorizontalResolution;
                //#endif
            }
        }

        /// <summary>
        /// Gets the vertical resolution of the image.
        /// </summary>
        public virtual double VerticalResolution
        {
            get
            {
                if (_importedImage != null)
                {
                    return _importedImage.DpiY;
                }
                return 96;
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating whether image interpolation is to be performed.
        /// </summary>
        public virtual bool Interpolate
        {
            get => _interpolate;
            set => _interpolate = value;
        }
        bool _interpolate = true;

        /// <summary>
        /// Gets the format of the image.
        /// </summary>
        public XImageFormat Format => _format ?? NRT.ThrowOnNull<XImageFormat>();

        XImageFormat? _format;

        static bool TryImportImage(string path,
            [MaybeNullWhen(false)] out ImportedImage image)
        {
            image = null!;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var success = ToBeNamed.TryImportImage(stream, out image!);
                    if (!success)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                PdfSharpLogHost.Logger.LogWarning(
                    $"Unhandled exception in TryImportImage, maybe caused by an unsupported image format. {ex.ToString()}");
            }
            return false;
        }

        static bool TryImportImage(Stream stream,
            [MaybeNullWhen(false)] out ImportedImage image)
        {
            image = null!;
            var success = ToBeNamed.TryImportImage(stream, out image!);
            if (!success)
                return false;

            return true;
        }



        internal void AssociateWithGraphics(XGraphics gfx)
        {
            if (_associatedGraphics is not null)
                throw new InvalidOperationException("XImage already associated with XGraphics.");
            _associatedGraphics = null;
        }

        internal void DisassociateWithGraphics()
        {
            if (_associatedGraphics is null)
                throw new InvalidOperationException("No XImage is associated with XGraphics.");
            _associatedGraphics.DisassociateImage();

            Debug.Assert(_associatedGraphics == null);
        }

        internal void DisassociateWithGraphics(XGraphics gfx)
        {
            if (_associatedGraphics != gfx)
                throw new InvalidOperationException("An XImage already associated with XGraphics.");
            _associatedGraphics = gfx;
        }

        internal XGraphics? AssociatedGraphics
        {
            get => _associatedGraphics;
            set => _associatedGraphics = value;
        }
        XGraphics? _associatedGraphics;

        internal ImportedImage? _importedImage;

        /// <summary>
        /// If path starts with '*' the image was created from a stream and the path is a GUID.
        /// </summary>
        internal string _path = null!;

        /// <summary>
        /// Contains a reference to the original stream if image was created from a stream.
        /// </summary>
        internal Stream _stream = null!;

        /// <summary>
        /// Cache PdfImageTable.ImageSelector to speed up finding the right PdfImage
        /// if this image is used more than once.
        /// </summary>
        internal PdfImageTable.ImageSelector _selector = null!;
    }
}
