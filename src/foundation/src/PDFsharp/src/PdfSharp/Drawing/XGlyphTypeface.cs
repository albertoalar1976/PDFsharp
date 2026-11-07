// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using Microsoft.Extensions.Logging;
using PdfSharp.Logging;
using PdfSharp.Internal;
using PdfSharp.Internal.OpenType;
using PdfSharp.Internal.Threading;
using PdfSharp.Fonts.Internal;
using PdfSharp.Fonts;

namespace PdfSharp.Drawing
{
    // TODO-7.0: Use OpenTypeGlyphTypeface as implementation of XGlyphTypeface.

    /// <summary>
    /// Specifies a physical font face that corresponds to a font file on the disk or in memory.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
    public sealed class XGlyphTypeface
    {
        const string KeySuffix = ":TFK";  // Typeface Key
        XGlyphTypeface(string key, XFontFamily fontFamily, XFontSource fontSource,
            XStyleSimulations styleSimulations)
        {
            Key = key;
            FontFamily = fontFamily;
            FontSource = fontSource;

            Debug.Assert(fontSource.OTFontFace != null);
            //OTFontFace = OpenTypeFontFace.GetOrCreateFrom(fontSource.OTFontSource);
            OTFontFace = fontSource.OTFontFace;
            
            StyleSimulations = styleSimulations;
            Initialize();
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="XGlyphTypeface"/> class by a font source.
        /// </summary>
        public XGlyphTypeface(XFontSource fontSource)
        {
            string familyName = fontSource.OTFontFace.name.OTFamilyName;
            FontFamily = new XFontFamily(familyName, false);
            OTFontFace = fontSource.OTFontFace;
            IsBold = OTFontFace.os2.IsBold;
            IsItalic = OTFontFace.os2.IsItalic;
            Key = ComputeGtfKey(familyName, IsBold, IsItalic);
            FontSource = fontSource;
            Initialize();
        }



        internal static XGlyphTypeface GetOrCreateFrom(string familyName, FontResolvingOptions fontResolvingOptions)
        {
            // Check cache for requested type face.
            string typefaceKey = ComputeGtfKey(familyName, fontResolvingOptions);
            XGlyphTypeface? glyphTypeface;

            // Fast path without the global font lock for typefaces that already exist.
            if (PsGlobals.Global.Fonts.GlyphTypefaceCache.TryGetGlyphTypeface(typefaceKey, out glyphTypeface))
                return glyphTypeface;

            try
            {
                // Lock around TryGetGlyphTypeface and AddGlyphTypeface.
                Locks.EnterFontManagement();
                var glyphTypefaceCache = PsGlobals.Global.Fonts.GlyphTypefaceCache;
                if (glyphTypefaceCache.TryGetGlyphTypeface(typefaceKey, out glyphTypeface))
                {
                    // Just return existing one.
                    return glyphTypeface;
                }

                //// Resolve typeface by FontFactory. If no success, try fallback font resolver.
                FontResolverInfo? fontResolverInfo = null;
                try  // Custom font resolvers may throw an exception.
                {
                    // Try custom font resolver.
                    fontResolverInfo = FontFactory.ResolveTypeface(familyName, fontResolvingOptions, typefaceKey, false);
                }
                catch (Exception ex)
                {
                    LogErrorBecauseFontResolverThrowsException(familyName, ex);
                }

                if (fontResolverInfo == null)
                {
                    try  // Custom fallback font resolvers may throw an exception.
                    {
                        // Try fallback font resolver.
                        fontResolverInfo = FontFactory.ResolveTypeface(familyName, fontResolvingOptions, typefaceKey, true);
                    }
                    catch (Exception ex)
                    {
                        LogErrorBecauseFontResolverThrowsException(familyName, ex);
                    }
                }

                void LogErrorBecauseFontResolverThrowsException(string name, Exception ex)
                    => PdfSharpLogHost.Logger.LogError("A font resolver cannot resolve font family '{familyName}' and throws an exception, " +
                                                       "but it must return null if the font cannot be resolved. Exception text: " + ex.Message, name);

                if (fontResolverInfo == null)
                {
                    // No fallback - just stop.
                    if (GlobalFontSettings.FontResolver is null)
                    {
                        throw new InvalidOperationException(
                            $"No appropriate font found for family name '{familyName}'. " +
                                   "Implement IFontResolver and assign to 'GlobalFontSettings.FontResolver' to use fonts. " +
                                   $"See {UrlLiterals.LinkToFontResolving}");
                    }
                    throw new InvalidOperationException($"No appropriate font found for family name '{familyName}'.");
                }
                // Now create the font family at the first.
                XFontFamily? fontFamily;
                if (fontResolverInfo is PlatformFontResolverInfo platformFontResolverInfo)
                {
                    // Case: fontResolverInfo was created by platform font resolver
                    // and contains platform specific objects that are reused.
                    // Get or create font family for custom font resolver retrieved font source.
                    fontFamily = XFontFamily.GetOrCreateFontFamily(familyName);
                }
                else
                {
                    // Case: fontResolverInfo was created by custom font resolver.

                    // Get or create font family for custom font resolver retrieved font source.
                    fontFamily = XFontFamily.GetOrCreateFontFamily(familyName);
                }

                // We have a valid font resolver info. That means we also have an XFontSource object loaded in the cache.
                var fontSourceCache = PsGlobals.Global.Fonts.FontSourceCache;
                XFontSource fontSource = fontSourceCache.GetFontSourceByFontName(fontResolverInfo.FaceName);
                Debug.Assert(fontSource != null);

                // Each font source already contains its OpenTypeFontFace.
                glyphTypeface = new XGlyphTypeface(typefaceKey, fontFamily, fontSource, fontResolverInfo.StyleSimulations);
                glyphTypefaceCache.AddGlyphTypeface(glyphTypeface);
            }
            finally { Locks.ExitFontManagement(); }
            return glyphTypeface;
        }



        /// <summary>
        /// Gets the font family of this glyph typeface.
        /// </summary>
        public XFontFamily FontFamily { get; }

        internal OpenTypeFontFace OTFontFace { get; }

        /// <summary>
        /// Gets the font source of this glyph typeface.
        /// </summary>
        public XFontSource FontSource { get; }

        void Initialize()
        {
            FamilyName = OTFontFace.name.OTFamilyName;
            FaceName = OTFontFace.name.OTSubfamilyName;
            FontName = DisplayName = OTFontFace.name.OTFullFontName;
            if (String.IsNullOrEmpty(DisplayName))
            {
                FontName = DisplayName = FamilyName;
                if (!String.IsNullOrEmpty(FaceName))
                    DisplayName += " (" + FaceName + ")";
            }

            // Bold, as defined in OS/2 table.
            IsBold = OTFontFace.os2.IsBold;

            // Italic, as defined in OS/2 table.
            IsItalic = OTFontFace.os2.IsItalic;
        }

        /// <summary>
        /// Gets the name of the font face. This can be a file name, a URI, or a GUID.
        /// </summary>
        internal string FontName{ get; private set; } = null!;  // Former FaceName

        /// <summary>
        /// Gets the English family name of the font, for example "Arial".
        /// </summary>
        public string FamilyName { get; private set; } = null!;

        /// <summary>
        /// Gets the English subfamily name of the font,
        /// for example "Bold".
        /// </summary>
        /// <remarks>
        /// Prior to PDFsharp 7 this function incorrectly returns the family.
        /// Starting with PDFsharp 7 it returns the correct value.
        /// This is a breaking change.
        /// </remarks>
        public string FaceName { get; private set; } = null!;

        /// <summary>
        /// Gets the English display name of the font,
        /// for example "Arial italic".
        /// </summary>
        public string DisplayName { get; private set; } = null!;

        /// <summary>
        /// Gets a value indicating whether the font weight is bold.
        /// </summary>
        public bool IsBold { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the font style is italic.
        /// </summary>
        public bool IsItalic { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the style bold, italic, or both styles must be simulated.
        /// </summary>
        public XStyleSimulations StyleSimulations { get; }

        /// <summary>
        /// Gets the suffix of the face name in a PDF font and font descriptor.
        /// The name based on the effective value of bold and italic from the OS/2 table.
        /// </summary>
        string GetFaceNameSuffix()
        {
            // Use naming of Microsoft Word.
            if (IsBold)
                return IsItalic ? ",BoldItalic" : ",Bold";
            return IsItalic ? ",Italic" : "";
        }

        internal string GetBaseName()  // #NFM 
        {
            string name = DisplayName;
            int ich = name.IndexOf("bold", StringComparison.OrdinalIgnoreCase);
            if (ich > 0)
                name = name[..ich] + name.Substring(ich + 4, name.Length - ich - 4);
            ich = name.IndexOf("italic", StringComparison.OrdinalIgnoreCase);
            if (ich > 0)
                name = name[..ich] + name.Substring(ich + 6, name.Length - ich - 6);
            //name = name.Replace(" ", "");
            name = name.Trim();
            name += GetFaceNameSuffix();
            return name;
        }

        /// <summary>
        /// Computes the human-readable key for a glyph typeface.
        /// {family-name}/{(N)ormal | (O)blique | (I)talic}/{weight}/{stretch}|{(B)old|not (b)old}/{(I)talic|not (i)talic}:tk
        /// e.g.: 'arial/N/400/500|B/i:tk'
        /// </summary>
        internal static string ComputeGtfKey(string familyName, FontResolvingOptions fontResolvingOptions)
        {
            // Compute a human-readable key.
            string simulationSuffix = "";
            if (fontResolvingOptions.OverrideStyleSimulations)
            {
                simulationSuffix = fontResolvingOptions.StyleSimulations switch
                {
                    XStyleSimulations.BoldSimulation => "|B/i",
                    XStyleSimulations.ItalicSimulation => "|b/I",
                    XStyleSimulations.BoldItalicSimulation => "|B/I",
                    XStyleSimulations.None => "|b/i",
                    _ => throw new ArgumentOutOfRangeException(nameof(fontResolvingOptions))
                };
            }

            familyName = familyName.ToLowerInvariant();
            var name = familyName.ToLowerInvariant();
            var bold = fontResolvingOptions.IsBold;
            var italic = fontResolvingOptions.IsItalic;
            var key = bold switch
            {
                false when !italic => name + "/N/400/5" + simulationSuffix + KeySuffix,
                true when !italic => name + "/N/700/5" + simulationSuffix + KeySuffix,
                false when italic => name + "/I/400/5" + simulationSuffix + KeySuffix,
                _ => name + "/I/700/500" + simulationSuffix + KeySuffix,
            };
            return key;
        }

        /// <summary>
        /// Computes the bijective key for a typeface.
        /// </summary>
        internal static string ComputeGtfKey(string familyName, bool isBold, bool isItalic)
        {
            return ComputeGtfKey(familyName, new FontResolvingOptions(FontHelper.CreateStyle(isBold, isItalic)));
        }

        /// <summary>
        /// Gets a string that uniquely identifies an instance of XGlyphTypeface.
        /// </summary>
        internal string Key { get; }


        internal void CheckVersion() => PsGlobals.Global.Fonts.CheckVersion(_globalFontStorageVersion);
        readonly int _globalFontStorageVersion = PsGlobals.Global.Fonts.Version;

        /// <summary>
        /// Gets the DebuggerDisplayAttribute text.
        /// </summary>
        // ReSharper disable UnusedMember.Local
        internal string DebuggerDisplay
            // ReSharper restore UnusedMember.Local
            => Invariant($"{FamilyName} - {FaceName} ({FontName})");
    }
}

/*
   Properties of WPF

   AdvanceHeights	
   Gets the advance heights for the glyphs represented by the GlyphTypeface object.
   
   AdvanceWidths	
   Gets the advance widths for the glyphs represented by the GlyphTypeface object.
   
   Baseline	
   Gets the baseline value for the GlyphTypeface.
   
   BottomSideBearings	
   Gets the distance from bottom edge of the black box to the bottom end of the advance vector for the glyphs represented by the GlyphTypeface object.
   
   CapsHeight	
   Gets the distance from the baseline to the top of an English capital, relative to em size, for the GlyphTypeface object.
   
   CharacterToGlyphMap	
   Gets the nominal mapping of a Unicode code point to a glyph index as defined by the font 'CMAP' table.
   
   Copyrights	
   Gets the copyright information for the GlyphTypeface object.
   
   Descriptions	
   Gets the description information for the GlyphTypeface object.
   
   DesignerNames	
   Gets the designer information for the GlyphTypeface object.
   
   DesignerUrls	
   Gets the designer URL information for the GlyphTypeface object.
   
   DistancesFromHorizontalBaselineToBlackBoxBottom	
   Gets the offset value from the horizontal Western baseline to the bottom of the glyph black box for the glyphs represented by the GlyphTypeface object.
   
   EmbeddingRights	
   Gets the font embedding permission for the GlyphTypeface object.
   
   FaceNames	
   Gets the face name for the GlyphTypeface object.
   
   FamilyNames	
   Gets the family name for the GlyphTypeface object.
   
   FontUri	
   Gets or sets the URI for the GlyphTypeface object.
   
   GlyphCount	
   Gets the number of glyphs for the GlyphTypeface object.
   
   Height	
   Gets the height of the character cell relative to the em size.
   
   LeftSideBearings	
   Gets the distance from the leading end of the advance vector to the left edge of the black box for the glyphs represented by the GlyphTypeface object.
   
   LicenseDescriptions	
   Gets the font license description information for the GlyphTypeface object.
   
   ManufacturerNames	
   Gets the font manufacturer information for the GlyphTypeface object.
   
   RightSideBearings	
   Gets the distance from the right edge of the black box to the right end of the advance vector for the glyphs represented by the GlyphTypeface object.
   
   SampleTexts	
   Gets the sample text information for the GlyphTypeface object.
   
   Stretch	
   Gets the FontStretch value for the GlyphTypeface object.
   
   StrikethroughPosition	
   Gets a value that indicates the distance from the baseline to the strikethrough for the typeface.
   
   StrikethroughThickness	
   Gets a value that indicates the thickness of the strikethrough relative to the font em size.
   
   Style	
   Gets the style for the GlyphTypeface object.
   
   StyleSimulations	
   Gets or sets the StyleSimulations for the GlyphTypeface object.
   
   Symbol	
   Gets a value that indicates whether the GlyphTypeface font conforms to Unicode encoding.
   
   TopSideBearings	
   Gets the distance from the top end of the vertical advance vector to the top edge of the black box for the glyphs represented by the GlyphTypeface object.
   
   Trademarks	
   Gets the trademark notice information for the GlyphTypeface object.
   
   UnderlinePosition	
   Gets the position of the underline in the GlyphTypeface.
   
   UnderlineThickness	
   Gets the thickness of the underline relative to em size.
   
   VendorUrls	
   Gets the vendor URL information for the GlyphTypeface object.
   
   Version	
   Gets the font face version interpreted from the font’s 'NAME' table.
   
   VersionStrings	
   Gets the version string information for the GlyphTypeface object interpreted from the font’s 'NAME' table.
   
   Weight	
   Gets the designed weight of the font represented by the GlyphTypeface object.
   
   Win32FaceNames	
   Gets the Win32 face name for the font represented by the GlyphTypeface object.
   
   Win32FamilyNames	
   Gets the Win32 family name for the font represented by the GlyphTypeface object.
   
   XHeight	
   Gets the Western x-height relative to em size for the font represented by the GlyphTypeface object.
*/
