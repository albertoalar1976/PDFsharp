// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Fonts;
using PdfSharp.Internal;
using PdfSharp.Internal.OpenType;


#pragma warning disable CS1591 // TODO_DOC: Missing XML comment for publicly visible type or member

namespace PdfSharp.Drawing
{
    /// <summary>
    /// The bytes of a font file.
    /// </summary>
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
    public class XFontSource
    {
        // Based on OpenTypeFontSource.

        // Implementation moved to OpenTypeFontSource.
        // XFontSource is just a thin wrapper of this class.

        // Signature of a true type collection font.
        const uint ttcf = 0x66637474;


        XFontSource(byte[] bytes, ulong checksumKey = 0)
        {
            _otFontSource = OpenTypeFontSource.GetOrCreateFrom(bytes, checksumKey);
        }

        XFontSource(OpenTypeFontSource otFontSource)
        {
            _otFontSource = otFontSource;
        }

        /// <summary>
        /// Gets an existing font source or creates a new one.
        /// A new font source is cached in font factory.
        /// </summary>
        public static XFontSource GetOrCreateFrom(byte[] bytes)
        {
            ulong checksumKey = ChecksumHelper.CalcChecksum(bytes);
            var fontSourceCache = PsGlobals.Global.Fonts.FontSourceCache;
            if (fontSourceCache.TryGetFontSourceByKey(checksumKey, out var fontSource))
                return fontSource;

            fontSource = new(bytes, checksumKey);
            fontSource = fontSourceCache.CacheFontSource(fontSource);
            return fontSource;
        }

        public static XFontSource GetOrCreateFrom(OpenTypeFontSource otFontSource)
        {
            var fontSourceCache = PsGlobals.Global.Fonts.FontSourceCache;
            if (fontSourceCache.TryGetFontSourceByKey(otFontSource.ChecksumKey, out var fontSource))
                return fontSource;

            fontSource = new(otFontSource);
            fontSource = fontSourceCache.CacheFontSource(fontSource);
            return fontSource;
        }

        /// <summary>
        /// Creates an XFontSource from a font file.
        /// </summary>
        /// <param name="path">The path of the font file.</param>
        public static XFontSource CreateFromFile(string path)
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var bytes = File.ReadAllBytes(path);
            return GetOrCreateFrom(bytes);
        }

        /// <summary>
        /// Creates an XFontSource from a uri to a font file.
        /// </summary>
        public static XFontSource CreateFromFile(Uri fontSourceUri)
        {
            if (!fontSourceUri.IsFile)
                throw new ArgumentException(GfxMsgs.Font_NoFileUri(fontSourceUri).Message);

            return CreateFromFile(fontSourceUri.LocalPath);
        }



        static XFontSource GetOrCreateFrom(string typefaceKey, byte[] fontBytes)
        {
            ulong checksumKey = ChecksumHelper.CalcChecksum(fontBytes);
            var fontSourceCache = PsGlobals.Global.Fonts.FontSourceCache;
            if (fontSourceCache.TryGetFontSourceByKey(checksumKey, out var fontSource))
            {
                // The font source already exists, but is not yet cached under the specified typeface key.
                FontFactory.CacheExistingFontSourceWithNewTypefaceKey(typefaceKey, fontSource);
            }
            else
            {
                // No font source exists. Create new one and cache it.
                fontSource = new(fontBytes, checksumKey);
                fontSourceCache.CacheNewFontSource(typefaceKey, fontSource);
            }
            return fontSource;
        }

        /// <summary>
        /// Creates a font source from a byte array.
        /// </summary>
        public static XFontSource CreateCompiledFont(byte[] bytes)
        {
            var fontSource = new XFontSource(bytes, 0);
            return fontSource;
        }

        /// <summary>
        /// Gets or sets the font face.
        /// </summary>
        internal OpenTypeFontFace OTFontFace
        {
            get => _otFontSource.OTFontFace;
            set => _otFontSource.OTFontFace = value;
        }

        /// <summary>
        /// Gets the key that uniquely identifies this font source.
        /// </summary>
        internal ulong ChecksumKey
        {
            get => _otFontSource.ChecksumKey;
        }

        /// <summary>
        /// //??? What??? Gets the name of the font’s name table.
        /// </summary>
        public string FontFaceKey => _otFontSource.OTFontFace.OTDescriptor.Key ?? null!;

        //string _fontFaceKey = null!;

        /// <summary>
        /// Gets the bytes of the font.
        /// </summary>
        public byte[] Bytes => _otFontSource.Bytes;

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        public override int GetHashCode()
            => _otFontSource.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>
        ///   <see langword="true" /> if the specified object  is equal to the current object; otherwise, <see langword="false" />.
        /// </returns>
        public override bool Equals(object? obj)
        {
            if (obj is not XFontSource fontSource)
                return false;
            return ChecksumKey == fontSource.ChecksumKey;
        }

        public OpenTypeFontSource OTFontSource => _otFontSource;

        readonly OpenTypeFontSource _otFontSource;

        /// <summary>
        /// Gets the DebuggerDisplayAttribute text.
        /// </summary>
        // ReSharper disable UnusedMember.Local
        internal string DebuggerDisplay =>
            String.Format(CultureInfo.InvariantCulture, "XFontSource: '{0}', keyhash={1}", FontFaceKey, ChecksumKey % 99991 /* largest prime number less than 100000 */);
    }
}
