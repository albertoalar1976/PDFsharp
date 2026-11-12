# Migration guide for applications

This fork removed API that the upstream project had marked as obsolete.
If your application does not compile anymore, find the member below and use its replacement.
All replacements already exist in PDFsharp 6.x, so you can migrate before you switch to this fork.

## PDFsharp: drawing

| Removed | Use instead |
|---|---|
| `XMatrix.Translate`, `Scale`, `ScaleAt`, `Rotate`, `RotateAt`, `Shear`, `Skew` without order | `TranslateAppend`/`TranslatePrepend`, `ScaleAppend`/`ScalePrepend`, … (the defaults of GDI+ and WPF were contrary) |
| `XMatrix.Multiply`, `MultiplyPrepend` | `Append`, `Prepend` |
| `XStringFormat.Default`, `TopLeft`, `Center`, `TopCenter`, `BottomCenter` | `XStringFormats.Default`, `XStringFormats.TopLeft`, … (plural) |
| `XUnit` ↔ `int`/`double` implicit conversions | `XUnit.FromPoint(…)`, `XUnitPt`, `unit.Point` |
| `XFont.Name2` | `XFont.Name` |
| `XFont.GetHeight(XGraphics)` | `XFont.GetHeight()` |
| `XFont` constructors with `XFontStyle`/`XFontWeight`/`XFontStretch` or `XTypeface` | Were not implemented. Use `new XFont(familyName, emSize, XFontStyleEx, …)` |
| `XFontFamily.Families`, `XFontFamily.GetFamilies(XGraphics)` | Threw exceptions. Use `SystemFontResolver.GetFamilyNames()` |
| `XColor.FromArgb(int)` | `XColor.FromArgb(uint)` |
| `XColor.FromName(string)` | `XColors.…` or `XColor.FromArgb(…)` |
| `XImage.Width`, `XImage.Height`, `XForm.Width`, `XForm.Height` | `PixelWidth`/`PixelHeight` or `PointWidth`/`PointHeight` |
| `XPoint - XSize` | `XPoint - XVector` |
| `XPageDirection.Upwards` | Was not implemented. |
| `GlobalFontSettings.DefaultFontName` | No replacement. Specify a font family. |

## PDFsharp: documents and objects

| Removed | Use instead |
|---|---|
| `PdfFontEmbedding.Automatic`, `Default`, `None` | `PdfFontEmbedding.TryComputeSubset` |
| `PdfFontEmbedding.Always` | `PdfFontEmbedding.EmbedCompleteFontFile` |
| `PdfDocumentOpenMode.ReadOnly`, `InformationOnly` | `PdfDocumentOpenMode.Import` (they were not implemented) |
| `PdfDocument.AcroForm`, `PdfCatalog.AcroForm` | `Catalog.GetAcroForm()` or `Catalog.GetOrCreateAcroForm()` |
| `PdfCatalog.Metadata`, `PdfCatalog.Destinations` | `GetMetadata()`/`GetOrCreateMetadata()`, `GetDestinations()`/`GetRequiredDestinations()` |
| `PdfDocument.AddEmbeddedFile(name, path/stream)` | `EmbeddedFilesManager.ForDocument(document).AddFile(new EmbeddedFileInfo { … })` |
| `PdfDocument.Flatten()` | Did nothing. Remove the call. |
| `PdfInternals` | `PdfDocumentInternals` |
| `PdfDocumentInternals.AllObjects` | `GetAllObjects()` |
| `PdfPage.MediaBoxReadOnly`, `CropBoxReadOnly`, `BleedBoxReadOnly`, `TrimBoxReadOnly`, `ArtBoxReadOnly` | `HasMediaBox` and `MediaBox`, etc. |
| `PdfRectangle.IsEmpty` | `PdfRectangle.IsZero` |
| `PdfRectangle.Empty` | Threw an exception. Use `new PdfRectangle()` and `IsZero`. |
| `PdfObject.ReferenceNotNull` | `RequiredReference` |
| `PdfMetadata.Xml` | `ToString()` |
| `PdfName.AddSlash`, `PdfName.RemoveSlash` | `Name.MakeName`, `Name.RemoveSlash` |
| `PdfDate(DateTime)`, `Elements.SetDateTime(key, DateTime)` | The overloads with `DateTimeOffset` |
| `Elements.SetReference(key, PdfObject)` | `Elements.SetObject(key, obj)` with an indirect object |
| `PdfStream.TryUnfilter()` | `TryUncompress()` |
| `PdfAcroField.DescendantNames` | `GetDescendantNames()` |
| `PdfStandardSecurityHandler.SetEncryption(DefaultEncryption)`, `DefaultEncryption` | `SetEncryption(PdfDefaultEncryption)`, `PdfDefaultEncryption` |

## PDFsharp: annotations and attachments

| Removed | Use instead |
|---|---|
| Parameterless constructors of `PdfLinkAnnotation`, `PdfStampAnnotation`, `PdfTextAnnotation`, `PdfWidgetAnnotation`, `PdfMarkupAnnotation`, `PdfTextMarkupAnnotation` | The constructors with a `PdfDocument` parameter |
| `PdfLinkAnnotation.CreateDocumentLink`, `CreateEmbeddedDocumentLink`, `CreateWebLink`, `CreateFileLink` without page | The overloads with a `PdfPage` as first parameter |
| `PdfRubberStampAnnotation`, `PdfAnnotationTypes.RubberStamp` | `PdfStampAnnotation`, `PdfAnnotationTypes.Stamp` |
| `PdfFileSpecification(document, embeddedFileStream, name)` | The constructor with an `EmbeddedFileInfo` parameter |

## PDFsharp: accessibility (PDF/UA)

The short tag names were aliases of the long ones, with the same values, and are removed.
The PDF files are unchanged: the tags are still written with their PDF names (`P`, `H1`, `Sect`, …).

| Removed | Use instead |
|---|---|
| `PdfBlockLevelElementTag.P`, `H`, `H1`…`H6`, `L`, `LI`, `Lbl`, `LBody`, `TR`, `TH`, `TD`, `THead`, `TBody`, `TFoot` | `Paragraph`, `Heading`, `Heading1`…`Heading6`, `List`, `ListItem`, `Label`, `ListBody`, `TableRow`, `TableHeaderCell`, `TableDataCell`, `TableHeadRowGroup`, `TableBodyRowGroup`, `TableFooterRowGroup` |
| `PdfGroupingElementTag.Art`, `Sect`, `Div`, `BlockQuote`, `TOC`, `TOCI`, `NonStruct`, `Private` | `Article`, `Section`, `Division`, `BlockQuotation`, `TableOfContents`, `TableOfContentsItem`, `NonstructuralElement`, `PrivateElement` |
| `PdfInlineLevelElementTag.Quote`, `BibEntry`, `Annot` | `Quotation`, `BibliographyEntry`, `Annotation` |

## PDFsharp: bar codes

| Removed | Use instead |
|---|---|
| `Code3of9Standard()` | A constructor with a `code` parameter |

## MigraDoc

| Removed | Use instead |
|---|---|
| `Unit` → `double`/`float` implicit conversions | `unit.Point` (or `Millimeter`, `Centimeter`, …) |
| `PageSetup.EffectivePageWidth`, `EffectivePageHeight` | `PageWidth`, `PageHeight` (they return the effective size) |
| `BorderType.Horizontal`, `BorderType.Vertical` | Were not used. |
| `PdfDocumentRenderer(bool unicode)`, `PdfDocumentRenderer.Unicode` | `new PdfDocumentRenderer()`; text is always Unicode |
| `MigraDocRenderingBuildInformation` | `PdfSharp.Internal.SemVersionInformation` |

## Test and sample helpers

| Removed | Use instead |
|---|---|
| `FeatureAndSnippetBase.SaveAndShowPdfDocument` and similar | The `PdfFileUtility`, `PngFileUtility`, … classes |
| `IOHelper` | `IOUtility` |
