// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Text;
using Microsoft.Extensions.Logging;
using PdfSharp.Internal.OpenType;
using PdfSharp.Internal.Threading;
using PdfSharp.Pdf.Internal;
using PdfSharp.Pdf;
using PdfSharp.Drawing.Pdf;
using PdfSharp.Events;
using PdfSharp.Fonts.Internal;
using PdfSharp.Internal;
using PdfSharp.Logging;
using PdfSharp.Pdf.Advanced;


#pragma warning disable CS1591 // TODO_DOC: Missing XML comment for publicly visible type or member
#pragma warning disable 1587 // TODO_DOC: XML comment is not placed on a valid language element
// ReSharper disable UseNullPropagation
// ReSharper disable RedundantNameQualifier
// ReSharper disable UseNameofExpression

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Holds information about the current state of the XGraphics object.
    /// </summary>
    [Flags]
    enum InternalGraphicsMode
    {
        DrawingGdiGraphics,
        DrawingPdfContent,
        DrawingBitmap,
    }

    /// <summary>
    /// Represents a drawing surface for a fixed size page.
    /// </summary>
    public sealed class XGraphics : IDisposable
    {
        // TODO_OLD: Implement better concept of a measure context.




        /// <summary>
        /// Initializes a new instance of the XGraphics class for drawing on a PDF page.
        /// </summary>
        XGraphics(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit pageUnit, XPageDirection pageDirection)
        {
#if DEBUG
            if (page.IsDead)
                _ = typeof(int);
#endif
            page.Owner.EnsureNotYetSaved();

            if (page == null)
                throw new ArgumentNullException(nameof(page));

            if (page.Owner == null)
                throw new ArgumentException("You cannot draw on a page that is not owned by a PdfDocument object.", nameof(page));

            if (page.RenderContent != null)
                throw new InvalidOperationException("Another page content renderer object already exists for this page and must be disposed before a new one can be created.");

            if (page.Owner.IsReadOnly)
                throw new InvalidOperationException("Cannot create XGraphics for a page of a document that cannot be modified. Use PdfDocumentOpenMode.Modify.");

            _gsStack = new(this);

            PdfContent? content;
            switch (options)
            {
                case XGraphicsPdfPageOptions.Append:
                    content = page.Contents.AppendContent();
                    break;

                case XGraphicsPdfPageOptions.Prepend:
                    content = page.Contents.PrependContent();
                    break;

                case XGraphicsPdfPageOptions.Replace:
                    page.Contents.Elements.Clear();
                    goto case XGraphicsPdfPageOptions.Append;

                default:
                    throw new InvalidOperationException();
            }
            page.RenderContent = content;
            TargetContext = XGraphicTargetContext.CORE;
            _owner = page.Owner;
            _renderer = new XGraphicsPdfRenderer(page, this, options);
            _pageSizePoints = new(page.Width.Point, page.Height.Point);
            switch (pageUnit)
            {
                case XGraphicsUnit.Point:
                    _pageSize = new(page.Width.Point, page.Height.Point);
                    break;

                case XGraphicsUnit.Inch:
                    _pageSize = new(page.Width.Inch, page.Height.Inch);
                    break;

                case XGraphicsUnit.Millimeter:
                    _pageSize = new(page.Width.Millimeter, page.Height.Millimeter);
                    break;

                case XGraphicsUnit.Centimeter:
                    _pageSize = new(page.Width.Centimeter, page.Height.Centimeter);
                    break;

                case XGraphicsUnit.Presentation:
                    _pageSize = new(page.Width.Presentation, page.Height.Presentation);
                    break;

                default:
                    throw new NotSupportedException($"Unit {pageUnit}");
            }
            _pageUnit = pageUnit;
            _pageDirection = pageDirection;

            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the XGraphics class for a measure context.
        /// </summary>
        XGraphics(XGraphicsUnit pageUnit, XPageDirection pageDirection, RenderEvents? renderEvents = null)
        {
            RenderEvents = renderEvents;
            _gsStack = default!;
            TargetContext = XGraphicTargetContext.CORE;
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the XGraphics class used for drawing on a form.
        /// </summary>
        XGraphics(XForm form, RenderEvents? renderEvents = null)
        {
            if (form == null!)
                throw new ArgumentNullException(nameof(form));

            _form = form;
            form.AssociateGraphics(this);

            RenderEvents = renderEvents;

            _gsStack = new GraphicsStateStack(this);
            TargetContext = XGraphicTargetContext.CORE;
            _drawGraphics = false;
            if (form.Owner != null!)
                _renderer = new XGraphicsPdfRenderer(form, this);
            _pageSize = form.Size;
            Initialize();
        }

        /// <summary>
        /// Creates the measure context. This is a graphics context created only for querying measures of text.
        /// Drawing on a measure context has no effect.
        /// Commit renderEvents to allow RenderTextEvent calls.
        /// </summary>
        public static XGraphics CreateMeasureContext(XSize size, XGraphicsUnit pageUnit, XPageDirection pageDirection, RenderEvents? renderEvents = null)
        {
            //var dummy = new PdfDocument();
            //var page = dummy.AddPage();
            //XGraphics gfx = new XGraphics(((System.Drawing.Graphics)null, size, pageUnit, pageDirection);
            //var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append, pageUnit, pageDirection, renderEvents);
            var gfx = new XGraphics(pageUnit, pageDirection, renderEvents);

            //PdfSharpLogHost.Logger.XGraphicsCreated("dummy page in dummy document used as measure context");

            return gfx;
        }





        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page)
        {
            var gfx = new XGraphics(page, XGraphicsPdfPageOptions.Append, XGraphicsUnit.Point, XPageDirection.Downwards);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XGraphicsUnit unit)
        {
            var gfx = new XGraphics(page, XGraphicsPdfPageOptions.Append, unit, XPageDirection.Downwards);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XPageDirection pageDirection)
        {
            var gfx = new XGraphics(page, XGraphicsPdfPageOptions.Append, XGraphicsUnit.Point, pageDirection);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options)
        {
            var gfx = new XGraphics(page, options, XGraphicsUnit.Point, XPageDirection.Downwards);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XPageDirection pageDirection)
        {
            var gfx = new XGraphics(page, options, XGraphicsUnit.Point, pageDirection);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit unit)
        {
            XGraphics gfx = new XGraphics(page, options, unit, XPageDirection.Downwards);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Pdf.PdfPage object.
        /// </summary>
        public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit unit, XPageDirection pageDirection)
        {
            var gfx = new XGraphics(page, options, unit, pageDirection);
            // #PDF-UA
            if (page.Owner.UAManager != null)
                page.Owner.Events.OnPageGraphicsCreated(page.Owner, new PageGraphicsEventArgs(page.Owner) { Page = page, Graphics = gfx, ActionType = PageGraphicsActionType.GraphicsCreated });

            PdfSharpLogHost.Logger.XGraphicsCreated("PDF page");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Drawing.XPdfForm object.
        /// </summary>
        public static XGraphics FromPdfForm(XPdfForm form)
        {
            if (form.Gfx != null)
                return form.Gfx;

            var gfx = new XGraphics(form, form.Owner.RenderEvents);

            PdfSharpLogHost.Logger.XGraphicsCreated("XPdfForm");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Drawing.XForm object.
        /// </summary>
        public static XGraphics FromForm(XForm form)
        {
            if (form.Gfx != null)
                return form.Gfx;

            var gfx = new XGraphics(form, form.Owner.RenderEvents);

            PdfSharpLogHost.Logger.XGraphicsCreated("XForm");

            return gfx;
        }

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Drawing.XForm object.
        /// </summary>
        public static XGraphics? FromImage(XImage image, RenderEvents? renderEvents = null)
            => FromImage(image, XGraphicsUnit.Point, renderEvents);

        /// <summary>
        /// Creates a new instance of the XGraphics class from a PdfSharp.Drawing.XImage object.
        /// </summary>
        public static XGraphics? FromImage(XImage image, XGraphicsUnit unit, RenderEvents? renderEvents = null)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (image is XBitmapImage bmImage)
            {
                DiagnosticsHelper.ThrowNotImplementedException("XGraphics from XImage");
            }
            return null;
        }

        /// <summary>
        /// Internal setup.
        /// </summary>
        void Initialize()
        {
            _pageOrigin = new();

            double pageHeight = _pageSize.Height;
            var targetPage = PdfPage;
            var trimOffset = new XPoint();
            if (targetPage is { TrimMargins.AreSet: true })
            {
                pageHeight += targetPage.TrimMargins.Top.Point + targetPage.TrimMargins.Bottom.Point;
                trimOffset = new XPoint(targetPage.TrimMargins.Left.Point, targetPage.TrimMargins.Top.Point);
            }

            var matrix = new XMatrix();
            // Nothing to do here.
            Debug.Assert(TargetContext == XGraphicTargetContext.CORE);
            if (_pageDirection != XPageDirection.Downwards)
                matrix.Prepend(new XMatrix(1, 0, 0, -1, 0, pageHeight));

            if (trimOffset != new XPoint())
                matrix.TranslatePrepend(trimOffset.X, -trimOffset.Y);

            DefaultViewMatrix = matrix;
            _transformationMatrix = new XMatrix();
        }

        /// <summary>
        /// Releases all resources used by this object.
        /// PDFsharp disposes XGraphics objects automatically.
        /// You must call Dispose if you want to create another XGraphics  object or a
        /// PDFsharp Graphics DrawingContext for this page.
        /// </summary>
        public void Dispose() => Dispose(true);

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    // Dispose managed resources.
                    if (_associatedImage != null)
                    {
                        _associatedImage.DisassociateWithGraphics(this);
                        _associatedImage = null;
                    }
                }

                if (_form != null!)
                    _form.Finish();
                _drawGraphics = false;

                _renderer?.Close();
                _renderer = null;
            }
        }
        bool _disposed;

        /// <summary>
        /// A value indicating whether GDI+ or WPF is used as context.
        /// </summary>
        internal XGraphicTargetContext TargetContext;

        /// <summary>
        /// Gets or sets the unit of measure used for page coordinates.
        /// CURRENTLY ONLY POINT IS IMPLEMENTED.
        /// </summary>
        public XGraphicsUnit PageUnit => _pageUnit;

        readonly XGraphicsUnit _pageUnit;

        /// <summary>
        /// Gets or sets the value indicating in which direction y-value grow.
        /// </summary>
        public XPageDirection PageDirection
        {
            get => _pageDirection;
            set
            {
                // Is there really anybody who needs the concept of XPageDirection.Upwards?
                if (value != XPageDirection.Downwards)
                    throw new NotSupportedException("PageDirection must be XPageDirection.Downwards in current implementation.");
            }
        }
        readonly XPageDirection _pageDirection;

        /// <summary>
        /// Gets the current page origin. Setting the origin is not yet implemented.
        /// </summary>
        public XPoint PageOrigin
        {
            get => _pageOrigin;
            set
            {
                // Is there really anybody who needs to set the page origin?
                if (value != new XPoint())
                    throw new NotSupportedException("PageOrigin cannot be modified in current implementation.");
            }
        }
        XPoint _pageOrigin;

        /// <summary>
        /// Gets the current size of the page in the current page units.
        /// </summary>
        public XSize PageSize
        {
            get => _pageSize;
        }
        XSize _pageSize;
        XSize _pageSizePoints;

        internal PdfDocument? Owner
        {
            get
            {
                if (_owner == null && _tryGetOwner is false)
                {
                    if (_renderer is XGraphicsPdfRenderer renderer)
                        _owner = renderer.Owner;
                    _tryGetOwner = true;
                }
                return _owner;
            }
        }
        PdfDocument? _owner;
        bool _tryGetOwner;

        RenderEvents? RenderEvents { get; }

        #region Drawing

        // ----- DrawLine -----------------------------------------------------------------------------




        /// <summary>
        /// Draws a line connecting two XPoint structures.
        /// </summary>
        public void DrawLine(XPen pen, XPoint pt1, XPoint pt2)
        {
            DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);
        }

        /// <summary>
        /// Draws a line connecting the two points specified by coordinate pairs.
        /// </summary>
        public void DrawLine(XPen pen, double x1, double y1, double x2, double y2)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawLines(pen, [new(x1, y1), new(x2, y2)]);
        }

        // ----- DrawLines ----------------------------------------------------------------------------




        /// <summary>
        /// Draws a series of line segments that connect an array of points.
        /// </summary>
        public void DrawLines(XPen pen, XPoint[] points)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Length < 2)
                throw new ArgumentException(PsMsgs.PointArrayAtLeast(2), nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawLines(pen, points);
        }

        /// <summary>
        /// Draws a series of line segments that connect an array of x and y pairs.
        /// </summary>
        public void DrawLines(XPen pen, double x, double y, params double[] value)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            int length = value.Length;
            XPoint[] points = new XPoint[length / 2 + 1];
            points[0].X = x;
            points[0].Y = y;
            for (int idx = 0; idx < length / 2; idx++)
            {
                points[idx + 1].X = value[2 * idx];
                points[idx + 1].Y = value[2 * idx + 1];
            }
            DrawLines(pen, points);
        }

        // ----- DrawBezier ---------------------------------------------------------------------------




        /// <summary>
        /// Draws a Bézier spline defined by four points.
        /// </summary>
        public void DrawBezier(XPen pen, XPoint pt1, XPoint pt2, XPoint pt3, XPoint pt4)
        {
            DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
        }

        /// <summary>
        /// Draws a Bézier spline defined by four points.
        /// </summary>
        public void DrawBezier(XPen pen, double x1, double y1, double x2, double y2,
          double x3, double y3, double x4, double y4)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawBeziers(pen, [new XPoint(x1, y1), new XPoint(x2, y2), new XPoint(x3, y3), new XPoint(x4, y4)]);
        }

        // ----- DrawBeziers --------------------------------------------------------------------------




        /// <summary>
        /// Draws a series of Bézier splines from an array of points.
        /// </summary>
        public void DrawBeziers(XPen pen, XPoint[] points)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            int count = points.Length;
            if (count == 0)
                return;

            if ((count - 1) % 3 != 0)
                throw new ArgumentException("Invalid number of points for bezier curves. Number must fulfill 4+3n.", nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawBeziers(pen, points);
        }

        // ----- DrawCurve ----------------------------------------------------------------------------




        /// <summary>
        /// Draws a cardinal spline through a specified array of points.
        /// </summary>
        public void DrawCurve(XPen pen, XPoint[] points)
        {
            DrawCurve(pen, points, 0.5);
        }




        /// <summary>
        /// Draws a cardinal spline through a specified array of point using a specified tension.
        /// The drawing begins offset from the beginning of the array.
        /// </summary>
        public void DrawCurve(XPen pen, XPoint[] points, int offset, int numberOfSegments, double tension)
        {
            XPoint[] points2 = new XPoint[numberOfSegments];
            Array.Copy(points, offset, points2, 0, numberOfSegments);
            DrawCurve(pen, points2, tension);
        }

        /// <summary>
        /// Draws a cardinal spline through a specified array of points using a specified tension. 
        /// </summary>
        public void DrawCurve(XPen pen, XPoint[] points, double tension)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            int count = points.Length;
            if (count < 2)
                throw new ArgumentException("DrawCurve requires two or more points.", nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawCurve(pen, points, tension);
        }

        // ----- DrawArc ------------------------------------------------------------------------------



        /// <summary>
        /// Draws an arc representing a portion of an ellipse.
        /// </summary>
        public void DrawArc(XPen pen, XRect rect, double startAngle, double sweepAngle)
        {
            DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Draws an arc representing a portion of an ellipse.
        /// </summary>
        public void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            if (Math.Abs(sweepAngle) >= 360)
            {
                DrawEllipse(pen, x, y, width, height);
            }
            else
            {
                if (_drawGraphics)
                {
                }

                _renderer?.DrawArc(pen, x, y, width, height, startAngle, sweepAngle);
            }
        }

        // ----- DrawRectangle ------------------------------------------------------------------------

        // ----- stroke -----



        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XPen pen, XRect rect)
        {
            DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XPen pen, double x, double y, double width, double height)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawRectangle(pen, null, x, y, width, height);
        }

        // ----- fill -----



        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XBrush brush, XRect rect)
        {
            DrawRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XBrush brush, double x, double y, double width, double height)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawRectangle(null, brush, x, y, width, height);
        }

        // ----- stroke and fill -----



        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XPen? pen, XBrush? brush, XRect rect)
        {
            DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        public void DrawRectangle(XPen? pen, XBrush? brush, double x, double y, double width, double height)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }

            if (_drawGraphics)
            {
            }

            _renderer?.DrawRectangle(pen, brush, x, y, width, height);
        }

        // ----- DrawRectangles -----------------------------------------------------------------------

        // ----- stroke -----



        /// <summary>
        /// Draws a series of rectangles.
        /// </summary>
        public void DrawRectangles(XPen pen, XRect[] rectangles)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (rectangles == null)
                throw new ArgumentNullException(nameof(rectangles));

            DrawRectangles(pen, null, rectangles);
        }

        // ----- fill -----



        /// <summary>
        /// Draws a series of rectangles.
        /// </summary>
        public void DrawRectangles(XBrush brush, XRect[] rectangles)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));
            if (rectangles == null)
                throw new ArgumentNullException(nameof(rectangles));

            DrawRectangles(null, brush, rectangles);
        }

        // ----- stroke and fill -----



        /// <summary>
        /// Draws a series of rectangles.
        /// </summary>
        public void DrawRectangles(XPen? pen, XBrush? brush, XRect[] rectangles)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }
            if (rectangles == null)
                throw new ArgumentNullException(nameof(rectangles));

            int count = rectangles.Length;
            if (_drawGraphics)
            {
            }

            if (_renderer != null)
            {
                for (int idx = 0; idx < count; idx++)
                {
                    XRect rect = rectangles[idx];
                    _renderer.DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        // ----- DrawRoundedRectangle -----------------------------------------------------------------

        // ----- stroke -----




        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XPen pen, XRect rect, XSize ellipseSize)
        {
            DrawRoundedRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
        }

        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XPen pen, double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            DrawRoundedRectangle(pen, null, x, y, width, height, ellipseWidth, ellipseHeight);
        }

        // ----- fill -----




        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XBrush brush, XRect rect, XSize ellipseSize)
        {
            DrawRoundedRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
        }

        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XBrush brush, double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            DrawRoundedRectangle(null, brush, x, y, width, height, ellipseWidth, ellipseHeight);
        }

        // ----- stroke and fill -----




        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XPen? pen, XBrush? brush, XRect rect, XSize ellipseSize)
        {
            DrawRoundedRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
        }

        /// <summary>
        /// Draws a rectangle with rounded corners.
        /// </summary>
        public void DrawRoundedRectangle(XPen? pen, XBrush? brush, double x, double y, double width, double height,
            double ellipseWidth, double ellipseHeight)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }

            if (_drawGraphics)
            {
            }

            _renderer?.DrawRoundedRectangle(pen, brush, x, y, width, height, ellipseWidth, ellipseHeight);
        }

        // ----- DrawEllipse --------------------------------------------------------------------------

        // ----- stroke -----



        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XPen pen, XRect rect)
        {
            DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XPen pen, double x, double y, double width, double height)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));

            // No DrawArc defined?
            if (_drawGraphics)
            {
            }

            _renderer?.DrawEllipse(pen, null, x, y, width, height);
        }

        // ----- fill -----



        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XBrush brush, XRect rect)
        {
            DrawEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XBrush brush, double x, double y, double width, double height)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawEllipse(null, brush, x, y, width, height);
        }

        // ----- stroke and fill -----



        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XPen? pen, XBrush? brush, XRect rect)
        {
            DrawEllipse(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws an ellipse defined by a bounding rectangle.
        /// </summary>
        public void DrawEllipse(XPen? pen, XBrush? brush, double x, double y, double width, double height)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }

            if (_drawGraphics)
            {
            }

            _renderer?.DrawEllipse(pen, brush, x, y, width, height);
        }

        // ----- DrawPolygon --------------------------------------------------------------------------

        // ----- stroke -----




        /// <summary>
        /// Draws a polygon defined by an array of points.
        /// </summary>
        public void DrawPolygon(XPen pen, XPoint[] points)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Length < 2)
                throw new ArgumentException(PsMsgs.PointArrayAtLeast(2), nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPolygon(pen, null, points, XFillMode.Alternate);  // XFillMode is ignored
        }

        // ----- fill -----




        /// <summary>
        /// Draws a polygon defined by an array of points.
        /// </summary>
        public void DrawPolygon(XBrush brush, XPoint[] points, XFillMode fillMode)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Length < 2)
                throw new ArgumentException(PsMsgs.PointArrayAtLeast(2), nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPolygon(null, brush, points, fillMode);
        }

        // ----- stroke and fill -----




        /// <summary>
        /// Draws a polygon defined by an array of points.
        /// </summary>
        public void DrawPolygon(XPen? pen, XBrush? brush, XPoint[] points, XFillMode fillMode)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            if (points.Length < 2)
                throw new ArgumentException(PsMsgs.PointArrayAtLeast(2), nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPolygon(pen, brush, points, fillMode);
        }

        // ----- DrawPie ------------------------------------------------------------------------------

        // ----- stroke -----



        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XPen pen, XRect rect, double startAngle, double sweepAngle)
        {
            DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen), PsMsgs.NeedPenOrBrush);

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPie(pen, null, x, y, width, height, startAngle, sweepAngle);
        }

        // ----- fill -----



        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XBrush brush, XRect rect, double startAngle, double sweepAngle)
        {
            DrawPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XBrush brush, double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush), PsMsgs.NeedPenOrBrush);

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPie(null, brush, x, y, width, height, startAngle, sweepAngle);
        }

        // ----- stroke and fill -----



        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XPen? pen, XBrush? brush, XRect rect, double startAngle, double sweepAngle)
        {
            DrawPie(pen, brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Draws a pie defined by an ellipse.
        /// </summary>
        public void DrawPie(XPen? pen, XBrush? brush, double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            if (pen == null && brush == null)
                throw new ArgumentNullException(nameof(pen), PsMsgs.NeedPenOrBrush);

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPie(pen, brush, x, y, width, height, startAngle, sweepAngle);
        }

        // ----- DrawClosedCurve ----------------------------------------------------------------------

        // ----- stroke -----




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XPen pen, XPoint[] points)
        {
            DrawClosedCurve(pen, null, points, XFillMode.Alternate, 0.5);
        }




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XPen pen, XPoint[] points, double tension)
        {
            DrawClosedCurve(pen, null, points, XFillMode.Alternate, tension);
        }

        // ----- fill -----




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XBrush brush, XPoint[] points)
        {
            DrawClosedCurve(null, brush, points, XFillMode.Alternate, 0.5);
        }




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XBrush brush, XPoint[] points, XFillMode fillMode)
        {
            DrawClosedCurve(null, brush, points, fillMode, 0.5);
        }




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XBrush brush, XPoint[] points, XFillMode fillMode, double tension)
        {
            DrawClosedCurve(null, brush, points, fillMode, tension);
        }

        // ----- stroke and fill -----




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XPen? pen, XBrush? brush, XPoint[] points)
        {
            DrawClosedCurve(pen, brush, points, XFillMode.Alternate, 0.5);
        }




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XPen? pen, XBrush? brush, XPoint[] points, XFillMode fillMode)
        {
            DrawClosedCurve(pen, brush, points, fillMode, 0.5);
        }




        /// <summary>
        /// Draws a closed cardinal spline defined by an array of points.
        /// </summary>
        public void DrawClosedCurve(XPen? pen, XBrush? brush, XPoint[] points, XFillMode fillMode, double tension)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }

            int count = points.Length;
            if (count == 0)
                return;
            if (count < 2)
                throw new ArgumentException("Not enough points.", nameof(points));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawClosedCurve(pen, brush, points, tension, fillMode);
        }

        // ----- DrawPath -----------------------------------------------------------------------------

        // ----- stroke -----

        /// <summary>
        /// Draws a graphical path.
        /// </summary>
        public void DrawPath(XPen pen, XGraphicsPath path)
        {
            if (pen == null)
                throw new ArgumentNullException(nameof(pen));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPath(pen, null, path);
        }

        // ----- fill -----

        /// <summary>
        /// Draws a graphical path.
        /// </summary>
        public void DrawPath(XBrush brush, XGraphicsPath path)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPath(null, brush, path);
        }

        // ----- stroke and fill -----

        /// <summary>
        /// Draws a graphical path.
        /// </summary>
        public void DrawPath(XPen? pen, XBrush? brush, XGraphicsPath path)
        {
            if (pen == null && brush == null)
            {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException(nameof(pen) + " and " + nameof(brush), PsMsgs.NeedPenOrBrush);
            }
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (_drawGraphics)
            {
            }

            _renderer?.DrawPath(pen, brush, path);
        }

        // ----- DrawString ---------------------------------------------------------------------------


        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string s, XFont font, XBrush brush, XPoint point)
        {
            DrawString(s, font, brush, new XRect(point.X, point.Y, 0, 0), XStringFormats.Default);
        }


        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string s, XFont font, XBrush brush, XPoint point, XStringFormat format)
        {
            DrawString(s, font, brush, new XRect(point.X, point.Y, 0, 0), format);
        }

        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string s, XFont font, XBrush brush, double x, double y)
        {
            DrawString(s, font, brush, new XRect(x, y, 0, 0), XStringFormats.Default);
        }

        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string s, XFont font, XBrush brush, double x, double y, XStringFormat format)
        {
            DrawString(s, font, brush, new XRect(x, y, 0, 0), format);
        }


        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string s, XFont font, XBrush brush, XRect layoutRectangle)
        {
            DrawString(s, font, brush, layoutRectangle, XStringFormats.Default);
        }


        /// <summary>
        /// Draws the specified text string.
        /// </summary>
        public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle, XStringFormat format)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            if (format is { LineAlignment: XLineAlignment.BaseLine } && layoutRectangle.Height != 0)
                throw new InvalidOperationException("DrawString: With XLineAlignment.BaseLine the height of the layout rectangle must be 0.");

            if (text.Length == 0)
                return;

            format ??= XStringFormats.Default;

            // format cannot be null below this line.

            if (_drawGraphics)
            {
            }
            _renderer?.DrawString(text, font, brush, layoutRectangle, format);
        }

        // ----- MeasureString ------------------------------------------------------------------------

        /// <summary>
        /// Measures the specified string when drawn with the specified font.
        /// </summary>
        public XSize MeasureString(string text, XFont font, XStringFormat stringFormat)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (stringFormat == null)
                throw new ArgumentNullException(nameof(stringFormat));
#if true
            var renderEvents = RenderEvents ?? Owner?.RenderEvents;
            // Allocate event arguments only if somebody listens. Formerly, every measured string allocated them.
            if (renderEvents is { HasPrepareTextHandlers: true })
            {
                // Invoke PrepareTextEvent.
                var args = new PrepareTextEventArgs(Owner!, font, text); // Owner may be null here.
                renderEvents.OnPrepareTextEvent(this, args);
                text = args.Text;
            }

            //var codePoints = UnicodeHelper.Utf32FromString(text /*, font.AnsiEncoding*/);
            var codePoints = font.IsSymbolFont
                ? UnicodeHelper.SymbolCodePointsFromString(text, font.OpenTypeDescriptor)
                : UnicodeHelper.Utf32FromString(text /*, font.AnsiEncoding*/);

            var otDescriptor = font.OpenTypeDescriptor;
            var codePointsWithGlyphIndices = otDescriptor.GlyphIndicesFromCodePoints(codePoints);

            if (renderEvents is { HasRenderTextHandlers: true })
            {
                // Invoke RenderTextEvent.
                var args = new RenderTextEventArgs(Owner!, font, codePointsWithGlyphIndices); // Owner may be null here.
                renderEvents.OnRenderTextEvent(this, args);
                codePointsWithGlyphIndices = args.CodePointGlyphIndexPairs;
                if (args.ReevaluateGlyphIndices)
                {
                    codePoints = args.CodePointGlyphIndexPairs.Select(x => x.CodePoint).ToArray();
                    codePointsWithGlyphIndices = otDescriptor.GlyphIndicesFromCodePoints(codePoints);
                }
            }
#if true_  // renderEvents being null is possible and OK.
            else
            {
                // We always need a target for RenderEvent.
                // TODO_OLD: Check whether this can happen.
                PdfSharpLogHost.FontManagementLogger.LogCritical(
                    "MeasureString could not invoke OnRenderTextEvent because we have no owning PDF document in the current context and no renderEvent parameter.");
                throw new InvalidOperationException("No owning document or valid renderEvent.");
            }
#endif
            return FontHelper.MeasureString(codePointsWithGlyphIndices, font/*, stringFormat*/);
#else

            XSize size = FontHelper.MeasureString(text, font, XStringFormats.Default);
            return size;
#endif
        }

        /// <summary>
        /// Measures the specified string when drawn with the specified font.
        /// </summary>
        public XSize MeasureString(string text, XFont font) => MeasureString(text, font, XStringFormats.Default);

        // ----- DrawImage ----------------------------------------------------------------------------




        /// <summary>
        /// Draws the specified image.
        /// </summary>
        public void DrawImage(XImage image, XPoint point)
        {
            DrawImage(image, point.X, point.Y);
        }

        /// <summary>
        /// Draws the specified image.
        /// </summary>
        public void DrawImage(XImage image, double x, double y)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            CheckXPdfFormConsistence(image);

            double width = image.PointWidth;
            double height = image.PointHeight;

            if (_drawGraphics)
            {
            }

            _renderer?.DrawImage(image, x, y, image.PointWidth, image.PointHeight);
            //image.Width * 72 / image.HorizontalResolution,
            //image.Height * 72 / image.HorizontalResolution);
        }



        /// <summary>
        /// Draws the specified image.
        /// </summary>
        public void DrawImage(XImage image, XRect rect)
        {
            DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Draws the specified image.
        /// </summary>
        public void DrawImage(XImage image, double x, double y, double width, double height)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            CheckXPdfFormConsistence(image);

            if (_drawGraphics)
            {
                // THHO4STLA: Platform-independent images cannot be drawn here, can they?    => They can. Lazy create platform-dependent image and draw that.
            }

            _renderer?.DrawImage(image, x, y, width, height);
        }

        // TODO_OLD: calculate destination size
        //public void DrawImage(XImage image, double x, double y, GdiRectF srcRect, XGraphicsUnit srcUnit)
        //public void DrawImage(XImage image, double x, double y, XRect srcRect, XGraphicsUnit srcUnit)



        /// <summary>
        /// Draws the specified image.
        /// </summary>
        public void DrawImage(XImage image, XRect destRect, XRect srcRect, XGraphicsUnit srcUnit)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            CheckXPdfFormConsistence(image);

            if (_drawGraphics)
            {
            }

            _renderer?.DrawImage(image, destRect, srcRect, srcUnit);
        }

        //public void DrawImage(XImage image, Rectangle destRect, double srcX, double srcY, double srcWidth, double srcHeight, GraphicsUnit srcUnit);
        //public void DrawImage(XImage image, Rectangle destRect, double srcX, double srcY, double srcWidth, double srcHeight, GraphicsUnit srcUnit);

        void DrawMissingImageRect(XRect rect)
        {
        }

        /// <summary>
        /// Checks whether drawing is allowed and disposes the XGraphics object, if necessary.
        /// </summary>
        void CheckXPdfFormConsistence(XImage image)
        {
            if (image is XForm xForm)
            {
                // Force disposing of XGraphics that draws the content
                xForm.Finish();

                // ReSharper disable once MergeSequentialChecks
                if (_renderer != null && (_renderer as XGraphicsPdfRenderer) != null)
                {
                    if (xForm.Owner != null && xForm.Owner != ((XGraphicsPdfRenderer)_renderer).Owner)
                        throw new InvalidOperationException(
                            "A XPdfForm object is always bound to the document it was created for and cannot be drawn in the context of another document.");

                    if (xForm == ((XGraphicsPdfRenderer)_renderer)._form)
                        throw new InvalidOperationException(
                            "A XPdfForm cannot be drawn on itself.");
                }
            }
        }

#if false
// #barcode => extension method
        // ----- DrawBarCode --------------------------------------------------------------------------

        /// <summary>
        /// Draws the specified bar code.
        /// </summary>
        public void DrawBarCode(BarCodes.BarCode barcode, XPoint position)
        {
            barcode.Render(this, XBrushes.Black, null, position);
        }

        /// <summary>
        /// Draws the specified bar code.
        /// </summary>
        public void DrawBarCode(BarCodes.BarCode barcode, XBrush brush, XPoint position)
        {
            barcode.Render(this, brush, null, position);
        }

        /// <summary>
        /// Draws the specified bar code.
        /// </summary>
        public void DrawBarCode(BarCodes.BarCode barcode, XBrush brush, XFont font, XPoint position)
        {
            barcode.Render(this, brush, font, position);
        }

        // ----- DrawMatrixCode -----------------------------------------------------------------------

        /// <summary>
        /// Draws the specified data matrix code.
        /// </summary>
        public void DrawMatrixCode(BarCodes.MatrixCode matrixcode, XPoint position)
        {
            matrixcode.Render(this, XBrushes.Black, position);
        }

        /// <summary>
        /// Draws the specified data matrix code.
        /// </summary>
        public void DrawMatrixCode(BarCodes.MatrixCode matrixcode, XBrush brush, XPoint position)
        {
            matrixcode.Render(this, brush, position);
        }
#endif

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Save and Restore

        /// <summary>
        /// Saves the current state of this XGraphics object and identifies the saved state with the
        /// returned XGraphicsState object.
        /// </summary>
        public XGraphicsState Save()
        {
            XGraphicsState xState = null!;
            if (TargetContext == XGraphicTargetContext.CORE || TargetContext == XGraphicTargetContext.NONE)
            {
                xState = new XGraphicsState();
                InternalGraphicsState iState = new InternalGraphicsState(this, xState)
                {
                    Transform = _transformationMatrix
                };
                _gsStack.Push(iState);
            }
            else
            {
                Debug.Assert(false, "XGraphicTargetContext must be XGraphicTargetContext.CORE.");
            }
            _renderer?.Save(xState);

            return xState;
        }

        /// <summary>
        /// Restores the state of this XGraphics object to the state represented by the specified 
        /// XGraphicsState object.
        /// </summary>
        public void Restore(XGraphicsState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (TargetContext == XGraphicTargetContext.CORE)
            {
                _gsStack.Restore(state.InternalState);
                _transformationMatrix = state.InternalState.Transform;
            }
            _renderer?.Restore(state);
        }

        /// <summary>
        /// Restores the state of this XGraphics object to the state before the most recently call of Save.
        /// </summary>
        public void Restore()
        {
            if (_gsStack.Count == 0)
                throw new InvalidOperationException("Cannot restore without preceding save operation.");
            Restore(_gsStack.Current.State);
        }

        /// <summary>
        /// Saves a graphics container with the current state of this XGraphics and 
        /// opens and uses a new graphics container.
        /// </summary>
        public XGraphicsContainer BeginContainer()
        {
            return BeginContainer(new XRect(0, 0, 1, 1), new XRect(0, 0, 1, 1), XGraphicsUnit.Point);
        }




        /// <summary>
        /// Saves a graphics container with the current state of this XGraphics and 
        /// opens and uses a new graphics container.
        /// </summary>
        public XGraphicsContainer BeginContainer(XRect dstRect, XRect srcRect, XGraphicsUnit unit)
        {
            // TODO_OLD: unit
            if (unit != XGraphicsUnit.Point)
                throw new ArgumentException("The current implementation supports XGraphicsUnit.Point only.", nameof(unit));

            XGraphicsContainer? xContainer = null;
            if (TargetContext == XGraphicTargetContext.CORE)
                xContainer = new XGraphicsContainer();

            if (xContainer is null)
                throw new InvalidOperationException(nameof(xContainer));

            InternalGraphicsState iState = new InternalGraphicsState(this, xContainer);
            iState.Transform = _transformationMatrix;

            _gsStack.Push(iState);

            _renderer?.BeginContainer(xContainer, dstRect, srcRect, unit);

            XMatrix matrix = new XMatrix();
            double scaleX = dstRect.Width / srcRect.Width;
            double scaleY = dstRect.Height / srcRect.Height;
            matrix.TranslatePrepend(-srcRect.X, -srcRect.Y);
            matrix.ScalePrepend(scaleX, scaleY);
            matrix.TranslatePrepend(dstRect.X / scaleX, dstRect.Y / scaleY);
            AddTransform(matrix, XMatrixOrder.Prepend);

            return xContainer;
        }

        /// <summary>
        /// Closes the current graphics container and restores the state of this XGraphics 
        /// to the state saved by a call to the BeginContainer method.
        /// </summary>
        public void EndContainer(XGraphicsContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            _gsStack.Restore(container.InternalState);
            // Nothing to do.
            _transformationMatrix = container.InternalState.Transform;

            _renderer?.EndContainer(container);
        }

        /// <summary>
        /// Gets the current graphics state level. The default value is 0. Each call of Save or BeginContainer
        /// increased and each call of Restore or EndContainer decreased the value by 1.
        /// </summary>
        public int GraphicsStateLevel
        {
            get { return _gsStack.Count; }
        }

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Properties

        /// <summary>
        /// Gets or sets the smoothing mode.
        /// </summary>
        /// <value>The smoothing mode.</value>
        public XSmoothingMode SmoothingMode
        {
            get
            {
                // Nothing to do.
                return _smoothingMode;
            }
            set
            {
                _smoothingMode = value;
                // Nothing to do.
            }
        }
        XSmoothingMode _smoothingMode;

        //public Region Clip { get; set; }
        //public GdiRectF ClipBounds { get; }
        //public CompositingMode CompositingMode { get; set; }
        //public CompositingQuality CompositingQuality { get; set; }
        //public float DpiX { get; }
        //public float DpiY { get; }
        //public InterpolationMode InterpolationMode { get; set; }
        //public bool IsClipEmpty { get; }
        //public bool IsVisibleClipEmpty { get; }
        //public float PageScale { get; set; }
        //public GraphicsUnit PageUnit { get; set; }
        //public PixelOffsetMode PixelOffsetMode { get; set; }
        //public Point RenderingOrigin { get; set; }
        //public SmoothingMode SmoothingMode { get; set; }
        //public int TextContrast { get; set; }
        //public TextRenderingHint TextRenderingHint { get; set; }
        //public Matrix Transform { get; set; }
        //public GdiRectF VisibleClipBounds { get; }

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Transformation

        /// <summary>
        /// Applies the specified translation operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void TranslateTransform(double dx, double dy)
        {
            AddTransform(XMatrix.CreateTranslation(dx, dy), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified translation operation to the transformation matrix of this object
        /// in the specified order.
        /// </summary>
        public void TranslateTransform(double dx, double dy, XMatrixOrder order)
        {
            var matrix = new XMatrix();
            matrix.TranslatePrepend(dx, dy);
            AddTransform(matrix, order);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void ScaleTransform(double scaleX, double scaleY)
        {
            AddTransform(XMatrix.CreateScaling(scaleX, scaleY), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object
        /// in the specified order.
        /// </summary>
        public void ScaleTransform(double scaleX, double scaleY, XMatrixOrder order)
        {
            XMatrix matrix = new XMatrix();
            matrix.ScalePrepend(scaleX, scaleY);
            AddTransform(matrix, order);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public void ScaleTransform(double scaleXY)
        {
            ScaleTransform(scaleXY, scaleXY);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object
        /// in the specified order.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public void ScaleTransform(double scaleXY, XMatrixOrder order)
        {
            ScaleTransform(scaleXY, scaleXY, order);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void ScaleAtTransform(double scaleX, double scaleY, double centerX, double centerY)
        {
            AddTransform(XMatrix.CreateScaling(scaleX, scaleY, centerX, centerY), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified scaling operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void ScaleAtTransform(double scaleX, double scaleY, XPoint center)
        {
            AddTransform(XMatrix.CreateScaling(scaleX, scaleY, center.X, center.Y), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified rotation operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void RotateTransform(double angle)
        {
            AddTransform(XMatrix.CreateRotationRadians(angle * Const.Deg2Rad), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified rotation operation to the transformation matrix of this object
        /// in the specified order. The angle unit of measure is degree.
        /// </summary>
        public void RotateTransform(double angle, XMatrixOrder order)
        {
            XMatrix matrix = new XMatrix();
            matrix.RotatePrepend(angle);
            AddTransform(matrix, order);
        }

        /// <summary>
        /// Applies the specified rotation operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void RotateAtTransform(double angle, XPoint point)
        {
            AddTransform(XMatrix.CreateRotationRadians(angle * Const.Deg2Rad, point.X, point.Y), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified rotation operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// </summary>
        public void RotateAtTransform(double angle, XPoint point, XMatrixOrder order)
        {
            AddTransform(XMatrix.CreateRotationRadians(angle * Const.Deg2Rad, point.X, point.Y), order);
        }

        /// <summary>
        /// Applies the specified shearing operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// ShearTransform is a synonym for SkewAtTransform.
        /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
        /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
        /// </summary>
        public void ShearTransform(double shearX, double shearY)
        {
            AddTransform(XMatrix.CreateSkewRadians(shearX * Const.Deg2Rad, shearY * Const.Deg2Rad), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified shearing operation to the transformation matrix of this object
        /// in the specified order.
        /// ShearTransform is a synonym for SkewAtTransform.
        /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
        /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
        /// </summary>
        public void ShearTransform(double shearX, double shearY, XMatrixOrder order)
        {
            AddTransform(XMatrix.CreateSkewRadians(shearX * Const.Deg2Rad, shearY * Const.Deg2Rad), order);
        }

        /// <summary>
        /// Applies the specified shearing operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// ShearTransform is a synonym for SkewAtTransform.
        /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
        /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
        /// </summary>
        public void SkewAtTransform(double shearX, double shearY, double centerX, double centerY)
        {
            AddTransform(XMatrix.CreateSkewRadians(shearX * Const.Deg2Rad, shearY * Const.Deg2Rad, centerX, centerY), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Applies the specified shearing operation to the transformation matrix of this object by 
        /// prepending it to the object’s transformation matrix.
        /// ShearTransform is a synonym for SkewAtTransform.
        /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
        /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
        /// </summary>
        public void SkewAtTransform(double shearX, double shearY, XPoint center)
        {
            AddTransform(XMatrix.CreateSkewRadians(shearX * Const.Deg2Rad, shearY * Const.Deg2Rad, center.X, center.Y), XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Multiplies the transformation matrix of this object and specified matrix.
        /// </summary>
        public void MultiplyTransform(XMatrix matrix)
        {
            AddTransform(matrix, XMatrixOrder.Prepend);
        }

        /// <summary>
        /// Multiplies the transformation matrix of this object and specified matrix in the specified order.
        /// </summary>
        public void MultiplyTransform(XMatrix matrix, XMatrixOrder order)
        {
            AddTransform(matrix, order);
        }

        /// <summary>
        /// Gets the current transformation matrix.
        /// The transformation matrix cannot be set. Instead use Save/Restore or BeginContainer/EndContainer to
        /// save the state before Transform is called and later restore to the previous transform.
        /// </summary>
        public XMatrix Transform => _transformationMatrix;

        /// <summary>
        /// Applies a new transformation to the current transformation matrix.
        /// </summary>
        void AddTransform(XMatrix transform, XMatrixOrder order)
        {
            XMatrix matrix = _transformationMatrix;
            matrix.Multiply(transform, order);
            _transformationMatrix = matrix;
            matrix = DefaultViewMatrix;
            matrix.Multiply(_transformationMatrix, XMatrixOrder.Prepend);
#if CORE_ // No concept of target context in Core build.
            if (TargetContext == XGraphicTargetContext.CORE)
            {
                _ = typeof(int);
            }
#endif
            _renderer?.AddTransform(transform, XMatrixOrder.Prepend);
        }

        //public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, Point[] points)
        //{
        //}
        //
        //public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, GdiPointF[] points)
        //{
        //}

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Clipping



        /// <summary>
        /// Updates the clip region of this XGraphics to the intersection of the 
        /// current clip region and the specified rectangle.
        /// </summary>
        public void IntersectClip(XRect rect)
        {
            XGraphicsPath path = new XGraphicsPath();
            path.AddRectangle(rect);
            IntersectClip(path);
        }

        /// <summary>
        /// Updates the clip region of this XGraphics to the intersection of the 
        /// current clip region and the specified graphical path.
        /// </summary>
        public void IntersectClip(XGraphicsPath path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (_drawGraphics)
            {
            }

            _renderer?.SetClip(path, XCombineMode.Intersect);
        }

        //public void SetClip(Graphics g);
        //public void SetClip(Graphics g, CombineMode combineMode);
        //public void SetClip(GraphicsPath path, CombineMode combineMode);
        //public void SetClip(Rectangle rect, CombineMode combineMode);
        //public void SetClip(GdiRectF rect, CombineMode combineMode);
        //public void SetClip(Region region, CombineMode combineMode);
        //public void IntersectClip(Region region);
        //public void ExcludeClip(Region region);

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Miscellaneous

        /// <summary>
        /// Writes a comment to the output stream. Comments have no effect on the rendering of the output.
        /// They may be useful to mark a position in a content stream of a page in a PDF document.
        /// </summary>
        public void WriteComment(string comment)
        {
            if (comment == null)
                throw new ArgumentNullException(nameof(comment));
            _renderer?.WriteComment(comment);
        }

        /// <summary>
        /// Permits access to internal data.
        /// </summary>
        public XGraphicsInternals Internals
            => _internals ??= new(this);

        XGraphicsInternals _internals = default!;

        /// <summary>
        /// (Under construction. May change in future versions.)
        /// </summary>
#if true_  // Change to 'true_' if code does not compile.
        public SpaceTransformer Transformer
            => field ??= new SpaceTransformer(this);

#else
        public SpaceTransformer Transformer
            => _transformer ??= new SpaceTransformer(this);

        SpaceTransformer? _transformer;
#endif

        #endregion

        // --------------------------------------------------------------------------------------------

        #region Internal Helper Functions








        #endregion

        ///// <summary>
        ///// Testcode
        ///// </summary>
        //public void TestXObject(PdfDocument thisDoc, PdfPage thisPage, int page,
        //      PdfDocument externalDoc, ImportedObjectTable impDoc)
        //{
        //    PdfPage impPage = externalDoc.Pages[page];
        //    //      impDoc.ImportPage(impPage);
        //    PdfFormXObject form = new PdfFormXObject(thisDoc, impDoc, impPage);
        //    thisDoc.xrefTable.Add(form);

        //    PdfDictionary xobjects = new PdfDictionary();
        //    xobjects.Elements["/X42"] = form.XRef;
        //    thisPage.Resources.Elements[PdfResources.Keys.XObject] = xobjects;
        //    ((XGraphicsPdfRenderer)renderer).DrawXObject("/X42");
        //}

        internal void DisassociateImage()
        {
            if (_associatedImage == null)
                throw new InvalidOperationException("No image associated.");

            Dispose();
        }

        internal InternalGraphicsMode InternalGraphicsMode
        {
            get { return _internalGraphicsMode; }
            set { _internalGraphicsMode = value; }
        }
        InternalGraphicsMode _internalGraphicsMode;

        internal XImage? AssociatedImage
        {
            get { return _associatedImage; }
            set { _associatedImage = value; }
        }
        XImage? _associatedImage;




        /// <summary>
        /// The transformation matrix from the XGraphics page space to the Graphics world space.
        /// (The name 'default view matrix' comes from Microsoft OS/2 Presentation Manager. I chose
        /// this name because I have no better one.)
        /// </summary>
        internal XMatrix DefaultViewMatrix;

        /// <summary>
        /// Indicates whether to send drawing operations to _gfx or _dc.
        /// </summary>
        bool _drawGraphics;

        readonly XForm _form = default!;


        /// <summary>
        /// Interface to an (optional) renderer. Currently, it is the XGraphicsPdfRenderer, if defined.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        internal IXGraphicsRenderer? _renderer;

        // #PDF-UA
        internal void AppendToContentStream(string str)
        {
            if (_renderer is XGraphicsPdfRenderer renderer)
                renderer.Append(str);
        }

        /// <summary>
        /// The transformation matrix from XGraphics world space to page unit space.
        /// </summary>
        XMatrix _transformationMatrix;

        /// <summary>
        /// The graphics state stack.
        /// </summary>
        readonly GraphicsStateStack _gsStack;

        /// <summary>
        /// Gets the PDF page that serves as drawing surface if PDF is rendered,
        /// or null if no such object exists.
        /// </summary>
        public PdfPage? PdfPage
            => _renderer is XGraphicsPdfRenderer renderer ? renderer._page : null;


        //#if CORE || GDI
        //        /// <summary>
        //        /// Critical section used to serialize access to GDI+.
        //        /// This may be necessary to use PDFsharp safely in a Web application.
        //        /// </summary>
        //        internal static readonly object GdiPlus = new object();
        //#endif

        /// <summary>
        /// Provides access to internal data structures of the XGraphics class.
        /// </summary>
        public class XGraphicsInternals
        {
            internal XGraphicsInternals(XGraphics gfx)
            {
                _gfx = gfx;
            }
            readonly XGraphics _gfx;

            public string? RealizedFontName
            {
                get
                {
                    if (_gfx._renderer is XGraphicsPdfRenderer renderer)
                        return renderer.PdfGraphicsState.RealizedFontName;
                    return null;
                }
            }

            /// <summary>
            /// Gets the content string builder of XGraphicsPdfRenderer, if it exists.
            /// </summary>
            public StringBuilder? ContentStringBuilder
            {
                get
                {
                    if (_gfx._renderer is XGraphicsPdfRenderer renderer)
                        return renderer._content;
                    return null;
                }
            }
        }

        /// <summary>
        /// (This class is under construction.)
        /// Currently used in MigraDoc only.
        /// </summary>
        public class SpaceTransformer
        {
            internal SpaceTransformer(XGraphics gfx)
            {
                _gfx = gfx;
            }
            readonly XGraphics _gfx;

            /// <summary>
            /// Gets the smallest rectangle in default page space units that completely encloses the specified rect
            /// in world space units.
            /// </summary>
            public XRect WorldToDefaultPage(XRect rect)
            {
                XPoint[] points = new XPoint[4];
                points[0] = new XPoint(rect.X, rect.Y);
                points[1] = new XPoint(rect.X + rect.Width, rect.Y);
                points[2] = new XPoint(rect.X, rect.Y + rect.Height);
                points[3] = new XPoint(rect.X + rect.Width, rect.Y + rect.Height);

                XMatrix matrix = _gfx.Transform;
                matrix.TransformPoints(points);

                double height = _gfx.PageSize.Height;
                points[0].Y = height - points[0].Y;
                points[1].Y = height - points[1].Y;
                points[2].Y = height - points[2].Y;
                points[3].Y = height - points[3].Y;

                double xmin = Math.Min(Math.Min(points[0].X, points[1].X), Math.Min(points[2].X, points[3].X));
                double xmax = Math.Max(Math.Max(points[0].X, points[1].X), Math.Max(points[2].X, points[3].X));
                double ymin = Math.Min(Math.Min(points[0].Y, points[1].Y), Math.Min(points[2].Y, points[3].Y));
                double ymax = Math.Max(Math.Max(points[0].Y, points[1].Y), Math.Max(points[2].Y, points[3].Y));

                return new(xmin, ymin, xmax - xmin, ymax - ymin);
            }

            /// <summary>
            /// Gets a point in PDF world space units.
            /// </summary>
            public XPoint WorldToDefaultPage(XPoint point)
            {
                XMatrix matrix = _gfx.Transform;
                matrix.Transform(point); // StL/25-11-22: This does nothing!

                double height = _gfx.PageSize.Height;
                point.Y = height - point.Y;

                return point;
            }
        }
    }
}
