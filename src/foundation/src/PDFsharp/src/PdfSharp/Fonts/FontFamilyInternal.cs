// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Fonts.Internal;
using PdfSharp.Internal.Threading;
using PdfSharp.Internal.OpenType;
using PdfSharp.Internal;


// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertPropertyToExpressionBody

namespace PdfSharp.Fonts
{
    /// <summary>
    /// Internal implementation class of XFontFamily.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
    internal sealed class FontFamilyInternal
    {
        // Implementation Notes
        // FontFamilyInternal implements an XFontFamily.
        //
        // * Each XFontFamily object is just a handle to its FontFamilyInternal singleton.
        //
        // * A FontFamilyInternal is uniquely identified by its name. It
        //    is not possible to use two different fonts that have the same
        //    family name.

        FontFamilyInternal(string familyName, bool createPlatformObjects)
        {
            _sourceName = familyName;
            _otFontFamily = OpenTypeFontFamily.GetOrCreateFrom(familyName);
            _name = _otFontFamily.FamilyName;
        }



        internal static FontFamilyInternal GetOrCreateFromName(string familyName, bool createPlatformObject)
        {
            try
            {
                Locks.EnterFontManagement();
                var fontFamilyCache = PsGlobals.Global.Fonts.FontFamilyCache;
                var family = fontFamilyCache.GetFamilyByName(familyName);
                if (family == null)
                {
                    family = new(familyName, createPlatformObject);
                    family = fontFamilyCache.CacheOrGetFontFamily(family);
                }
                return family;
            }
            finally { Locks.ExitFontManagement(); }
        }



        /// <summary>
        /// Gets the family name this family was originally created with.
        /// </summary>
        public string SourceName => _sourceName;
        readonly string _sourceName;

        /// <summary>
        /// Gets the name that uniquely identifies this font family.
        /// </summary>
        public string Name => _name;
        readonly string _name;

        readonly OpenTypeFontFamily _otFontFamily;


        /// <summary>
        /// Gets the DebuggerDisplayAttribute text.
        /// </summary>
        internal string DebuggerDisplay
            => String.Format(CultureInfo.InvariantCulture, "FontFamily: '{0}'", _otFontFamily.FamilyName);
    }
}
