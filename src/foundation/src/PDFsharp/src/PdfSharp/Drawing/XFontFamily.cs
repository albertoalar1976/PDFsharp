// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Internal.OpenType;
using PdfSharp.Fonts;
using PdfSharp.Internal;

namespace PdfSharp.Drawing
{
    /// <summary>
    /// Defines a group of typefaces having a similar basic design and certain variations in styles.
    /// </summary>
    public sealed class XFontFamily
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XFontFamily"/> class.
        /// </summary>
        /// <param name="familyName">The family name of a font.</param>
        public XFontFamily(string familyName)
        {
            FamilyInternal = FontFamilyInternal.GetOrCreateFromName(familyName, true);
        }

        internal XFontFamily(string familyName, bool createPlatformObjects)
        {
            FamilyInternal = FontFamilyInternal.GetOrCreateFromName(familyName, createPlatformObjects);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFontFamily"/> class from FontFamilyInternal.
        /// </summary>
        XFontFamily(FontFamilyInternal fontFamilyInternal)
        {
            FamilyInternal = fontFamilyInternal;
        }

        //public XFontFamily(GdiFontFamily gdiFontFamily)
        //{
        //    FamilyInternal = FontFamilyInternal.GetOrCreateFromGdi(gdiFontFamily);
        //}


////        internal static XFontFamily CreateFromName_not_used(string name, bool createPlatformFamily)
////        {
////            XFontFamily fontFamily = new XFontFamily(name);
////            if (createPlatformFamily)
////            {
////#if GDI
////                //fontFamily._gdiFamily = new System.Drawing.FontFamily(name);
////#endif
////#if WPF
////                //fontFamily._wpfFamily = new System.Windows.Media.FontFamily(name);
////#endif
////            }
////            return fontFamily;
////        }

        /// <summary>
        /// An XGlyphTypeface for a font source that comes from a custom font resolver
        /// creates a solitary font family exclusively for it.
        /// </summary>
        internal static XFontFamily GetOrCreateFontFamily(string name)
        {
            // Custom font resolver face names must not clash with platform family names.
            var fontFamilyCache = PsGlobals.Global.Fonts.FontFamilyCache;
            var fontFamilyInternal = fontFamilyCache.GetFamilyByName(name);
            if (fontFamilyInternal == null)
            {
                fontFamilyInternal = FontFamilyInternal.GetOrCreateFromName(name, false);
                fontFamilyInternal = fontFamilyCache.CacheOrGetFontFamily(fontFamilyInternal);
            }

            // Create font family and save it in cache. Do not try to create platform objects.
            return new(fontFamilyInternal);
        }



        /// <summary>
        /// Gets the name of the font family.
        /// </summary>
        public string Name => FamilyInternal.Name;

#if true__
        public double LineSpacing
        {
            get
            {
                WpfFamily.FamilyTypefaces[0].UnderlineThickness
            }
        }

#endif

        /// <summary>
        /// Returns the cell ascent, in design units, of the XFontFamily object of the specified style.
        /// </summary>
        public int GetCellAscent(XFontStyleEx style)
        {
            var fontDescriptorCache = PsGlobals.Global.Fonts.FontDescriptorCache;
            OpenTypeFontDescriptor descriptor = (OpenTypeFontDescriptor)fontDescriptorCache.GetOrCreateDescriptor(Name, style);

            int result = descriptor.Ascender;
            return result;
        }

        /// <summary>
        /// Returns the cell descent, in design units, of the XFontFamily object of the specified style.
        /// </summary>
        public int GetCellDescent(XFontStyleEx style)
        {
            var fontDescriptorCache = PsGlobals.Global.Fonts.FontDescriptorCache;
            OpenTypeFontDescriptor descriptor = (OpenTypeFontDescriptor)fontDescriptorCache.GetOrCreateDescriptor(Name, style);
            int result = descriptor.Descender;
            return result;
        }

        /// <summary>
        /// Gets the height, in font design units, of the em square for the specified style.
        /// </summary>
        public int GetEmHeight(XFontStyleEx style)
        {
            var fontDescriptorCache = PsGlobals.Global.Fonts.FontDescriptorCache;
            OpenTypeFontDescriptor descriptor = (OpenTypeFontDescriptor)fontDescriptorCache.GetOrCreateDescriptor(Name, style);
            int result = descriptor.UnitsPerEm;
#if DEBUG_
            int headValue = descriptor.FontFace.head.unitsPerEm;
            Debug.Assert(headValue == result);
#endif
            return result;
        }

        /// <summary>
        /// Returns the line spacing, in design units, of the FontFamily object of the specified style.
        /// The line spacing is the vertical distance between the base lines of two consecutive lines of text.
        /// </summary>
        public int GetLineSpacing(XFontStyleEx style)
        {
            var fontDescriptorCache = PsGlobals.Global.Fonts.FontDescriptorCache;
            OpenTypeFontDescriptor descriptor = (OpenTypeFontDescriptor)fontDescriptorCache.GetOrCreateDescriptor(Name, style);
            int result = descriptor.LineSpacing;
            return result;
        }

        //public string GetName(int language);

        /// <summary>
        /// Indicates whether the specified FontStyle enumeration is available.
        /// </summary>
        public bool IsStyleAvailable(XFontStyleEx style)
        {
            XFontStyleEx xStyle = style & XFontStyleEx.BoldItalic;
            throw new InvalidOperationException("In Core build it is the responsibility of the developer to provide all required font faces.");
        }

        /// <summary>
        /// Returns an array that contains all the FontFamily objects associated with the current graphics context.
        /// </summary>
        [Obsolete("Use platform API directly.")]
        public static XFontFamily[] Families => throw new InvalidOperationException("Obsolete and not implemented anymore.");

        /// <summary>
        /// Returns an array that contains all the FontFamily objects available for the specified 
        /// graphics context.
        /// </summary>
        [Obsolete("Use platform API directly.")]
        public static XFontFamily[] GetFamilies(XGraphics graphics)
        {
            throw new InvalidOperationException("Obsolete and not implemented anymore.");
        }



        /// <summary>
        /// The implementation singleton of font family;
        /// </summary>
        internal FontFamilyInternal FamilyInternal;
    }
}
