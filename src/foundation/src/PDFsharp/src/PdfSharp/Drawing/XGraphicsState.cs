// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


namespace PdfSharp.Drawing
{
    /// <summary>
    /// Represents the internal state of an XGraphics object.
    /// This class is used as a handle for restoring the context.
    /// </summary>
    public sealed class XGraphicsState
    {
        // This class is simply a wrapper of InternalGraphicsState.
        internal XGraphicsState()
        { }
        internal InternalGraphicsState InternalState = default!;
    }
}
