// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;
//#if NET/FX_CORE || WUI || DNC10
//using System.Threading.Tasks;
//using Windows.Foundation;
//using Windows.Storage;
//#endif
using System.Drawing.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Events;
using PdfSharp.Pdf;

// ReSharper disable ConvertToAutoProperty

namespace PdfSharp.Quality
{
    /// <summary>
    /// Base class with common code for both features and snippets.
    /// </summary>
    public abstract class FeatureAndSnippetBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureAndSnippetBase"/> class.
        /// </summary>
        protected FeatureAndSnippetBase()
        { }

        /// <summary>
        /// Specifies how to draw a box on the PDF page.
        /// </summary>
        protected enum BoxOptions
        {
            /// <summary>
            /// Draw no box.
            /// </summary>
            None,

            /// <summary>
            /// Draw a box.
            /// </summary>
            Box,

            /// <summary>
            /// Draw 
            /// </summary>
            DrawX,

            /// <summary>
            /// Draw 
            /// </summary>
            Fill,

            /// <summary>
            /// Draw 
            /// </summary>
            Tile,
        }

        // The size of the test page was designed as 135mm times 180mm.
        // This is in point 382.677 times 510.236. To make it easier to check
        // PDF content the size is rounded to 380 times 510 point, that is
        // 134.055mm times 179.971mm
        //
        //public static readonly double WidthInMillimeter = 135;
        //public static readonly double HeightInMillimeter = 180;
        //public static readonly double WidthInPoint = 380; // 360
        //public static readonly double HeightInPoint = 510;  // 480

        /// <summary>
        /// The width of the PDF page in point.
        /// </summary>
        public const double WidthInPoint = 360;

        /// <summary>
        /// The height of the PDF page in point.
        /// </summary>
        public const double HeightInPoint = 480;

        /// <summary>
        /// The width of the PDF page in millimeter.
        /// </summary>
        public const double WidthInMillimeter = WidthInPoint * 25.4 / 72;  // 127cm

        /// <summary>
        /// The height of the PDF page in millimeter.
        /// </summary>
        public const double HeightInMillimeter = HeightInPoint * 25.4 / 72;  // 169.3‾cm

        // ReSharper disable InconsistentNaming
        /// <summary>
        /// The width of the PDF page in presentation units.
        /// </summary>
        public const double WidthInPU = 480;

        /// <summary>
        /// The width of the PDF page in presentation units.
        /// </summary>
        public const double HeightInPU = 640;
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// The width of the drawing box in presentation units.
        /// </summary>
        public const double BoxWidth = 224;  // = 168pt
        //public const double BoxWidth = 220;  // = 160pt

        /// <summary>
        /// The height of the drawing box in presentation units.
        /// </summary>
        public const double BoxHeight = 144;  // = 108pt
        //public const double BoxHeight = 150;  // = 200pt

        /// <summary>
        /// The center of the box.
        /// </summary>
        public XPoint BoxCenter { get; } = new(BoxWidth / 2, BoxHeight / 2);

        /// <summary>
        /// Gets the gray background brush for boxes.
        /// </summary>
        protected XSolidBrush Gray { get; } = new(XColor.FromArgb(204, 204, 204));

        /// <summary>
        /// Gets a tag that specifies the PDFsharp build technology.
        /// It is either 'core', 'gdi', or 'wpf'.
        /// </summary>
        public static string PdfSharpTechnology
        {
            get
            {
                if (Capabilities.Build.IsCoreBuild)
                    return "core";
                if (Capabilities.Build.IsGdiBuild)
                    return "gdi";
                if (Capabilities.Build.IsWpfBuild)
                    return "wpf";
                throw new NotImplementedException("Unknown build.");
            }
        }

        /// <summary>
        /// Creates a new PDF document.
        /// </summary>
        public void BeginPdfDocument()
        {
            var document = new PdfDocument();
            document.Info.Author = "PDFsharp team";
            document.PageLayout = PdfPageLayout.SinglePage;
            //document.PageMode = PdfPageMode.FullScreen;

            Document = document;
        }

        /// <summary>
        /// Ends access to the current PDF document and renders it to a memory stream.
        /// </summary>
        public void EndPdfDocument()
        {
            var stream = new MemoryStream();
            Document.Save(stream);

            Page = null!;
            XGraphics = null!;

            var bytes = stream.ToArray();
            PdfBytes = bytes;
        }

        /// <summary>
        /// Creates a new page in the current PDF document.
        /// </summary>
        public XGraphics BeginPdfPage()
        {
            return BeginPdfPage(XUnit.FromPoint(WidthInPoint), XUnit.FromPoint(HeightInPoint), XGraphicsUnit.Presentation, XPageDirection.Downwards);
        }

        /// <summary>
        /// Creates a new page in the current PDF document.
        /// </summary>
        public XGraphics BeginPdfPage(XUnit width, XUnit height, XGraphicsUnit graphicsUnit, XPageDirection pageDirection = XPageDirection.Downwards)
        {
            var page = Document.AddPage();

            page.Width = width;  // = XUnit.FromPresentation(WidthInPU);
            page.Height = height;  // = XUnit.FromPresentation(HeightInPU);

            // All drawing is done in presentation units (1/96 inch).
            var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append, graphicsUnit, pageDirection);
            // Draw a bounding box. BUG_OLD if graphics unit is not Presentation.

            if (graphicsUnit != XGraphicsUnit.Point)
            {
                width.ConvertType(graphicsUnit);
                height.ConvertType(graphicsUnit);
            }

            var rect = new XRect(0.5, 0.5, width.Presentation - 1, height.Presentation - 1);
            gfx.DrawRectangle(XBrushes.WhiteSmoke, rect);

            Page = page;
            XGraphics = gfx;

            return gfx;
        }

        /// <summary>
        /// Ends the current PDF page.
        /// </summary>
        public void EndPdfPage()
        {
            if (_page is not null)
            {
                _page.Close();
                _page = null;
            }
        }

        /// <summary>
        /// Generates a PDF document for parallel comparison of two render results.
        /// </summary>
        public void GenerateParallelComparisonDocument()
        {
            var document = new PdfDocument();

            var page = document.AddPage();
            page.Width = XUnit.FromPoint(WidthInPoint * 2);
            page.Height = XUnit.FromPoint(HeightInPoint);

            var gfx = XGraphics.FromPdfPage(page);

            var width = page.Width.Point;
            var height = page.Height.Point;

            var leftBox = new XRect(0, 0, width / 2, height);
            DrawPdfToBox(gfx, leftBox);

            var rightBox = new XRect(width / 2, 0, width / 2, height);
            DrawImageToBox(gfx, rightBox);

            var stream = new MemoryStream();
            document.Save(stream);

            var bytes = stream.ToArray();
            ComparisonBytes = bytes;
        }

        /// <summary>
        /// Generates the serial comparison document. DOCTODO
        /// </summary>
        public void GenerateSerialComparisonDocument()
        {
            var document = new PdfDocument();

            var page = document.AddPage();
            page.Width = XUnit.FromPoint(WidthInPoint);
            page.Height = XUnit.FromPoint(HeightInPoint);

            var gfx = XGraphics.FromPdfPage(page);

            var pageBox = new XRect(0, 0, page.Width.Point, page.Height.Point);
            DrawPdfToBox(gfx, pageBox);

            page = document.AddPage();
            page.Width = XUnit.FromPoint(WidthInPoint);
            page.Height = XUnit.FromPoint(HeightInPoint);

            gfx = XGraphics.FromPdfPage(page);

            DrawImageToBox(gfx, pageBox);

            var stream = new MemoryStream();
            document.Save(stream);

            var bytes = stream.ToArray();
            ComparisonBytes = bytes;
        }

        void DrawPdfToBox(XGraphics gfx, XRect box)
        {
            if (PdfBytes == null)
            {
                DrawEmptyBox(gfx, box);
                return;
            }

            var stream = new MemoryStream(PdfBytes);
            var form = XPdfForm.FromStream(stream);
            gfx.DrawImage(form, box);
        }

        void DrawImageToBox(XGraphics gfx, XRect box)
        {
            if (PngBytes == null!)
            {
                DrawEmptyBox(gfx, box);
                return;
            }

            var stream = new MemoryStream(PngBytes, 0, PngBytes.Length, false, true);
            var image = XImage.FromStream(stream);
            image.Interpolate = false;
            gfx.DrawImage(image, box);
        }

        void DrawEmptyBox(XGraphics gfx, XRect box)
        {
            var pen = XPens.Red;
            gfx.DrawRectangle(pen, box);
            gfx.DrawLine(pen, box.TopLeft, box.BottomRight);
        }

        /// <summary>
        /// Saves the bytes to PDF file and show file.
        /// </summary>
        /// <param name="sourceBytes">The source bytes.</param>
        /// <param name="filepath">The filepath.</param>
        /// <param name="startViewer">if set to <c>true</c> [start viewer].</param>
        /// <exception cref="System.ArgumentNullException">sourceBytes</exception>
        public string SaveAndShowFile(byte[]? sourceBytes, string filepath = "", bool startViewer = false)
        {
            if (sourceBytes is null)
                throw new ArgumentNullException(nameof(sourceBytes));

            //filepath = CompleteFilePath(sourceBytes, filepath);
            filepath = IOUtility.GetTempFullFileName(filepath, GetFileExtension(sourceBytes), true);

            // Save the PDF document...
            using var stream = File.Create(filepath);
            {
                stream.Write(sourceBytes, 0, sourceBytes.Length);
                // Stream must be closed here, otherwise it cannot be copied under Linux.
                // To me, it is not clear what’s the difference with Windows.
                stream.Close();
            }

            if (startViewer)
                PdfFileUtility.ShowDocument(filepath);

            return filepath;
        }

        //string CompleteFilePath(byte[]? sourceBytes, string filepath)
        //{
        //    if (sourceBytes is null)
        //        throw new ArgumentNullException(nameof(sourceBytes));

        //    // Add generated filename, if filepath is only a directory (doesn’t contain a file extension).
        //    return String.IsNullOrEmpty(Path.GetExtension(filepath))
        //        ? Path.Combine(filepath, GenerateFilename(sourceBytes))
        //        : filepath;
        //}

        //string GenerateFilename(byte[] sourceBytes)
        //{
        //    return $"{GetType().Name}-{PdfSharpTechnology}-tempfile.{GetFileExtension(sourceBytes)}";
        //}

        string GetFileExtension(byte[] sourceBytes)
        {
            if (sourceBytes == PdfBytes)
                return "pdf";
            if (sourceBytes == PngBytes)
                return "png";
            if (sourceBytes == ComparisonBytes)
                return "pdf";
            Debug.Assert(false);
            return "";
        }

        /// <summary>
        /// Prepares new bitmap image for drawing.
        /// </summary>
        public void BeginImage()
        {
            //int factor = 4;
            //int width = (int)(WidthInPoint * factor);
            //int height = (int)(HeightInPoint * factor);
            //this.image = new RenderTargetBitmap(width, height, 72 * factor, 72 * factor, PixelFormats.Default);
            //this.visual = new DrawingVisual();
            //this.dc = this.visual.RenderOpen();

            //// Draw a bounding box
            ////Rect rect = new Rect(0, 0, width - 1, height - 1);
            //Rect rect = new Rect(0, 0, WidthInPoint * 4 / 3 - 1, HeightInPoint * 4 / 3 - 1);
            ////this.dc.DrawRectangle(Brushes.WhiteSmoke, new Pen(Brushes.LightGray, 1), rect);
            //this.dc.DrawRectangle(Brushes.WhiteSmoke, null, rect);

            ////double d = 10;
            ////Pen pen = new Pen(Brushes.Red, 5);
            ////this.dc.DrawLine(pen, rect.TopLeft, rect.TopLeft + new Vector(d, d));
            ////this.dc.DrawLine(pen, rect.TopRight, rect.TopRight + new Vector(-d, d));
            ////this.dc.DrawLine(pen, rect.BottomLeft, rect.BottomLeft + new Vector(d, -d));
            ////this.dc.DrawLine(pen, rect.BottomRight, rect.BottomRight + new Vector(-d, -d));

            ////this.dc.PushTransform(new ScaleTransform(factor, factor));
            //this.imgGfx = XGraphics.FromDrawingContext(this.dc, new XSize(WidthInPoint, HeightInPoint), XGraphicsUnit.Point);
        }

        /// <summary>
        /// Ends the current GDI+ image.
        /// </summary>
        public void EndPngImage()
        {
        }

        /// <summary>
        /// Gets the current PDF document.
        /// </summary>
        public PdfDocument Document
        {
            get => _document ?? throw new InvalidOperationException("Document was not yet set.");
            private set
            {
                _document = value;
                //_page = null;
                _pdfBytes = [];
            }
        }
        PdfDocument? _document;

        /// <summary>
        /// Gets the current PDF page.
        /// </summary>
        public PdfPage Page
        {
            get => _page ?? throw new InvalidOperationException("Page must not be null.");
            private set
            {
                _page = value;
            }
        }
        PdfPage? _page;

        /// <summary>
        /// Gets the current PDFsharp graphics object.
        /// </summary>
        public XGraphics XGraphics
        {
            get => _xgfx ?? throw new InvalidOperationException("XGraphics must not be null.");
            private set => _xgfx = value;
        }
        XGraphics? _xgfx;


        /// <summary>
        /// Gets the bytes of a PDF document.
        /// </summary>
        public byte[]? PdfBytes
        {
            get => _pdfBytes;
            private set
            {
                _pdfBytes = value ?? [];
                _document = null;
                _page = null;
            }
        }
        byte[] _pdfBytes = [];

        /// <summary>
        /// Gets the bytes of a PNG image.
        /// </summary>
        public byte[] PngBytes
        {
            get => _pngBytes;
            private set
            {
                _pngBytes = [];
            }
        }
        byte[] _pngBytes = [];

        /// <summary>
        /// Gets the bytes of a comparision document.
        /// </summary>
        public byte[] ComparisonBytes
        {
            get => _comparisonBytes;
            private set => _comparisonBytes = value ?? [];
        }
        byte[] _comparisonBytes = [];


    }
}
