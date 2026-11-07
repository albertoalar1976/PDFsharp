// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;

namespace PdfSharp.Drawing
{
#if true_
    /// <summary>
    /// Not used in this implementation.
    /// </summary>
    [Flags]
    public enum XStringFormatFlags
    {
        //DirectionRightToLeft  = 0x0001,
        //DirectionVertical     = 0x0002,
        //FitBlackBox           = 0x0004,
        //DisplayFormatControl  = 0x0020,
        //NoFontFallback        = 0x0400,
        /// <summary>
        /// The default value.
        /// </summary>
        MeasureTrailingSpaces = 0x0800,
        //NoWrap                = 0x1000,
        //LineLimit             = 0x2000,
        //NoClip                = 0x4000,
    }
#endif

    /// <summary>
    /// Represents the text layout information.
    /// </summary>
    public class XStringFormat
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XStringFormat"/> class.
        /// </summary>
        public XStringFormat()
        { }

        //TODO_OLD public StringFormat(StringFormat format);
        //public StringFormat(StringFormatFlags options);
        //public StringFormat(StringFormatFlags options, int language);
        //public object Clone();
        //public void Dispose();
        //private void Dispose(bool disposing);
        //protected override void Finalize();
        //public float[] GetTabStops(out float firstTabOffset);
        //public void SetDigitSubstitution(int language, StringDigitSubstitute substitute);
        //public void SetMeasurableCharacterRanges(CharacterRange[] ranges);
        //public void SetTabStops(float firstTabOffset, float[] tabStops);
        //public override string ToString();

        /// <summary>
        /// Gets or sets horizontal text alignment information.
        /// </summary>
        public XStringAlignment Alignment
        {
            get => _alignment;
            set
            {
                _alignment = value;
            }
        }
        XStringAlignment _alignment;

        //public int DigitSubstitutionLanguage { get; }
        //public StringDigitSubstitute DigitSubstitutionMethod { get; }
        //public StringFormatFlags FormatFlags { get; set; }
        //public static StringFormat GenericDefault { get; }
        //public static StringFormat GenericTypographic { get; }
        //public HotkeyPrefix HotkeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the line alignment.
        /// </summary>
        public XLineAlignment LineAlignment
        {
            get { return _lineAlignment; }
            set
            {
                _lineAlignment = value;
            }
        }
        XLineAlignment _lineAlignment;

        //public StringTrimming Trimming { get; set; }

        /// <summary>
        /// Gets a new XStringFormat object that aligns the text left on the base line.
        /// </summary>
        [Obsolete("Use XStringFormats.Default. (Note plural in class name.)")]
        public static XStringFormat Default => XStringFormats.Default;

        /// <summary>
        /// Gets a new XStringFormat object that aligns the text top left of the layout rectangle.
        /// </summary>
        [Obsolete("Use XStringFormats.Default. (Note plural in class name.)")]
        public static XStringFormat TopLeft => XStringFormats.TopLeft;

        /// <summary>
        /// Gets a new XStringFormat object that centers the text in the middle of the layout rectangle.
        /// </summary>
        [Obsolete("Use XStringFormats.Center. (Note plural in class name.)")]
        public static XStringFormat Center => XStringFormats.Center;

        /// <summary>
        /// Gets a new XStringFormat object that centers the text at the top of the layout rectangle.
        /// </summary>
        [Obsolete("Use XStringFormats.TopCenter. (Note plural in class name.)")]
        public static XStringFormat TopCenter => XStringFormats.TopCenter;

        /// <summary>
        /// Gets a new XStringFormat object that centers the text at the bottom of the layout rectangle.
        /// </summary>
        [Obsolete("Use XStringFormats.BottomCenter. (Note plural in class name.)")]
        public static XStringFormat BottomCenter => XStringFormats.BottomCenter;

    }
}
