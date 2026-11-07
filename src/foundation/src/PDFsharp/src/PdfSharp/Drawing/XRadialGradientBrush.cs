// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.ComponentModel;
using PdfSharp.Internal;

// ReSharper disable RedundantNameQualifier because it is required for hybrid build

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a Brush with a radial gradient.
    /// </summary>
    public sealed class XRadialGradientBrush : XGradientBrush
    {


        /// <summary>
        /// Initializes a new instance of the <see cref="XRadialGradientBrush"/> class.
        /// </summary>
        public XRadialGradientBrush(XPoint point1, XPoint point2, double innerRadius, double outerRadius, XColor color1,
            XColor color2)
        {
            _point1 = point1;
            _point2 = point2;
            _innerRadius = innerRadius;
            _outerRadius = outerRadius;
            _color1 = color1;
            _color2 = color2;
        }

        /// <summary>
        /// Gets or sets an XMatrix that defines a local geometric transform for this RadialGradientBrush.
        /// </summary>
        public XMatrix Transform
        {
            get => _matrix;
            set => _matrix = value;
        }

        /// <summary>
        /// Gets or sets the inner radius.
        /// </summary>
        public double InnerRadius
        {
            get => _innerRadius;
            set => _innerRadius = value;
        }

        /// <summary>
        /// Gets or sets the outer radius.
        /// </summary>
        public double OuterRadius
        {
            get => _outerRadius;
            set => _outerRadius = value;
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
            _matrix = new XMatrix(); //XMatrix.Identity;
        }

        //public void SetBlendTriangularShape(double focus);
        //public void SetBlendTriangularShape(double focus, double scale);
        //public void SetSigmaBellShape(double focus);
        //public void SetSigmaBellShape(double focus, double scale);




        internal XPoint _point1, _point2;
        internal XColor _color1, _color2;
        internal double _innerRadius, _outerRadius;
        internal XMatrix _matrix;
    }
}