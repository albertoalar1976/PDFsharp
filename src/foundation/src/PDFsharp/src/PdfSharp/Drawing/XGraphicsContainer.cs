// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


namespace PdfSharp.Drawing
{
    /// <summary>
    /// Represents the internal state of an XGraphics object.
    /// </summary>
    public sealed class XGraphicsContainer
    {
        internal InternalGraphicsState InternalState = default!;
    }
}
