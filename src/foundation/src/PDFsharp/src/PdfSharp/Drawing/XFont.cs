// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.ComponentModel;
using PdfSharp.Internal.OpenType;
using PdfSharp.Internal.Threading;
using PdfSharp.Fonts;
using PdfSharp.Fonts.Internal;
using PdfSharp.Internal;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

// v7.0 review

#pragma warning disable CS1591 // TODO_DOC: Missing XML comment for publicly visible type or member

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines an object used to draw text.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
    public sealed class XFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// </summary>
        /// <param name="familyName">Name of the font family.</param>
        /// <param name="emSize">The em size.</param>
        public XFont(string familyName, double emSize)
            : this(familyName, emSize, XFontStyleEx.Regular, new XPdfFontOptions(GlobalFontSettings.DefaultFontEncoding))
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// </summary>
        /// <param name="familyName">Name of the font family.</param>
        /// <param name="emSize">The em size.</param>
        /// <param name="style">The font style.</param>
        public XFont(string familyName, double emSize, XFontStyleEx style)
            : this(familyName, emSize, style, new XPdfFontOptions(GlobalFontSettings.DefaultFontEncoding))
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// </summary>
        /// <param name="familyName">Name of the font family.</param>
        /// <param name="emSize">The em size.</param>
        /// <param name="style">The font style.</param>
        /// <param name="pdfOptions">Additional PDF options.</param>
        public XFont(string familyName, double emSize, XFontStyleEx style, XPdfFontOptions pdfOptions)
        {
            _familyName = familyName;
            _emSize = emSize;
            _style = style;
            _pdfOptions = pdfOptions;
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class with enforced style simulation.
        /// Only for testing PDFsharp.
        /// </summary>
        internal XFont(string familyName, double emSize, XFontStyleEx style, XPdfFontOptions pdfOptions, XStyleSimulations styleSimulations)
        {
            _familyName = familyName;
            _emSize = emSize;
            _style = style;
            _pdfOptions = pdfOptions;
            OverrideStyleSimulations = true;
            StyleSimulations = styleSimulations;
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// Not yet implemented.
        /// </summary>
        /// <param name="familyName">Name of the family.</param>
        /// <param name="emSize">The em size.</param>
        /// <param name="style">The style.</param>
        /// <param name="weight">The weight.</param>
        /// <param name="fontStretch">The font stretch.</param>
        /// <param name="pdfOptions">The PDF options.</param>
        /// <param name="styleSimulations">The style simulations.</param>
        /// <exception cref="System.NotImplementedException">XFont</exception>
        [Obsolete("Not yet implemented.")]
        public XFont(string familyName, double emSize, XFontStyle style, XFontWeight weight, XFontStretch fontStretch,
            XPdfFontOptions? pdfOptions = null, XStyleSimulations? styleSimulations = null)
        {
            throw new NotImplementedException(nameof(XFont));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// Not yet implemented.
        /// </summary>
        /// <param name="typeface">The typeface.</param>
        /// <param name="emSize">The em size.</param>
        /// <param name="pdfOptions">The PDF options.</param>
        /// <param name="styleSimulations">The style simulations.</param>
        /// <exception cref="System.NotImplementedException">XFont</exception>
        [Obsolete("Not yet implemented.")]
        public XFont(XTypeface typeface, double emSize, XPdfFontOptions? pdfOptions = null, XStyleSimulations? styleSimulations = null)
        {
            throw new NotImplementedException(nameof(XFont));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFont"/> class.
        /// Not yet implemented.
        /// </summary>
        /// <param name="glyphTypeface">The typeface.</param>
        /// <param name="emSize">The em size.</param>
        /// <param name="pdfOptions">The PDF options.</param>
        /// <param name="styleSimulations">The style simulations.</param>
        public XFont(XGlyphTypeface glyphTypeface, double emSize, XPdfFontOptions? pdfOptions = null, XStyleSimulations? styleSimulations = null)
        {
            GlyphTypeface = glyphTypeface;
            _familyName = glyphTypeface.FamilyName;
            _emSize = emSize;
            _style = (glyphTypeface.IsBold ? XFontStyleEx.Bold : XFontStyleEx.Regular) |
                     (glyphTypeface.IsItalic ? XFontStyleEx.Italic : XFontStyleEx.Regular);
            _pdfOptions = pdfOptions ?? new XPdfFontOptions(GlobalFontSettings.DefaultFontEncoding);
            OverrideStyleSimulations = styleSimulations != null && styleSimulations != XStyleSimulations.None;
            StyleSimulations = styleSimulations ?? XStyleSimulations.None;
            Initialize();
        }



        //// Methods
        //public Font(Font prototype, FontStyle newStyle);
        //public Font(FontFamily family, float emSize);
        //public Font(string familyName, float emSize);
        //public Font(FontFamily family, float emSize, FontStyle style);
        //public Font(FontFamily family, float emSize, GraphicsUnit unit);
        //public Font(string familyName, float emSize, FontStyle style);
        //public Font(string familyName, float emSize, GraphicsUnit unit);
        //public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit);
        //public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit);
        ////public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet);
        ////public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet);
        ////public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont);
        ////public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont);
        //public object Clone();
        //static FontFamily CreateFontFamilyWithFallback(string familyName);
        //private void Dispose(bool disposing);
        //public override bool Equals(object? obj);
        //protected override void Finalize();
        //public static Font FromHdc(IntPtr hdc);
        //public static Font FromHfont(IntPtr hfont);
        //public static Font FromLogFont(object lf);
        //public static Font FromLogFont(object lf, IntPtr hdc);
        //public override int GetHashCode();

        /// <summary>
        /// Initializes this instance by computing the glyph typeface, font family, font source and TrueType font face.
        /// (PDFsharp currently only deals with TrueType fonts.)
        /// </summary>
        void Initialize()
        {
            FontResolvingOptions fontResolvingOptions = OverrideStyleSimulations
                ? new FontResolvingOptions(_style, StyleSimulations)
                : new FontResolvingOptions(_style);

            if (GlyphTypeface == null!)
            {
                // In principle an XFont is an XGlyphTypeface plus an em-size.
                GlyphTypeface = XGlyphTypeface.GetOrCreateFrom(_familyName, fontResolvingOptions);

                GlyphTypeface.OTFontFace.SetFontEmbedding((OpenTypeFontEmbedding)_pdfOptions.FontEmbedding);
            }
            CreateDescriptorAndInitializeFontMetrics();
        }



        /// <summary>
        /// Code separated from Metric getter to make code easier to debug.
        /// (Setup properties in their getters caused side effects during debugging because Visual Studio calls a getter
        /// too early to show its value in a debugger window.)
        /// </summary>
        void CreateDescriptorAndInitializeFontMetrics()  // TODO_OLD: refactor
        {
            Debug.Assert(_fontMetrics == null, "InitializeFontMetrics() was already called.");

            //var fontDescriptorCache = PsGlobals.Global.Fonts.FontDescriptorCache;
            //OpenTypeDescriptor = (OpenTypeDescriptor)fontDescriptorCache.GetOrCreateDescriptorFor(this); //_familyName, _style, _glyphTypeface.FontFace);
            //Debug.Assert(ReferenceEquals(OpenTypeDescriptor, GlyphTypeface.OTFontFace.OTDescriptor));
            OpenTypeDescriptor = GlyphTypeface.OTFontFace.OTDescriptor;

            _fontMetrics = new XFontMetrics(OpenTypeDescriptor.Key, OpenTypeDescriptor.UnitsPerEm,
                OpenTypeDescriptor.Ascender, OpenTypeDescriptor.Descender, OpenTypeDescriptor.Leading,
                OpenTypeDescriptor.LineSpacing, OpenTypeDescriptor.CapHeight, OpenTypeDescriptor.XHeight,
                OpenTypeDescriptor.StemV, 0, 0, 0,
                OpenTypeDescriptor.UnderlinePosition, OpenTypeDescriptor.UnderlineThickness,
                OpenTypeDescriptor.StrikeoutPosition, OpenTypeDescriptor.StrikeoutSize);

            XFontMetrics fm = Metrics;

            // Already done in CreateDescriptorAndInitializeFontMetrics.
            //if (_descriptor == null)
            //    _descriptor = (OpenTypeDescriptor)FontDescriptorStock.Global.CreateDescriptor(this);  //(Name, (XGdiFontStyle)Font.Style);

            UnitsPerEm = OpenTypeDescriptor.UnitsPerEm;
            CellAscent = OpenTypeDescriptor.Ascender;
            CellDescent = OpenTypeDescriptor.Descender;
            CellSpace = OpenTypeDescriptor.LineSpacing;

            Debug.Assert(fm.UnitsPerEm == OpenTypeDescriptor.UnitsPerEm);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the XFontFamily object associated with this XFont object.
        /// </summary>
        [Browsable(false)]
        public XFontFamily FontFamily => GlyphTypeface.FontFamily;

        [Obsolete("Use Name to get the fonts family name. Name2 was accidentally released.")]
        public string Name2 => GlyphTypeface.FontFamily.Name;

        /// <summary>
        /// Gets the font family name.
        /// </summary>
        public string Name => GlyphTypeface.FamilyName;

        //internal string FaceName => GlyphTypeface.FontName;

        /// <summary>
        /// Gets the em-size of this font measured in the unit of this font object.
        /// </summary>
        public double Size => _emSize;

        readonly double _emSize;

        /// <summary>
        /// Gets style information for this Font object.
        /// </summary>
        [Browsable(false)]
        public XFontStyleEx Style => _style;

        readonly XFontStyleEx _style;

        /// <summary>
        /// Indicates whether this XFont object is bold.
        /// </summary>
        public bool Bold => (_style & XFontStyleEx.Bold) == XFontStyleEx.Bold;

        /// <summary>
        /// Indicates whether this XFont object is italic.
        /// </summary>
        public bool Italic => (_style & XFontStyleEx.Italic) == XFontStyleEx.Italic;

        /// <summary>
        /// Indicates whether this XFont object is stroke out.
        /// </summary>
        public bool Strikeout => (_style & XFontStyleEx.Strikeout) == XFontStyleEx.Strikeout;

        /// <summary>
        /// Indicates whether this XFont object is underlined.
        /// </summary>
        public bool Underline => (_style & XFontStyleEx.Underline) == XFontStyleEx.Underline;

        /// <summary>
        /// Indicates whether this XFont object is a symbol font.
        /// </summary>
        public bool IsSymbolFont => OpenTypeDescriptor.IsSymbolFont;

        /// <summary>
        /// Gets the PDF options of the font.
        /// </summary>
        public XPdfFontOptions PdfOptions => _pdfOptions ??= new();

        XPdfFontOptions _pdfOptions;

        /// <summary>
        /// Indicates whether this XFont is encoded as Unicode.
        /// Gets a value indicating whether text drawn with this font uses Unicode / CID encoding in the PDF document.
        /// </summary>
        //internal bool Unicode => _pdfOptions != null && _pdfOptions.FontEncoding == PdfFontEncoding.Unicode;
        internal bool UnicodeEncoding => _pdfOptions is { FontEncoding: PdfFontEncoding.Unicode };

        /// <summary>
        /// Gets a value indicating whether text drawn with this font uses ANSI encoding in the PDF document.
        /// </summary>
        internal bool AnsiEncoding => _pdfOptions is { FontEncoding: PdfFontEncoding.WinAnsi };

        /// <summary>
        /// Gets a value indicating whether the font encoding is determined from the characters used in the text.
        /// </summary>
        internal bool AutoEncoding => _pdfOptions is { FontEncoding: PdfFontEncoding.Automatic };

        internal FontType FontTypeFromUnicodeFlag =>
            UnicodeEncoding ? FontType.Type0Unicode
            : AnsiEncoding ? FontType.TrueTypeWinAnsi
            : throw new InvalidOperationException("Font type must be Unicode or WinAnsi encoding.");

        /// <summary>
        /// Gets the cell space for the font. The CellSpace is the line spacing, the sum of CellAscent and CellDescent and optionally some extra space.
        /// </summary>
        public int CellSpace
        {
            get => _cellSpace;
            internal set => _cellSpace = value;
        }
        int _cellSpace;

        /// <summary>
        /// Gets the cell ascent, the area above the baseline that is used by the font.
        /// </summary>
        public int CellAscent { get; internal set; }

        /// <summary>
        /// Gets the cell descent, the area below the baseline that is used by the font.
        /// </summary>
        public int CellDescent { get; internal set; }

        /// <summary>
        /// Gets the font metrics.
        /// </summary>
        public XFontMetrics Metrics => _fontMetrics;

        XFontMetrics _fontMetrics = default!;

        /// <summary>
        /// Returns the line spacing, in pixels, of this font. The line spacing is the vertical distance
        /// between the base lines of two consecutive lines of text. Thus, the line spacing includes the
        /// blank space between lines along with the height of the character itself.
        /// </summary>
        public double GetHeight()
        {
            double value = CellSpace * _emSize / UnitsPerEm;
            return value;
        }

        /// <summary>
        /// Returns the line spacing, in the current unit of a specified Graphics object, of this font.
        /// The line spacing is the vertical distance between the base lines of two consecutive lines of
        /// text. Thus, the line spacing includes the blank space between lines along with the height of
        /// </summary>
        [Obsolete("Use GetHeight() without parameter.")]
        public double GetHeight(XGraphics graphics)
        {
            throw new InvalidOperationException("Honestly: Use GetHeight() without parameter!");
        }

        /// <summary>
        /// Gets the line spacing of this font.
        /// </summary>
        [Browsable(false)]
        public int Height => (int)Math.Ceiling(GetHeight());

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        internal XGlyphTypeface GlyphTypeface { get; private set; } = null!;

        internal OpenTypeFontDescriptor OpenTypeDescriptor { get; private set; } = null!;

        string FamilyName => _familyName;

        string _familyName = "";

        internal int UnitsPerEm { get; private set; }

        /// <summary>
        /// Override style simulations by using the value of StyleSimulations.
        /// </summary>
        internal bool OverrideStyleSimulations { get; set; }

        /// <summary>
        /// Used to enforce style simulations by renderer. For development purposes only.
        /// </summary>
        internal XStyleSimulations StyleSimulations { get; set; }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        ///// <summary>
        ///// Cache PdfFontTable.FontSelector to speed up finding the right PdfFont
        ///// if this font is used more than once.
        ///// </summary>
        //internal string? PdfFontSelector { get; set; }

        internal void CheckVersion() => GlyphTypeface.CheckVersion();

        /// <summary>
        /// Gets the DebuggerDisplayAttribute text.
        /// </summary>
        // ReSharper disable UnusedMember.Local
        string DebuggerDisplay
        // ReSharper restore UnusedMember.Local
        {
            get => Invariant($"font=('{Name}' {Size:0.##}{(Bold ? " bold" : "")}{(Italic ? " italic" : "")} {GlyphTypeface.StyleSimulations})");
        }
    }
}
