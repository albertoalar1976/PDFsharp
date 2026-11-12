// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


namespace PdfSharp.Drawing
{
    /// <summary>
    /// Provides access to the internal data structures of XGraphicsPath.
    /// This class prevents the public interface from pollution with internal functions.
    /// </summary>
    public sealed class XGraphicsPathInternals
    {
        internal XGraphicsPathInternals(XGraphicsPath path)
        {
            _path = path;
        }

        XGraphicsPath _path;


    }
}