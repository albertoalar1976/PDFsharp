// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;
using System.ComponentModel;
using PdfSharp.Internal;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a Brush with a linear gradient.
    /// </summary>
    public abstract class XGradientBrush : XBrush
    {
        /// <summary>
        /// Gets or sets a value indicating whether to extend the gradient beyond its bounds.
        /// </summary>
        public bool ExtendLeft
        {
            get => _extendLeft;
            set => _extendLeft = value;
        }
        internal bool _extendLeft;

        /// <summary>
        /// Gets or sets a value indicating whether to extend the gradient beyond its bounds.
        /// </summary>
        public bool ExtendRight
        {
            get => _extendRight;
            set => _extendRight = value;
        }
        internal bool _extendRight;
    }
}
