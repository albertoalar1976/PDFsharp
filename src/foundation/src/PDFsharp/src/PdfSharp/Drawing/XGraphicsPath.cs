// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Internal;
using PdfSharp.Internal.Threading;
using PdfSharp.Fonts.Internal;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Represents a series of connected lines and curves.
    /// </summary>
    public sealed class XGraphicsPath
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XGraphicsPath"/> class.
        /// </summary>
        public XGraphicsPath()
        {
            CorePath = new CoreGraphicsPath();
        }




        /// <summary>
        /// Clones this instance.
        /// </summary>
        public XGraphicsPath Clone()
        {
            var path = (XGraphicsPath)MemberwiseClone();
            CorePath = new CoreGraphicsPath(CorePath);
            return path;
        }



        // ----- AddLine ------------------------------------------------------------------------------




        /// <summary>
        /// Adds a line segment to current figure.
        /// </summary>
        public void AddLine(XPoint pt1, XPoint pt2)
        {
            AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);
        }

        /// <summary>
        /// Adds a line segment to current figure.
        /// </summary>
        public void AddLine(double x1, double y1, double x2, double y2)
        {
            CorePath.MoveOrLineTo(x1, y1);
            CorePath.LineTo(x2, y2, false);
        }

        // ----- AddLines -----------------------------------------------------------------------------




        /// <summary>
        /// Adds a series of connected line segments to current figure.
        /// </summary>
        public void AddLines(XPoint[] points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            int count = points.Length;
            if (count == 0)
                return;
            CorePath.MoveOrLineTo(points[0].X, points[0].Y);
            for (int idx = 1; idx < count; idx++)
                CorePath.LineTo(points[idx].X, points[idx].Y, false);
        }

        // ----- AddBezier ----------------------------------------------------------------------------




        /// <summary>
        /// Adds a cubic Bézier curve to the current figure.
        /// </summary>
        public void AddBezier(XPoint pt1, XPoint pt2, XPoint pt3, XPoint pt4)
        {
            AddBezier(pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
        }

        /// <summary>
        /// Adds a cubic Bézier curve to the current figure.
        /// </summary>
        public void AddBezier(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
        {
            CorePath.MoveOrLineTo(x1, y1);
            CorePath.BezierTo(x2, y2, x3, y3, x4, y4, false);
        }

        // ----- AddBeziers ---------------------------------------------------------------------------




        /// <summary>
        /// Adds a sequence of connected cubic Bézier curves to the current figure.
        /// </summary>
        public void AddBeziers(XPoint[] points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            int count = points.Length;
            if (count < 4)
                throw new ArgumentException("At least four points required for bezier curve.", nameof(points));

            if ((count - 1) % 3 != 0)
                throw new ArgumentException("Invalid number of points for bezier curve. Number must fulfil 4+3n.",
                    nameof(points));

            CorePath.MoveOrLineTo(points[0].X, points[0].Y);
            for (int idx = 1; idx < count; idx += 3)
            {
                CorePath.BezierTo(points[idx].X, points[idx].Y, points[idx + 1].X, points[idx + 1].Y,
                    points[idx + 2].X, points[idx + 2].Y, false);
            }
        }

        // ----- AddCurve -----------------------------------------------------------------------




        /// <summary>
        /// Adds a spline curve to the current figure.
        /// </summary>
        public void AddCurve(XPoint[] points)
        {
            AddCurve(points, 0.5);
        }




        /// <summary>
        /// Adds a spline curve to the current figure.
        /// </summary>
        public void AddCurve(XPoint[] points, double tension)
        {
            int count = points.Length;
            if (count < 2)
                throw new ArgumentException("AddCurve requires two or more points.", nameof(points));
            CorePath.AddCurve(points, tension);
        }




        /// <summary>
        /// Adds a spline curve to the current figure.
        /// </summary>
        public void AddCurve(XPoint[] points, int offset, int numberOfSegments, double tension)
        {
            throw new NotImplementedException("AddCurve not yet implemented.");
        }

        // ----- AddArc -------------------------------------------------------------------------------



        /// <summary>
        /// Adds an elliptical arc to the current figure.
        /// </summary>
        public void AddArc(XRect rect, double startAngle, double sweepAngle)
        {
            AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Adds an elliptical arc to the current figure.
        /// </summary>
        public void AddArc(double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            CorePath.AddArc(x, y, width, height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Adds an elliptical arc to the current figure. The arc is specified WPF like.
        /// </summary>
        public void AddArc(XPoint point1, XPoint point2, XSize size, double rotationAngle, bool isLargeArg, XSweepDirection sweepDirection)
        {
            CorePath.AddArc(point1, point2, size, rotationAngle, isLargeArg, sweepDirection);
        }

        // ----- AddRectangle -------------------------------------------------------------------------



        /// <summary>
        /// Adds a rectangle to this path.
        /// </summary>
        public void AddRectangle(XRect rect)
        {
            CorePath.MoveTo(rect.X, rect.Y);
            CorePath.LineTo(rect.X + rect.Width, rect.Y, false);
            CorePath.LineTo(rect.X + rect.Width, rect.Y + rect.Height, false);
            CorePath.LineTo(rect.X, rect.Y + rect.Height, true);
            CorePath.CloseSubpath();
        }

        /// <summary>
        /// Adds a rectangle to this path.
        /// </summary>
        public void AddRectangle(double x, double y, double width, double height)
        {
            AddRectangle(new XRect(x, y, width, height));
        }

        // ----- AddRectangles ------------------------------------------------------------------------



        /// <summary>
        /// Adds a series of rectangles to this path.
        /// </summary>
        public void AddRectangles(XRect[] rects)
        {
            int count = rects.Length;
            for (int idx = 0; idx < count; idx++)
            {
                AddRectangle(rects[idx]);
            }
        }

        // ----- AddRoundedRectangle ------------------------------------------------------------------





        /// <summary>
        /// Adds a rectangle with rounded corners to this path.
        /// </summary>
        public void AddRoundedRectangle(double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
        {
            double arcWidth = ellipseWidth / 2;
            double arcHeight = ellipseHeight / 2;
            CorePath.MoveTo(x + width - arcWidth, y);
            CorePath.QuadrantArcTo(x + width - arcWidth, y + arcHeight, arcWidth, arcHeight, 1, true);

            CorePath.LineTo(x + width, y + height - arcHeight, false);
            CorePath.QuadrantArcTo(x + width - arcWidth, y + height - arcHeight, arcWidth, arcHeight, 4, true);

            CorePath.LineTo(x + arcWidth, y + height, false);
            CorePath.QuadrantArcTo(x + arcWidth, y + height - arcHeight, arcWidth, arcHeight, 3, true);

            CorePath.LineTo(x, y + arcHeight, false);
            CorePath.QuadrantArcTo(x + arcWidth, y + arcHeight, arcWidth, arcHeight, 2, true);

            CorePath.CloseSubpath();
        }

        // ----- AddEllipse ---------------------------------------------------------------------------



        /// <summary>
        /// Adds an ellipse to the current path.
        /// </summary>
        public void AddEllipse(XRect rect)
        {
            AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Adds an ellipse to the current path.
        /// </summary>
        public void AddEllipse(double x, double y, double width, double height)
        {
            double w = width / 2;
            double h = height / 2;
            double xc = x + w;
            double yc = y + h;
            CorePath.MoveTo(x + w, y);
            CorePath.QuadrantArcTo(xc, yc, w, h, 1, true);
            CorePath.QuadrantArcTo(xc, yc, w, h, 4, true);
            CorePath.QuadrantArcTo(xc, yc, w, h, 3, true);
            CorePath.QuadrantArcTo(xc, yc, w, h, 2, true);
            CorePath.CloseSubpath();
        }

        // ----- AddPolygon ---------------------------------------------------------------------------




        /// <summary>
        /// Adds a polygon to this path.
        /// </summary>
        public void AddPolygon(XPoint[] points)
        {
            int count = points.Length;
            if (count == 0)
                return;

            CorePath.MoveTo(points[0].X, points[0].Y);
            for (int idx = 0; idx < count - 1; idx++)
                CorePath.LineTo(points[idx].X, points[idx].Y, false);
            CorePath.LineTo(points[count - 1].X, points[count - 1].Y, true);
            CorePath.CloseSubpath();
        }

        // ----- AddPie -------------------------------------------------------------------------------



        /// <summary>
        /// Adds the outline of a pie shape to this path.
        /// </summary>
        public void AddPie(XRect rect, double startAngle, double sweepAngle)
        {
            AddPie(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        /// <summary>
        /// Adds the outline of a pie shape to this path.
        /// </summary>
        public void AddPie(double x, double y, double width, double height, double startAngle, double sweepAngle)
        {
            const string message = "AddPie: This operation is not yet implemented in Core build.";
            DiagnosticsHelper.HandleNotImplemented(message, Capabilities.Action.PathOperations);
        }

        // ----- AddClosedCurve ------------------------------------------------------------------------




        /// <summary>
        /// Adds a closed curve to this path.
        /// </summary>
        public void AddClosedCurve(XPoint[] points)
        {
            AddClosedCurve(points, 0.5);
        }




        /// <summary>
        /// Adds a closed curve to this path.
        /// </summary>
        public void AddClosedCurve(XPoint[] points, double tension)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            int count = points.Length;
            if (count == 0)
                return;
            if (count < 2)
                throw new ArgumentException("Not enough points.", nameof(points));

            const string message = "AddClosedCurve: This operation is not yet implemented in Core build.";
            DiagnosticsHelper.HandleNotImplemented(message, Capabilities.Action.PathOperations);
        }

        // ----- AddPath ------------------------------------------------------------------------------

        /// <summary>
        /// Adds the specified path to this path.
        /// </summary>
        public void AddPath(XGraphicsPath path, bool connect)
        {
            const string message = "AddPath: This operation is not yet implemented in Core build.";
            DiagnosticsHelper.HandleNotImplemented(message, Capabilities.Action.PathOperations);
        }

        // ----- AddPath ------------------------------------------------------------------------------


        // ----- AddString ----------------------------------------------------------------------------




        /// <summary>
        /// Adds a text string to this path.
        /// </summary>
        public void AddString(string s, XFontFamily family, XFontStyleEx style, double emSize, XPoint origin,
            XStringFormat format)
        {
            try
            {
                // ReviewSTLA THHO4STLA
                // EXPERIMENTAL
                DiagnosticsHelper.HandleNotImplemented(AddStringMessage, Capabilities.Action.GlyphsToPath);
            }
            catch
            {
                throw;
            }
        }



        /// <summary>
        /// Adds a text string to this path.
        /// </summary>
        public void AddString(string s, XFontFamily family, XFontStyleEx style, double emSize, XRect layoutRect, XStringFormat format)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            if (family == null)
                throw new ArgumentNullException(nameof(family));

            if (format == null!)
                format = XStringFormats.Default;

            if (format.LineAlignment == XLineAlignment.BaseLine && layoutRect.Height != 0)
                throw new InvalidOperationException(
                    "DrawString: With XLineAlignment.BaseLine the height of the layout rectangle must be 0.");

            if (s.Length == 0)
                return;

            var font = new XFont(family.Name, emSize, style);
            DiagnosticsHelper.HandleNotImplemented(AddStringMessage, Capabilities.Action.GlyphsToPath);
        }

        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Closes the current figure and starts a new figure.
        /// </summary>
        public void CloseFigure()
        {
            CorePath.CloseSubpath();
        }

        /// <summary>
        /// Starts a new figure without closing the current figure.
        /// </summary>
        public void StartFigure()
        {
            // Nothing to do.
        }

        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets or sets an XFillMode that determines how the interiors of shapes are filled.
        /// </summary>
        public XFillMode FillMode
        {
            get { return _fillMode; }
            set
            {
                _fillMode = value;
                // Nothing to do.
            }
        }

        XFillMode _fillMode;

        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Converts each curve in this XGraphicsPath into a sequence of connected line segments. 
        /// </summary>
        public void Flatten()
        {
            // ReviewSTLA THHO4STLA
            // Just do nothing.
        }

        /// <summary>
        /// Converts each curve in this XGraphicsPath into a sequence of connected line segments. 
        /// </summary>
        public void Flatten(XMatrix matrix)
        {
            // ReviewSTLA THHO4STLA
            // Just do nothing.
        }

        /// <summary>
        /// Converts each curve in this XGraphicsPath into a sequence of connected line segments. 
        /// </summary>
        public void Flatten(XMatrix matrix, double flatness)
        {
            // ReviewSTLA THHO4STLA
            // Just do nothing.
        }

        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Replaces this path with curves that enclose the area that is filled when this path is drawn 
        /// by the specified pen.
        /// </summary>
        public void Widen(XPen pen)
        {
            // ReviewSTLA THHO4STLA
            // Just do nothing.
        }

        /// <summary>
        /// Replaces this path with curves that enclose the area that is filled when this path is drawn 
        /// by the specified pen.
        /// </summary>
        public void Widen(XPen pen, XMatrix matrix)
        {
            // Just do nothing.
            throw new NotImplementedException("XGraphicsPath.Widen");
        }

        /// <summary>
        /// Replaces this path with curves that enclose the area that is filled when this path is drawn 
        /// by the specified pen.
        /// </summary>
        public void Widen(XPen pen, XMatrix matrix, double flatness)
        {
            // Just do nothing.
        }

        /// <summary>
        /// Grants access to internal objects of this class.
        /// </summary>
        public XGraphicsPathInternals Internals => new XGraphicsPathInternals(this);

        /// <summary>
        /// Gets access to underlying Core graphics path.
        /// </summary>
        internal CoreGraphicsPath CorePath;

        const string AddStringMessage = "AddString: Converting a string into a graphical path is not available in Core build.";


    }
}