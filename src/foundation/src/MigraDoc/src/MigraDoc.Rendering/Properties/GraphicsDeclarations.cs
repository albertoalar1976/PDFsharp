// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

#pragma warning disable CS1591

global using float_ = double;
global using FLOAT_ = double;
using System.Runtime.CompilerServices;

using System.Runtime.InteropServices;
using PdfSharp.Drawing;

public static class IdentityExtensions
{
    extension(XRect rect)
    {
        public PdfSharp.Drawing.XRect AsXRect => rect;
    }

    extension(XPoint point)
    {
        public PdfSharp.Drawing.XPoint AsXPoint => point;
    }
}
