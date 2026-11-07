// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.


namespace PdfSharp.Drawing
{
#if true_ // unused
    /// <summary>
    /// Represents a segment of a path defined by a type and a set of points.
    /// </summary>
    internal sealed class XGraphicsPathItem
    {
        public XGraphicsPathItem(XGraphicsPathItemType type)
        {
            Type = type;
            Points = null;
        }


        public XGraphicsPathItem(XGraphicsPathItemType type, params XPoint[] points)
        {
            Type = type;
            Points = (XPoint[])points.Clone();
        }

        public XGraphicsPathItem Clone()
        {
            XGraphicsPathItem item = (XGraphicsPathItem)MemberwiseClone();
            item.Points = (XPoint[])Points.Clone();
            return item;
        }

        public XGraphicsPathItemType Type;
        public XPoint[] Points;
    }
#endif
}