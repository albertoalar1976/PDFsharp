// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;
using System.Diagnostics;
using PdfSharp.Internal;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a single-color object used to fill shapes and draw text.
    /// </summary>
    public sealed class XSolidBrush : XBrush
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XSolidBrush"/> class.
        /// </summary>
        public XSolidBrush()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XSolidBrush"/> class.
        /// </summary>
        public XSolidBrush(XColor color)
            : this(color, false)
        { }

        internal XSolidBrush(XColor color, bool immutable)
        {
            _color = color;
            _immutable = immutable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XSolidBrush"/> class.
        /// </summary>
        public XSolidBrush(XSolidBrush brush)
        {
            _color = brush.Color;
        }

        /// <summary>
        /// Gets or sets the color of this brush.
        /// </summary>
        public XColor Color
        {
            get { return _color; }
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XSolidBrush)));
                _color = value;
            }
        }
        XColor _color;

        /// <summary>
        /// Gets or sets a value indicating whether the brush enables overprint when used in a PDF document.
        /// Experimental, takes effect only on CMYK color mode.
        /// </summary>
        public bool Overprint
        {
            get => _overprint;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XSolidBrush)));
                _overprint = value;
            }
        }
        bool _overprint;




        readonly bool _immutable;
    }
}
