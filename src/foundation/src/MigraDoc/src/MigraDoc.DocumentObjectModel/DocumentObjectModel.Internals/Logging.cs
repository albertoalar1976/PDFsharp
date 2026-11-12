// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using Microsoft.Extensions.Logging;

#pragma warning disable 1591 // Because this is preview code.

namespace MigraDoc.DocumentObjectModel.Internals
{
    public static class AppLogEventIds
    {
        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once IdentifierTypo
        public const int MDDOM = 11001;

    }
    public static class AppLogEvents
    {
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once IdentifierTypo
        public static EventId MDDOM = new(AppLogEventIds.MDDOM, "NDDOM");

        // TODO_OLD Not yet used.

        public static EventId FontCreated = new(AppLogEventIds.MDDOM + 0, "Font created");
        public static EventId FontFound = new(AppLogEventIds.MDDOM + 1, "Font found");
        public static EventId FontNotFound = new(AppLogEventIds.MDDOM + 2, "Font not found");

    }
}
