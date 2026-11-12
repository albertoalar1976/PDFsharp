// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using Microsoft.Extensions.Logging;
using PdfSharp.Logging;

namespace PdfSharp.Fonts
{
    /// <summary>
    /// A font resolver that uses the fonts installed on the computer.
    /// It works the same way on Windows, Linux, and macOS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The font folders are scanned once, when the first typeface is resolved. Only the headers of the font files are read.
    /// TrueType fonts (.ttf) and TrueType collections (.ttc) are supported. OpenType fonts with PostScript (CFF) outlines
    /// cannot be embedded by PDFsharp and are ignored.
    /// </para>
    /// <para>
    /// A requested style that the family does not provide is simulated, e.g. bold for a family that has only a regular face.
    /// If a family is not installed, a metric-compatible family is used if one is installed, e.g. Liberation Sans for Arial.
    /// Text set with such a substitute takes exactly the same space as with the original font.
    /// </para>
    /// <para>
    /// Use it as font resolver, or as fallback font resolver for your own font resolver:
    /// <code>
    /// GlobalFontSettings.FontResolver = new SystemFontResolver();
    /// GlobalFontSettings.FallbackFontResolver = new SystemFontResolver();
    /// </code>
    /// The instance is thread-safe.
    /// </para>
    /// </remarks>
    public sealed class SystemFontResolver : IFontResolver
    {
        /// <summary>
        /// Initializes a new instance that uses the font folders of the operating system, see <see cref="GetDefaultFontFolders"/>.
        /// </summary>
        public SystemFontResolver() : this(GetDefaultFontFolders())
        { }

        /// <summary>
        /// Initializes a new instance that uses the fonts in the specified folders and their subfolders.
        /// If several faces fit a request equally well, the face found first is used, so the order of the folders matters.
        /// </summary>
        /// <param name="fontFolders">The font folders. Folders that do not exist are ignored.</param>
        public SystemFontResolver(IEnumerable<string> fontFolders)
        {
            ArgumentNullException.ThrowIfNull(fontFolders);
            FontFolders = fontFolders.Where(folder => !String.IsNullOrEmpty(folder)).ToArray();
            _index = new(() => FontIndex.Create(FontFolders), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the folders this resolver uses.
        /// </summary>
        public IReadOnlyList<string> FontFolders { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a metric-compatible family is used if a family is not installed.
        /// The default is true.
        /// The groups of metric-compatible families are: Arial, Helvetica, Liberation Sans, Arimo;
        /// Times New Roman, Times, Liberation Serif, Tinos; Courier New, Courier, Liberation Mono, Cousine;
        /// Calibri, Carlito; Cambria, Caladea.
        /// </summary>
        public bool UseMetricCompatibleSubstitutes { get; init; } = true;

        /// <summary>
        /// Gets the names of all font families found, sorted alphabetically.
        /// Useful to find out which fonts are available, e.g. in a container.
        /// </summary>
        public IReadOnlyList<string> GetFamilyNames() => _index.Value.FamilyNames;

        /// <summary>
        /// Gets the font folders of the current operating system.
        /// Windows: the Windows fonts folder and the fonts folder of the current user.
        /// macOS: ~/Library/Fonts, /Library/Fonts, and /System/Library/Fonts.
        /// Linux and other systems: the fonts folders of the XDG Base Directory Specification as used by fontconfig,
        /// i.e. ~/.local/share/fonts, ~/.fonts, /usr/local/share/fonts, and /usr/share/fonts by default.
        /// </summary>
        public static IReadOnlyList<string> GetDefaultFontFolders()
        {
            var folders = new List<string>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var hasHome = !String.IsNullOrEmpty(home);

            if (OperatingSystem.IsWindows())
            {
                folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
                // Fonts installed for the current user only.
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!String.IsNullOrEmpty(localAppData))
                    folders.Add(Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"));
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (hasHome)
                    folders.Add(Path.Combine(home, "Library", "Fonts"));
                folders.Add("/Library/Fonts");
                folders.Add("/System/Library/Fonts");
            }
            else
            {
                var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (!String.IsNullOrEmpty(dataHome))
                    folders.Add(Path.Combine(dataHome, "fonts"));
                else if (hasHome)
                    folders.Add(Path.Combine(home, ".local", "share", "fonts"));
                if (hasHome)
                    folders.Add(Path.Combine(home, ".fonts"));

                var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
                if (String.IsNullOrEmpty(dataDirs))
                    dataDirs = "/usr/local/share:/usr/share";
                foreach (var dataDir in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries))
                    folders.Add(Path.Combine(dataDir, "fonts"));
            }
            return folders.Where(folder => !String.IsNullOrEmpty(folder)).Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <inheritdoc/>
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            ArgumentNullException.ThrowIfNull(familyName);

            var index = _index.Value;
            foreach (var name in GetCandidateFamilies(familyName.Trim()))
            {
                var match = index.Find(name, isBold, isItalic);
                if (match is { } m)
                    return new(m.Face.FaceName, m.SimulateBold, m.SimulateItalic);
            }
            return null;
        }

        /// <inheritdoc/>
        public byte[]? GetFont(string faceName)
        {
            ArgumentNullException.ThrowIfNull(faceName);

            if (!_index.Value.FacesByFaceName.TryGetValue(faceName, out var face))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(face.FilePath);
                return face.CollectionIndex < 0 ? bytes : SystemFontFileReader.ExtractFromCollection(bytes, face.CollectionIndex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                PdfSharpLogHost.FontManagementLogger.LogWarning("Cannot read font file '{Path}': {Message}", face.FilePath, ex.Message);
                return null;
            }
        }

        IEnumerable<string> GetCandidateFamilies(string familyName)
        {
            yield return familyName;

            if (!UseMetricCompatibleSubstitutes)
                yield break;

            foreach (var group in MetricCompatibleFamilies)
            {
                if (!group.Contains(familyName, StringComparer.OrdinalIgnoreCase))
                    continue;
                foreach (var name in group)
                {
                    if (!String.Equals(name, familyName, StringComparison.OrdinalIgnoreCase))
                        yield return name;
                }
            }
        }

        // Families with identical character widths. The first one is the original, the others are substitutes.
        static readonly string[][] MetricCompatibleFamilies =
        [
            ["Arial", "Helvetica", "Liberation Sans", "Arimo"],
            ["Times New Roman", "Times", "Liberation Serif", "Tinos"],
            ["Courier New", "Courier", "Liberation Mono", "Cousine"],
            ["Calibri", "Carlito"],
            ["Cambria", "Caladea"]
        ];

        readonly Lazy<FontIndex> _index;

        /// <summary>
        /// The font faces found in the font folders. Immutable after creation, so it can be used by several threads.
        /// </summary>
        sealed class FontIndex
        {
            // Prefix of the face names, so that they never collide with face names of other font resolvers.
            const string FaceNamePrefix = "SystemFont:";

            readonly Dictionary<string, List<SystemFontFace>> _byFamily = new(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, List<SystemFontFace>> _byTypographicFamily = new(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string, List<SystemFontFace>> _byFullName = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, SystemFontFace> FacesByFaceName { get; } = new(StringComparer.Ordinal);

            public IReadOnlyList<string> FamilyNames { get; private set; } = [];

            public static FontIndex Create(IReadOnlyList<string> folders)
            {
                var index = new FontIndex();
                var files = new HashSet<string>(StringComparer.Ordinal);
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MaxRecursionDepth = 8,  // Protects against cycles of symbolic links.
                    AttributesToSkip = 0    // Font files may be hidden or system files on Windows.
                };

                foreach (var folder in folders)
                {
                    if (!Directory.Exists(folder))
                        continue;

                    try
                    {
                        // Sorted, so that the result does not depend on the order of the file system.
                        var fontFiles = Directory.EnumerateFiles(folder, "*", options).Where(IsFontFile).Order(StringComparer.Ordinal).ToList();
                        foreach (var file in fontFiles)
                        {
                            // Linux distributions often install aliases as symbolic links. Each font is indexed only once,
                            // with its real path.
                            var path = GetRealPath(file);
                            if (files.Add(path))
                                index.AddFile(path);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        PdfSharpLogHost.FontManagementLogger.LogWarning("Cannot enumerate font folder '{Folder}': {Message}", folder, ex.Message);
                    }
                }

                index.FamilyNames = index._byFamily.Keys.Concat(index._byTypographicFamily.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                PdfSharpLogHost.FontManagementLogger.LogInformation("SystemFontResolver found {FaceCount} font faces of {FamilyCount} families in {Folders}.",
                    index.FacesByFaceName.Count, index.FamilyNames.Count, String.Join(", ", folders));
                return index;
            }

            static string GetRealPath(string path)
            {
                try
                {
                    return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);
                }
                catch (IOException)
                {
                    return Path.GetFullPath(path);
                }
            }

            static bool IsFontFile(string path)
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                       || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
                       || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)  // Only a few have TrueType outlines.
                       || extension.Equals(".otc", StringComparison.OrdinalIgnoreCase);
            }

            void AddFile(string path)
            {
                List<SystemFontFace> faces;
                try
                {
                    faces = SystemFontFileReader.ReadFaces(path, FaceNamePrefix);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    PdfSharpLogHost.FontManagementLogger.LogDebug("Cannot read font file '{Path}': {Message}", path, ex.Message);
                    return;
                }

                foreach (var face in faces)
                {
                    FacesByFaceName[face.FaceName] = face;
                    foreach (var name in face.FamilyNames)
                        Add(_byFamily, name, face);
                    foreach (var name in face.TypographicFamilyNames)
                        Add(_byTypographicFamily, name, face);
                    foreach (var name in face.FullNames)
                        Add(_byFullName, name, face);
                }
            }

            static void Add(Dictionary<string, List<SystemFontFace>> dictionary, string name, SystemFontFace face)
            {
                if (!dictionary.TryGetValue(name, out var faces))
                    dictionary[name] = faces = [];
                faces.Add(face);
            }

            /// <summary>
            /// Finds the face that fits best to the requested style, and whether bold or italic must be simulated.
            /// </summary>
            public (SystemFontFace Face, bool SimulateBold, bool SimulateItalic)? Find(string familyName, bool bold, bool italic)
            {
                // 1. A family with up to four members: regular, bold, italic, and bold italic.
                //    This is the font model of PDFsharp and of most applications, e.g. "Arial" or "Noto Sans SemiBold".
                if (_byFamily.TryGetValue(familyName, out var faces))
                {
                    // A face of the requested style is best. Bold and italic can be simulated, but not removed.
                    // Static fonts are preferred to variable fonts, because PDFsharp uses only the default instance.
                    var face = faces.MinBy(f => (2 * StyleCost(f.IsBold, bold) + StyleCost(f.IsItalic, italic),
                        f.IsVariable ? 1 : 0, Math.Abs(f.Weight - (bold ? 700 : 400))))!;
                    return (face, bold && !face.IsBold && face.Weight < 600, italic && !face.IsItalic);
                }

                // 2. A typographic family with faces of several weights, e.g. "Noto Sans" with "Noto Sans SemiBold".
                if (_byTypographicFamily.TryGetValue(familyName, out faces))
                {
                    var target = bold ? 700 : 400;
                    var face = faces.MinBy(f => (StyleCost(f.IsItalic, italic), Math.Abs(f.Weight - target),
                        f.IsVariable ? 1 : 0, bold ? -f.Weight : f.Weight))!;
                    return (face, bold && face.Weight < 600, italic && !face.IsItalic);
                }

                // 3. The full name of a face, e.g. "Arial Bold".
                if (_byFullName.TryGetValue(familyName, out faces))
                {
                    var face = faces[0];
                    return (face, bold && !face.IsBold && face.Weight < 600, italic && !face.IsItalic);
                }

                return null;
            }

            // 0: the face has the requested style, 1: the style can be simulated, 4: the face has a style that was not requested.
            static int StyleCost(bool has, bool requested) => has == requested ? 0 : requested ? 1 : 4;
        }
    }
}
