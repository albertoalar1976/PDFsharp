// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;

namespace PdfSharp.Drawing
{

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

    }
}
