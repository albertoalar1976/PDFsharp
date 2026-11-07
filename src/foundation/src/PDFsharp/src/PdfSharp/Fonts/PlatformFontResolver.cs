// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Drawing;
using PdfSharp.Fonts.Internal;
using PdfSharp.Internal;
using System.Linq;
using System.Runtime.InteropServices;

// Re-Sharper disable RedundantNameQualifier

namespace PdfSharp.Fonts
{
    /// <summary>
    /// Default platform specific font resolving.
    /// </summary>
    public static class PlatformFontResolver
    {
        /// <summary>
        /// Resolves the typeface by generating a font resolver info.
        /// </summary>
        /// <param name="familyName">Name of the font family.</param>
        /// <param name="bold">Indicates whether a bold font is requested.</param>
        /// <param name="italic">Indicates whether an italic font is requested.</param>
        public static FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            FontFactory.CheckInvocationOfPlatformFontResolver();

            var fontResolvingOptions = new FontResolvingOptions(FontHelper.CreateStyle(bold, italic));
            return ResolveTypeface(familyName, fontResolvingOptions, XGlyphTypeface.ComputeGtfKey(familyName, fontResolvingOptions));
        }

        /// <summary>
        /// Internal implementation.
        /// </summary>
        internal static FontResolverInfo? ResolveTypeface(string familyName, FontResolvingOptions fontResolvingOptions, string typefaceKey)
        {
            // Internally we often have the typeface key already.
            if (String.IsNullOrEmpty(typefaceKey))
                typefaceKey = XGlyphTypeface.ComputeGtfKey(familyName, fontResolvingOptions);

            // The user may call ResolveTypeface anytime from anywhere, so check cache in FontFactory in the first place.
            if (FontFactory.TryGetFontResolverInfoByTypefaceKey(typefaceKey, out var fontResolverInfo))
                return fontResolverInfo;

            // Let the platform create the requested font source and save both PlatformResolverInfo
            // and XFontSource in FontFactory cache.
            // It is possible that we already have the correct font source. E.g. we already have the regular typeface in cache
            // and look now for the italic typeface, but no such font exists. In this case we get the regular font source
            // and cache it again with the italic typeface key. Furthermore, in glyph typeface style simulation for italic is set.
            var resolverInfo = TryWindowsPlatformFontResolver(familyName, fontResolvingOptions, typefaceKey);
            return resolverInfo;
        }

        /// <summary>
        /// Creates an XGlyphTypeface.
        /// </summary>
        internal static FontResolverInfo? TryWindowsPlatformFontResolver(string familyName, FontResolvingOptions fontResolvingOptions, string typefaceKey)
        {
            if (String.IsNullOrEmpty(typefaceKey))
                typefaceKey = XGlyphTypeface.ComputeGtfKey(familyName, fontResolvingOptions);
            var style = fontResolvingOptions.FontStyle;

            var fontResolverInfosByName = PsGlobals.Global.Fonts.FontResolverInfosByName;
            var fontSourcesByName = PsGlobals.Global.Fonts.FontSourcesByName;

            // Was this typeface requested before?
            if (fontResolverInfosByName.TryGetValue(typefaceKey, out var fontResolverInfo))
            {
                return null;
            }

            if (_tryWindowsPlatformFontResolver is null)
            {
                if (Capabilities.OperatingSystem.IsWindows)
                {
                    _tryWindowsPlatformFontResolver = GlobalFontSettings.UseWindowsFontsUnderWindows;
                }
                else if (Capabilities.OperatingSystem.IsLinux)
                {
                    if (Capabilities.OperatingSystem.IsWsl2)
                        _tryWindowsPlatformFontResolver = GlobalFontSettings.UseWindowsFontsUnderWsl2;
                    else
                        _tryWindowsPlatformFontResolver = false;
                }
                else
                    _tryWindowsPlatformFontResolver = false;
            }

            if (_tryWindowsPlatformFontResolver is false)
            {
                // No font face found.
                return null;
            }

            var resolverInfo = WindowsPlatformFontResolver.ResolveTypeface(familyName, fontResolvingOptions.IsBold, fontResolvingOptions.IsItalic);
            if (resolverInfo == null)
            {
                // No font face found.
                return null;
            }

            var type = resolverInfo.GetType();
            Debug.Assert(resolverInfo is PlatformFontResolverInfo);

            FontFactory.RegisterResolverResult(WindowsPlatformFontResolver, familyName, fontResolvingOptions, resolverInfo, typefaceKey);

            return resolverInfo;
        }
        static readonly IFontResolver WindowsPlatformFontResolver = new WindowsPlatformFontResolver();
        static bool? _tryWindowsPlatformFontResolver;

        internal static void Reset()
        {
            _tryWindowsPlatformFontResolver = null;
        }



    }
}
