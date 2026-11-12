// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


// ReSharper disable ConvertToPrimaryConstructor

namespace PdfSharp.Fonts
{
    /// <summary>
    /// Represents a font resolver info created by the platform font resolver if,
    /// and only if, the font is resolved by a platform-specific flavor (GDI+ or WPF).
    /// The point is that PlatformFontResolverInfo contains the platform-specific objects
    /// like the GDI font or the WPF glyph typeface.
    /// </summary>
    class PlatformFontResolverInfo : FontResolverInfo
    {
        public PlatformFontResolverInfo(string faceName, bool mustSimulateBold, bool mustSimulateItalic)
            : base(faceName, mustSimulateBold, mustSimulateItalic)
        { }

    }
}
