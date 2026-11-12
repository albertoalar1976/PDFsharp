// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace PdfSharp.Internal
{
    /// <summary>
    /// Allows optional error handling without exceptions by assigning to a Nullable&lt;SuppressExceptions&gt; parameter.
    /// </summary>
    class SuppressExceptions  // #RENAME? It is more an ExceptionSuppressor.
    {
        /// <summary>
        /// Returns true, if an error occurred.
        /// </summary>
        public bool ErrorOccurred { get; private set; }

        /// <summary>
        /// If suppressExceptions is set, its ErrorOccurred is set to true, otherwise throwException is invoked.
        /// </summary>
        public static void HandleError(SuppressExceptions? suppressExceptions, Action throwException)
        {
            if (suppressExceptions == null)
                throwException.Invoke();

            suppressExceptions!.ErrorOccurred = true;
        }

        /// <summary>
        /// Returns true, if suppressExceptions is set and its ErrorOccurred is true.
        /// </summary>
        public static bool HasError(SuppressExceptions? suppressExceptions)
        {
            return suppressExceptions?.ErrorOccurred ?? false;
        }
    }

    // Experimental - not yet in use.
}
