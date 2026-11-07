// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using MigraDoc.DocumentObjectModel;

namespace MigraDoc.RtfRendering
{
    /// <summary>
    /// Renders a ListInfo in the \listoverridetable control.
    /// </summary>
    class ListInfoOverrideRenderer : RendererBase
    {
        public ListInfoOverrideRenderer(DocumentObject domObj, RtfDocumentRenderer docRenderer)
            : base(domObj, docRenderer)
        {
            _listInfo = (ListInfo)domObj;
        }

        /// <summary>
        /// Renders a ListInfo to RTF for the \listoverridetable.
        /// </summary>
        internal override void Render()
        {
            // The numbering state belongs to the current rendering, not to the process.
            var lists = _docRenderer.Lists;

            int id = lists.GetListID(_listInfo);
            if (id > -1)
            {
                var listNumber = lists.NextListNumber;
                _rtfWriter.StartContent();
                _rtfWriter.WriteControl("listoverride");
                _rtfWriter.WriteControl("listid", id);
                _rtfWriter.WriteControl("listoverridecount", 0);
                _rtfWriter.WriteControl("ls", listNumber);
                _rtfWriter.EndContent();
                lists.ListNumbers.Add(_listInfo, listNumber);
                lists.NextListNumber = listNumber + 1;
            }
        }

        readonly ListInfo _listInfo;
    }
}
