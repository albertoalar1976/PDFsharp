// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

//#pragma warning disable 0436

using PdfSharp.Internal;

namespace PdfSharp
{
    /// <summary>
    /// Version information for all PDFsharp related assemblies.
    /// </summary>
    // TODO_OLD: These literals are not in sync with the NuGet metadata. Should be fixed.
    public static class PdfSharpProductVersionInformation
    {
        // The fields are reordered to take initialization chronology into account.
        // Today this may be not necessary anymore.

        /// <summary>
        /// The title of the product.
        /// </summary>
        public const string Title = "PDFsharp";

        /// <summary>
        /// A characteristic description of the product.
        /// </summary>
        public const string Description = "A .NET library for processing PDF.";

        /// <summary>
        /// The major version number of the product.
        /// </summary>
        public static readonly string VersionMajor = SemVersionInformation.Major;

        /// <summary>
        /// The minor version number of the product.
        /// </summary>
        public static readonly string VersionMinor = SemVersionInformation.Minor;

        /// <summary>
        /// The patch number of the product.
        /// </summary>
        public static readonly string VersionPatch = SemVersionInformation.Patch;

        /// <summary>
        /// The Version PreRelease string for NuGet.
        /// </summary>
        public static readonly string VersionPreRelease = SemVersionInformation.PreReleaseLabel;
        
        /// <summary>
        /// The PDF creator application information string.
        /// </summary>
        public static readonly string Creator = $"{Title} {SemVersionInformation.InformationalVersion}{Technology}";

        /// <summary>
        /// The PDF producer (created by) information string.
        /// TODO_OLD: Called Creator in MigraDoc???
        /// </summary>
        public static readonly string Producer = $"{Title} {SemVersionInformation.InformationalVersion} ({Url})";

        /// <summary>
        /// The full version number.
        /// </summary>
        public static readonly string Version = SemVersionInformation.Version;

        /// <summary>
        /// The full file version number.
        /// </summary>
        public static readonly string FileVersion = SemVersionInformation.FileVersion;

        /// <summary>
        /// The full semantic version number.
        /// </summary>
        public static readonly string SemanticVersion = SemVersionInformation.InformationalVersion;

        /// <summary>
        /// The home page of this product.
        /// </summary>
        public const string Url = "www.pdfsharp.com";

        /// <summary>
        /// Unused.
        /// </summary>
        public const string Configuration = "";

        /// <summary>
        /// The company that created/owned the product.
        /// </summary>
        public const string Company = "empira Software GmbH, Troisdorf (Cologne Area), Germany";

        /// <summary>
        /// The name of the product.
        /// </summary>
        public const string Product = "PDFsharp";

        /// <summary>
        /// The copyright information.
        /// </summary>
        public const string Copyright = "Copyright © 2005-2026 empira Software GmbH."; // Not used as NuGet Copyright. See Directory.Build.Props.

        /// <summary>
        /// The trademark of the product.
        /// </summary>
        public const string Trademark = "PDFsharp";

        /// <summary>
        /// Unused.
        /// </summary>
        public const string Culture = "";



        /// <summary>
        /// The technology tag of the product:
        /// (none) : Core -- .NET 6 or higher
        /// -gdi : GDI+ -- Windows only
        /// -wpf : WPF -- Windows only
        /// -hybrid : Both GDI+ and WPF (hybrid) -- for self-testing, not used anymore
        /// -sl : Silverlight -- deprecated
        /// -wp : Windows Phone -- deprecated
        /// -wrt : Windows RunTime -- deprecated
        /// -uwp : Universal Windows Platform -- not used anymore
        /// </summary>
        // .NET 6.0 without GDI+ and WPF
        public const string Technology = "";  // no tag
    }
}
