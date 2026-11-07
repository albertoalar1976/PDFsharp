// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using System.Globalization;
using System.Diagnostics;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Internals;
using PdfSharp.Diagnostics;
using BorderStyle = MigraDoc.DocumentObjectModel.BorderStyle;
using Color = MigraDoc.DocumentObjectModel.Color;
using TabAlignment = MigraDoc.DocumentObjectModel.TabAlignment;

namespace MigraDoc.RtfRendering
{
    /// <summary>
    /// This class is a base for all renderers.
    /// </summary>
    public abstract class RendererBase
    {
        /// <summary>
        /// Indicates whether the container contains an element
        /// that is of one of the specified types or inherited.
        /// </summary>
        /// <param name="coll">The collection to search.</param>
        /// <param name="types">The types to find within the collection.</param>
        /// <returns>True, if an object of one of the given types is found within the collection.</returns>
        internal static bool CollectionContainsObjectAssignableTo(DocumentObjectCollection coll, params Type[] types)
        {
            foreach (var obj in coll)
            {
                Debug.Assert(obj != null, nameof(obj) + " != null");
                foreach (Type type in types)
                {
                    // "IsInstanceOfType" is not better, it is just slower.
                    // ReSharper disable once UseMethodIsInstanceOfType
                    if (type.IsAssignableFrom(obj.GetType()))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Initializes a new instance of the RendererBase class.
        /// </summary>
        internal RendererBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the RendererBase class.
        /// </summary>
        internal RendererBase(DocumentObject domObj, RtfDocumentRenderer docRenderer)
        {
            _docObject = domObj;
            _docRenderer = docRenderer;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (docRenderer != null)
                _rtfWriter = docRenderer.RtfWriter;
            _useEffectiveValue = false;
        }

        /// <summary>
        /// Helps translating MigraDoc DOM enumerations to an RTF control word.
        /// </summary>
        static Dictionary<object, object> CreateEnumTranslationTable()
        {
            // Fill a local table and publish it only when it is complete (see _enumTranslationTable).
            var table = new Dictionary<object, object>();
            //ParagraphAlignment
            table.Add(ParagraphAlignment.Left, "l");
            table.Add(ParagraphAlignment.Right, "r");
            table.Add(ParagraphAlignment.Center, "c");
            table.Add(ParagraphAlignment.Justify, "j");

            //LineSpacingRule
            table.Add(LineSpacingRule.AtLeast, 0);
            table.Add(LineSpacingRule.Exactly, 0);
            table.Add(LineSpacingRule.Double, 1);
            table.Add(LineSpacingRule.OnePtFive, 1);
            table.Add(LineSpacingRule.Multiple, 1);
            table.Add(LineSpacingRule.Single, 1);

            //OutLineLevel
            //BodyText rendered by leaving away rtf control word.
            table.Add(OutlineLevel.Level1, 0);
            table.Add(OutlineLevel.Level2, 1);
            table.Add(OutlineLevel.Level3, 2);
            table.Add(OutlineLevel.Level4, 3);
            table.Add(OutlineLevel.Level5, 4);
            table.Add(OutlineLevel.Level6, 5);
            table.Add(OutlineLevel.Level7, 6);
            table.Add(OutlineLevel.Level8, 7);
            table.Add(OutlineLevel.Level9, 8);

            //UnderlineType
            table.Add(Underline.Dash, "dash");
            table.Add(Underline.DotDash, "dashd");
            table.Add(Underline.DotDotDash, "dashdd");
            table.Add(Underline.Dotted, "d");
            table.Add(Underline.None, "none");
            table.Add(Underline.Single, "");
            table.Add(Underline.Words, "w");

            //BorderStyle
            table.Add(BorderStyle.DashDot, "dashd");
            table.Add(BorderStyle.DashDotDot, "dashdd");
            table.Add(BorderStyle.DashLargeGap, "dash");
            table.Add(BorderStyle.DashSmallGap, "dashsm");
            table.Add(BorderStyle.Dot, "dot");
            table.Add(BorderStyle.Single, "s");
            //BorderType.None simply not rendered.

            //TabLeader
            table.Add(TabLeader.Dashes, "hyph");
            table.Add(TabLeader.Dots, "dot");
            table.Add(TabLeader.Heavy, "th");
            table.Add(TabLeader.Lines, "ul");
            table.Add(TabLeader.MiddleDot, "mdot");
            //TabLeader.Spaces rendered by leaving away the tab leader control

            //TabAlignment
            table.Add(TabAlignment.Center, "c");
            table.Add(TabAlignment.Decimal, "dec");
            table.Add(TabAlignment.Right, "r");
            table.Add(TabAlignment.Left, "l");

            //FootnoteNumberStyle
            table.Add(FootnoteNumberStyle.Arabic, "ar");
            table.Add(FootnoteNumberStyle.LowercaseLetter, "alc");
            table.Add(FootnoteNumberStyle.LowercaseRoman, "rlc");
            table.Add(FootnoteNumberStyle.UppercaseLetter, "auc");
            table.Add(FootnoteNumberStyle.UppercaseRoman, "ruc");

            //FootnoteNumberingRule
            table.Add(FootnoteNumberingRule.RestartContinuous, "rstcont");
            table.Add(FootnoteNumberingRule.RestartPage, "rstpg");
            table.Add(FootnoteNumberingRule.RestartSection, "restart");

            //FootnoteLocation
            table.Add(FootnoteLocation.BeneathText, "tj");
            table.Add(FootnoteLocation.BottomOfPage, "bj");

            //(Section) BreakType
            table.Add(BreakType.BreakEvenPage, "even");
            table.Add(BreakType.BreakOddPage, "odd");
            table.Add(BreakType.BreakNextPage, "page");

            //TODO_OLD:  ListType under construction.
            table.Add(ListType.BulletList1, 23);
            table.Add(ListType.BulletList2, 23);
            table.Add(ListType.BulletList3, 23);
            table.Add(ListType.NumberList1, 0);
            table.Add(ListType.NumberList2, 0);
            table.Add(ListType.NumberList3, 4);

            //RowAlignment
            table.Add(RowAlignment.Center, "c");
            table.Add(RowAlignment.Left, "l");
            table.Add(RowAlignment.Right, "r");

            //VerticalAlignment
            table.Add(VerticalAlignment.Top, "t");
            table.Add(VerticalAlignment.Center, "c");
            table.Add(VerticalAlignment.Bottom, "b");

            //RelativeHorizontal
            table.Add(RelativeHorizontal.Character, "margin");
            table.Add(RelativeHorizontal.Column, "margin");
            table.Add(RelativeHorizontal.Margin, "margin");
            table.Add(RelativeHorizontal.Page, "page");

            //RelativeVertical
            table.Add(RelativeVertical.Line, "para");
            table.Add(RelativeVertical.Margin, "margin");
            table.Add(RelativeVertical.Page, "page");
            table.Add(RelativeVertical.Paragraph, "para");

            //WrapStyle
            table.Add(WrapStyle.None, 3);
            //Caution: Word imterpretates "Through" (in rtf value "5") slightly different!
            table.Add(WrapStyle.Through, 3);
            table.Add(WrapStyle.TopBottom, 1);

            //LineStyle
            table.Add(LineStyle.Single, 0);

            //DashStyle
            table.Add(DashStyle.Solid, 0);
            table.Add(DashStyle.Dash, 1);
            table.Add(DashStyle.SquareDot, 2);
            table.Add(DashStyle.DashDot, 3);
            table.Add(DashStyle.DashDotDot, 4);

            //DashStyle
            table.Add(TextOrientation.Downward, 3);
            table.Add(TextOrientation.Horizontal, 0);
            table.Add(TextOrientation.HorizontalRotatedFarEast, 0);
            table.Add(TextOrientation.Upward, 2);
            table.Add(TextOrientation.Vertical, 3);
            table.Add(TextOrientation.VerticalFarEast, 3);
            return table;
        }

        /// <summary>
        /// Translates the given Unit to an RTF unit.
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="rtfUnit"></param>
        internal static int ToRtfUnit(Unit unit, RtfUnit rtfUnit)
        {
            switch (rtfUnit)
            {
                case RtfUnit.HalfPts:
                    return (int)(Math.Round(unit.Point * 2));
                case RtfUnit.Twips:
                    return (int)(Math.Round(unit.Point * 20));
                case RtfUnit.Lines:
                    return (int)(Math.Round(unit.Point * 12 * 20));
                case RtfUnit.EMU:
                    return (int)(Math.Round(unit.Point * 12700));
                case RtfUnit.CharUnit100:
                    return (int)(Math.Round(unit.Pica * 100));
            }
            return (int)unit.Point;
        }

        /// <summary>
        /// Translates a value named 'valueName' to a Rtf Control word that specifies a Unit, Enum, Bool, Int or Color.
        /// </summary>
        protected void Translate(string valueName, string rtfCtrl, RtfUnit unit, string? defaultValue, bool withStar)
        {
            var val = GetValueAsIntended(valueName);
            if (val == null)
            {
                if (defaultValue != null)
                    _rtfWriter.WriteControl(rtfCtrl, defaultValue);
                return;
            }
            else
            {
                if (val is Unit val1)
                {
                    _rtfWriter.WriteControl(rtfCtrl, ToRtfUnit(val1, unit), withStar);
                }
                else if (val is bool b)
                {
                    if (b)
                        _rtfWriter.WriteControl(rtfCtrl, withStar);
                }
                else if (val is Color color)
                {
                    int idx = _docRenderer.GetColorIndex(color);
                    _rtfWriter.WriteControl(rtfCtrl, idx, withStar);
                }
                else if (val is Enum)
                {
                    _rtfWriter.WriteControl(rtfCtrl, _enumTranslationTable[val].ToString()!, withStar);
                }
                else if (val is int i)
                {
                    _rtfWriter.WriteControl(rtfCtrl, i, withStar);
                }
                else
                    Debug.Assert(false, "Invalid use of Translate");
            }
        }

        /// <summary>
        /// Translates a value named 'valueName' to a Rtf Control word that specifies a unit, enum, bool, int or color.
        /// </summary>
        protected void Translate(string valueName, string rtfCtrl, RtfUnit unit, Unit val, bool withStar)
        {
            Translate(valueName, rtfCtrl, unit, ToRtfUnit(val, RtfUnit.Twips).ToString(CultureInfo.InvariantCulture), withStar);
        }

        /// <summary>
        /// Translates a value named 'valueName' to a Rtf Control word that specifies a unit, enum, bool or color.
        /// If it is a unit, twips are assumed as RtfUnit.
        /// </summary>
        protected void Translate(string valueName, string rtfCtrl)
        {
            Translate(valueName, rtfCtrl, RtfUnit.Twips, null, false);
        }

        /// <summary>
        /// Translates a value named 'valueName' to a Rtf Control word that specifies a boolean and divides in two control words.
        /// If the control word in false case is simply left away, you can also use the Translate function as well.
        /// </summary>
        protected void TranslateBool(string valueName, string rtfTrueCtrl, string rtfFalseCtrl, bool withStar)
        {
            var val = GetValueAsIntended(valueName);
            if (val == null)
                return;
            if ((bool)val)
                _rtfWriter.WriteControl(rtfTrueCtrl, withStar);
            else if (!rtfFalseCtrl.IsValueNullOrEmpty())
                _rtfWriter.WriteControl(rtfFalseCtrl, withStar);
        }

        /// <summary>
        /// Gets the specified value either as effective value if useEffectiveValue is set to true,
        /// otherwise returns the usual GetValue, or null if IsNull evaluates to true.
        /// </summary>
        protected virtual object? GetValueAsIntended(string valueName)
        {
            return _docObject.GetValue(valueName, GV.GetNull);
        }

        /// <summary>
        /// Typed version of GetValueAsIntended.
        /// </summary>
        protected virtual T? GetValueAsIntended2<T>(string valueName)  // TODO_OLD Use this?
        {
            return (T?)_docObject.GetValue(valueName, GV.GetNull);
        }

        /// <summary>
        /// Renders the given Unit as control / value pair of the given RTF control in the given RTF unit, optionally with a star.
        /// </summary>
        protected void RenderUnit(string rtfControl, Unit value, RtfUnit rtfUnit = RtfUnit.Twips, bool withStar = false)
        {
            _rtfWriter.WriteControl(rtfControl, ToRtfUnit(value, rtfUnit), withStar);
        }

        /// <summary>
        /// Converts the given Unit to Twips.
        /// </summary>
        internal static int ToTwips(Unit unit)
        {
            return ToRtfUnit(unit, RtfUnit.Twips);
        }

        /// <summary>
        /// Converts the given Unit to EMU
        /// </summary>
        internal static int ToEmu(Unit unit)
        {
            return ToRtfUnit(unit, RtfUnit.EMU);
        }

        /// <summary>
        /// Renders the given object to rtf, _docObj must be of type DocumentObject or DocumentObjectContainer.
        /// </summary>
        internal abstract void Render();

        /// <summary>
        /// Returns GetValueAsIntended if this evaluates non-null, otherwise the given default value.
        /// </summary>
        protected object GetValueOrDefault(string valName, object valDefault)
        {
            var obj = GetValueAsIntended(valName);
            return obj ?? valDefault;
        }

        /// <summary>
        /// Renders a trailing standard paragraph in case the last element in elements isn’t a paragraph.
        /// (Some RTF elements need to close with a paragraph.)
        /// </summary>
        protected void RenderTrailingParagraph(DocumentElements elements)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (elements == null || !(elements.LastObject is Paragraph))
            {
                //At least one paragraph needs to be written at the end of the document.
                //Otherwise, word cannot read the resulting rtf file.
                _rtfWriter.WriteControl("pard");
                _rtfWriter.WriteControl("s", _docRenderer.GetStyleIndex(StyleNames.Normal));
                new ParagraphFormatRenderer(_docRenderer.Document.Styles[StyleNames.Normal]!.ParagraphFormat, _docRenderer).Render();
                _rtfWriter.WriteControl("par");
            }
        }
        /// <summary>
        /// The DocumentObject (the current document).
        /// </summary>
        protected DocumentObject _docObject = null!;

        /// <summary>
        /// The RtfDocumentRenderer.
        /// </summary>
        protected readonly RtfDocumentRenderer _docRenderer = null!;

        /// <summary>
        /// The RtfWriter.
        /// </summary>
        internal RtfWriter _rtfWriter = null!;

        /// <summary>
        /// The translation table.
        /// It is created by the type initializer, which the runtime executes exactly once and thread-safely.
        /// Formerly, the table was created lazily by the first renderer and assigned before it was filled,
        /// so a renderer on another thread could see an incomplete table and fail with a KeyNotFoundException.
        /// </summary>
        protected static readonly Dictionary<object, object> _enumTranslationTable = CreateEnumTranslationTable();

        /// <summary>
        /// Specifies whether to use the effective value.
        /// </summary>
        protected bool _useEffectiveValue;
    }
}
