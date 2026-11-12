// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Internal;
using PdfSharp.Internal.Threading;

namespace PdfSharp.Drawing
{
    // TODO_OLD Free GDI objects (pens, brushes, ...) automatically without IDisposable.
    /// <summary>
    /// Defines an object used to draw lines and curves.
    /// </summary>
    public sealed class XPen
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XPen"/> class.
        /// </summary>
        public XPen(XColor color)
            : this(color, 1, false)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XPen"/> class.
        /// </summary>
        public XPen(XColor color, double width)
            : this(color, width, false)
        { }

        internal XPen(XColor color, double width, bool immutable)
        {
            _color = color;
            _width = width;
            _lineJoin = XLineJoin.Miter;
            _lineCap = XLineCap.Flat;
            _dashStyle = XDashStyle.Solid;
            _dashOffset = 0f;
            _immutable = immutable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XPen"/> class.
        /// </summary>
        public XPen(XPen pen)
        {
            _color = pen._color;
            _width = pen._width;
            _lineJoin = pen._lineJoin;
            _lineCap = pen._lineCap;
            _dashStyle = pen._dashStyle;
            _dashOffset = pen._dashOffset;
            _dashPattern = pen._dashPattern;
            if (_dashPattern != null)
                _dashPattern = (double[])_dashPattern.Clone();
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        public XPen Clone()
            => new XPen(this);

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        public XColor Color
        {
            get => _color;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                _dirty = _dirty || _color != value;
                _color = value;
            }
        }
        XColor _color;

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public double Width
        {
            get => _width;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                _dirty = _dirty || _width != value;
                _width = value;
            }
        }
        double _width;

        /// <summary>
        /// Gets or sets the line join.
        /// </summary>
        public XLineJoin LineJoin
        {
            get => _lineJoin;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                _dirty = _dirty || _lineJoin != value;
                _lineJoin = value;
            }
        }
        XLineJoin _lineJoin;

        /// <summary>
        /// Gets or sets the line cap.
        /// </summary>
        public XLineCap LineCap
        {
            get => _lineCap;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                _dirty = _dirty || _lineCap != value;
                _lineCap = value;
            }
        }
        XLineCap _lineCap;

        /// <summary>
        /// Gets or sets the miter limit.
        /// </summary>
        public double MiterLimit
        {
            get => _miterLimit;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                _dirty = _dirty || _miterLimit != value;
                _miterLimit = value;
            }
        }
        double _miterLimit;

        /// <summary>
        /// Gets or sets the dash style.
        /// </summary>
        public XDashStyle DashStyle
        {
            get => _dashStyle;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                _dirty = _dirty || _dashStyle != value;
                _dashStyle = value;
            }
        }
        XDashStyle _dashStyle;

        /// <summary>
        /// Gets or sets the dash offset.
        /// </summary>
        public double DashOffset
        {
            get => _dashOffset;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                _dirty = _dirty || _dashOffset != value;
                _dashOffset = value;
            }
        }
        double _dashOffset;

        /// <summary>
        /// Gets or sets the dash pattern.
        /// </summary>
        public double[] DashPattern
        {
            get
            {
                if (_dashPattern == null)
                    _dashPattern = [];
                return _dashPattern;
            }
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));

                int length = value.Length;
                //if (length == 0)
                //  throw new ArgumentException("Dash pattern array must not be empty.");

                for (int idx = 0; idx < length; idx++)
                {
                    if (value[idx] <= 0)
                        throw new ArgumentException("Dash pattern value must greater than zero.");
                }

                _dirty = true;
                _dashStyle = XDashStyle.Custom;
                _dashPattern = (double[])value.Clone();
            }
        }
        internal double[]? _dashPattern;

        /// <summary>
        /// Gets or sets a value indicating whether the pen enables overprint when used in a PDF document.
        /// Experimental, takes effect only on CMYK color mode.
        /// </summary>
        public bool Overprint
        {
            get => _overprint;
            set
            {
                if (_immutable)
                    throw new ArgumentException(PsMsgs.CannotChangeImmutableObject(nameof(XPen)));
                _overprint = value;
            }
        }
        bool _overprint;



        bool _dirty = true;
        readonly bool _immutable;
    }
}
