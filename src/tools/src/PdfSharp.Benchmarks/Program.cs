// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

// Measures time and allocated bytes of typical PDFsharp and MigraDoc workloads.
// Each workload runs once to warm up, then several times; the median and minimum times are reported.
#pragma warning disable CA1050 // Top-level program.
using System.Diagnostics;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Quality;

GlobalFontSettings.FontResolver = new UnitTestFontResolver();

byte[] created = [];
var results = new List<string>();
Run("PDFsharp: create 200 pages", 5, () => created = CreatePdf(200));
Run("PDFsharp: open + save (Modify)", 5, () => OpenAndSave(created));
Run("MigraDoc: table 3000 rows", 3, () => RenderMigraDoc(TableDocument(3000)));
Run("MigraDoc: 2000 paragraphs, alternating fonts", 3, () => RenderMigraDoc(FontDocument(2000)));
results.ForEach(Console.WriteLine);

void Run(string name, int iterations, Action action)
{
    action(); // Warm-up.
    var times = new List<double>(); long bytes = 0;
    for (int i = 0; i < iterations; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    times.Sort();
    results.Add($"{name,-48} median {times[times.Count / 2],8:F1} ms   min {times[0],8:F1} ms   alloc {bytes / 1024.0 / 1024.0,8:F1} MB");
}

static byte[] CreatePdf(int pages)
{
    var doc = new PdfDocument();
    var font = new XFont("Arial", 10);
    var bold = new XFont("Arial", 14, XFontStyleEx.Bold);
    for (int p = 0; p < pages; p++)
    {
        var page = doc.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawString($"Page {p + 1}", bold, XBrushes.Black, 40, 40);
        for (int line = 0; line < 50; line++)
            gfx.DrawString($"Line {line}: The quick brown fox jumps over the lazy dog. 0123456789 {p * line}", font, XBrushes.Black, 40, 70 + line * 14);
        for (int i = 0; i < 40; i++)
        {
            gfx.DrawLine(XPens.Red, 10 + i * 13.37, 10, 300.25, 800 - i * 7.5);
            gfx.DrawRectangle(XPens.Blue, XBrushes.LightGray, 300 + i * 5.5, 600 + i * 3.25, 20.5, 10.25);
        }
    }
    var ms = new MemoryStream();
    doc.Save(ms);
    return ms.ToArray();
}

static void OpenAndSave(byte[] pdf)
{
    using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
    doc.Save(new MemoryStream());
}

static Document TableDocument(int rows)
{
    var document = new Document();
    var section = document.AddSection();
    var table = section.AddTable();
    table.Borders.Width = 0.5;
    for (int c = 0; c < 5; c++) table.AddColumn(Unit.FromCentimeter(3.2));
    var header = table.AddRow(); header.HeadingFormat = true;
    for (int c = 0; c < 5; c++) header.Cells[c].AddParagraph($"Header {c}");
    for (int r = 0; r < rows; r++)
    {
        var row = table.AddRow();
        for (int c = 0; c < 5; c++) row.Cells[c].AddParagraph($"R{r} C{c} value {r * c}");
    }
    return document;
}

static Document FontDocument(int paragraphs)
{
    string[] fonts = ["Arial", "Times New Roman", "Courier New"];
    var document = new Document();
    var section = document.AddSection();
    for (int i = 0; i < paragraphs; i++)
    {
        var p = section.AddParagraph($"Paragraph {i}: The quick brown fox jumps over the lazy dog. ");
        p.AddFormattedText("Bold text. ", TextFormat.Bold);
        p.AddFormattedText("Italic text.", TextFormat.Italic);
        p.Format.Font.Name = fonts[i % fonts.Length];
        p.Format.Font.Size = 9 + i % 4;
    }
    return document;
}

static void RenderMigraDoc(Document document)
{
    var renderer = new PdfDocumentRenderer { Document = document };
    renderer.RenderDocument();
    renderer.PdfDocument.Save(new MemoryStream());
}
