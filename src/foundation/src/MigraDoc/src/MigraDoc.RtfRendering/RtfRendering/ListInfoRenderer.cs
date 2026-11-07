// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using MigraDoc.DocumentObjectModel;
using System.Collections.Generic;
using System.Diagnostics;

namespace MigraDoc.RtfRendering
{
    /// <summary>
    /// ListInfoRenderer.
    /// </summary>
    class ListInfoRenderer : RendererBase
    {
        public ListInfoRenderer(DocumentObject domObj, RtfDocumentRenderer docRenderer)
            : base(domObj, docRenderer)
        {
            _listInfo = (ListInfo)domObj;
        }

        /// <summary>
        /// Renders a ListIfo to RTF.
        /// </summary>
        internal override void Render()
        {
            // The numbering state belongs to the current rendering, not to the process.
            var lists = _docRenderer.Lists;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (lists.PreviousListInfoID.Key != null && _listInfo.ContinuePreviousList)
            {
                lists.ListIDs.Add(_listInfo, lists.PreviousListInfoID.Value);
                return;
            }
            var listID = lists.NextListID;
            lists.ListIDs.Add(_listInfo, listID);

            _rtfWriter.StartContent();
            _rtfWriter.WriteControl("list");
            // rtfWriter.WriteControl("listtemplateid", templateID.ToString());
            _rtfWriter.WriteControl("listsimple", 1);
            WriteListLevel();
            _rtfWriter.WriteControl("listrestarthdn", 0);
            _rtfWriter.WriteControl("listid", listID.ToString());
            _rtfWriter.EndContent();

            lists.PreviousListInfoID = new KeyValuePair<ListInfo, int>(_listInfo, listID);
            lists.NextListID += 2;
            lists.NextTemplateID += 2;
        }

        readonly ListInfo _listInfo;

        void WriteListLevel()
        {
            ListType listType = _listInfo.ListType;
            string levelText1 = "";
            string levelText2 = "";
            string levelNumbers = "";
            int fontIdx = -1;
            switch (listType)
            {
                case ListType.NumberList1:
                    levelText1 = "'02";
                    levelText2 = "'00.";
                    levelNumbers = "'01";
                    break;

                case ListType.NumberList2:
                case ListType.NumberList3:
                    levelText1 = "'02";
                    levelText2 = "'00)";
                    levelNumbers = "'01";
                    break;

                //levelText1 = "'02";
                //levelText2 = "'00)";
                //levelNumbers = "'01";
                //break;

                case ListType.BulletList1:
                    levelText1 = "'01";
                    levelText2 = GetBulletItemText2(PredefinedFontsAndChars.Bullets.Level1Character);
                    fontIdx = _docRenderer.GetFontIndex(PredefinedFontsAndChars.Bullets.Level1FontName);
                    break;

                case ListType.BulletList2:
                    levelText1 = "'01";
                    levelText2 = GetBulletItemText2(PredefinedFontsAndChars.Bullets.Level2Character);
                    fontIdx = _docRenderer.GetFontIndex(PredefinedFontsAndChars.Bullets.Level2FontName);
                    break;

                case ListType.BulletList3:
                    levelText1 = "'01";
                    levelText2 = GetBulletItemText2(PredefinedFontsAndChars.Bullets.Level3Character);
                    fontIdx = _docRenderer.GetFontIndex(PredefinedFontsAndChars.Bullets.Level3FontName);
                    break;
            }
            WriteListLevel(levelText1, levelText2, levelNumbers, fontIdx);
        }

        string GetBulletItemText2(char c)
        {
            var text2 = Invariant($"u{(int)c} ?");
            return text2;
        }

        void WriteListLevel(string levelText1, string levelText2, string levelNumbers, int fontIdx)
        {
            _rtfWriter.StartContent();
            _rtfWriter.WriteControl("listlevel");
            // Start
            Translate("ListType", "levelnfcn", RtfUnit.Undefined, "4", false);
            Translate("ListType", "levelnfc", RtfUnit.Undefined, "4", false);
            _rtfWriter.WriteControl("leveljcn", 0);
            _rtfWriter.WriteControl("levelstartat", 1); //Start-At immer auf 1.

            _rtfWriter.WriteControl("levelold", 0); //Kompatibel mit Word 2000?

            _rtfWriter.StartContent();
            _rtfWriter.WriteControl("leveltext");
            _rtfWriter.WriteControl("leveltemplateid", _docRenderer.Lists.NextTemplateID);
            _rtfWriter.WriteControl(levelText1);
            if (levelText2 != "")
                _rtfWriter.WriteControl(levelText2);

            _rtfWriter.WriteSeparator();

            _rtfWriter.EndContent();
            _rtfWriter.StartContent();
            _rtfWriter.WriteControl("levelnumbers");
            if (levelNumbers != "")
                _rtfWriter.WriteControl(levelNumbers);

            _rtfWriter.WriteSeparator();
            _rtfWriter.EndContent();

            if (fontIdx >= 0)
                _rtfWriter.WriteControl("f", fontIdx);

            _rtfWriter.WriteControl("levelfollow", 0);

            _rtfWriter.EndContent();
        }
    }
}
