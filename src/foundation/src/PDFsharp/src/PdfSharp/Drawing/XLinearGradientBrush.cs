// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.ComponentModel;
using PdfSharp.Internal.Threading;

// ReSharper disable RedundantNameQualifier because it is required for hybrid build

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a Brush with a linear gradient.
    /// </summary>
    public sealed class XLinearGradientBrush : XGradientBrush
    {


        /// <summary>
        /// Initializes a new instance of the <see cref="XLinearGradientBrush"/> class.
        /// </summary>
        public XLinearGradientBrush(XPoint point1, XPoint point2, XColor color1, XColor color2)
        {
            _point1 = point1;
            _point2 = point2;
            _color1 = color1;
            _color2 = color2;
        }



        /// <summary>
        /// Initializes a new instance of the <see cref="XLinearGradientBrush"/> class.
        /// </summary>
        public XLinearGradientBrush(XRect rect, XColor color1, XColor color2, XLinearGradientMode linearGradientMode)
        {
            if (!Enum.IsDefined(typeof(XLinearGradientMode), linearGradientMode))
                throw new InvalidEnumArgumentException(nameof(linearGradientMode), (int)linearGradientMode, typeof(XLinearGradientMode));

            if (rect.Width == 0 || rect.Height == 0)
                throw new ArgumentException("Invalid rectangle.", nameof(rect));

            _useRect = true;
            _color1 = color1;
            _color2 = color2;
            _rect = rect;
            _linearGradientMode = linearGradientMode;
        }

        // TODO_OLD: 
        //public XLinearGradientBrush(Rectangle rect, XColor color1, XColor color2, double angle);
        //public XLinearGradientBrush(RectangleF rect, XColor color1, XColor color2, double angle);
        //public XLinearGradientBrush(Rectangle rect, XColor color1, XColor color2, double angle, bool isAngleScaleable);
        //public XLinearGradientBrush(RectangleF rect, XColor color1, XColor color2, double angle, bool isAngleScaleable);
        //public XLinearGradientBrush(RectangleF rect, XColor color1, XColor color2, double angle, bool isAngleScaleable);

        //Blend _GetBlend();
        //ColorBlend _GetInterpolationColors();
        //XColor[] _GetLinearColors();
        //RectangleF _GetRectangle();
        //Matrix _GetTransform();
        //WrapMode _GetWrapMode();
        //void _SetBlend(Blend blend);
        //void _SetInterpolationColors(ColorBlend blend);
        //void _SetLinearColors(XColor color1, XColor color2);
        //void _SetTransform(Matrix matrix);
        //void _SetWrapMode(WrapMode wrapMode);

        //public override object Clone();

        /// <summary>
        /// Gets or sets an XMatrix that defines a local geometric transform for this LinearGradientBrush.
        /// </summary>
        public XMatrix Transform
        {
            get => _matrix;
            set => _matrix = value;
        }

        /// <summary>
        /// Translates the brush with the specified offset.
        /// </summary>
        public void TranslateTransform(double dx, double dy)
        {
            _matrix.TranslatePrepend(dx, dy);
        }

        /// <summary>
        /// Translates the brush with the specified offset.
        /// </summary>
        public void TranslateTransform(double dx, double dy, XMatrixOrder order)
        {
            _matrix.Translate(dx, dy, order);
        }

        /// <summary>
        /// Scales the brush with the specified scalars.
        /// </summary>
        public void ScaleTransform(double sx, double sy)
        {
            _matrix.ScalePrepend(sx, sy);
        }

        /// <summary>
        /// Scales the brush with the specified scalars.
        /// </summary>
        public void ScaleTransform(double sx, double sy, XMatrixOrder order)
        {
            _matrix.Scale(sx, sy, order);
        }

        /// <summary>
        /// Rotates the brush with the specified angle.
        /// </summary>
        public void RotateTransform(double angle)
        {
            _matrix.RotatePrepend(angle);
        }

        /// <summary>
        /// Rotates the brush with the specified angle.
        /// </summary>
        public void RotateTransform(double angle, XMatrixOrder order)
        {
            _matrix.Rotate(angle, order);
        }

        /// <summary>
        /// Multiply the brush transformation matrix with the specified matrix.
        /// </summary>
        public void MultiplyTransform(XMatrix matrix)
        {
            _matrix.Prepend(matrix);
        }

        /// <summary>
        /// Multiply the brush transformation matrix with the specified matrix.
        /// </summary>
        public void MultiplyTransform(XMatrix matrix, XMatrixOrder order)
        {
            _matrix.Multiply(matrix, order);
        }

        /// <summary>
        /// Resets the brush transformation matrix with identity matrix.
        /// </summary>
        public void ResetTransform()
        {
            _matrix = new XMatrix();
        }

        //public void SetBlendTriangularShape(double focus);
        //public void SetBlendTriangularShape(double focus, double scale);
        //public void SetSigmaBellShape(double focus);
        //public void SetSigmaBellShape(double focus, double scale);




        //public Blend Blend { get; set; }
        //public bool GammaCorrection { get; set; }
        //public ColorBlend InterpolationColors { get; set; }
        //public XColor[] LinearColors { get; set; }
        //public RectangleF Rectangle { get; }
        //public WrapMode WrapMode { get; set; }
        //private bool interpolationColorsWasSet;

        internal bool _useRect;
        internal XPoint _point1, _point2;
        internal XColor _color1, _color2;
        internal XRect _rect;
        internal XLinearGradientMode _linearGradientMode;

        internal XMatrix _matrix;
    }
}