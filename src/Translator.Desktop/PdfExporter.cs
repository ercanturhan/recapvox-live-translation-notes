using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Translator.Desktop;

// Self-contained page-image PDF export. WPF renders Unicode with the installed
// Segoe UI font, so Turkish text does not depend on a PDF font package.
internal static class PdfExporter
{
    private const double PageWidth = 794;
    private const double PageHeight = 1123;
    private const double Margin = 55;
    private const double LineHeight = 24;

    public static void Save(string path, string text)
    {
        var lines = Wrap(text);
        var pageCapacity = (int)((PageHeight - 2 * Margin) / LineHeight);
        var pages = lines.Chunk(pageCapacity).Select(RenderPage).ToArray();
        if (pages.Length == 0) pages = [RenderPage([])];
        WritePdf(path, pages);
    }

    private static List<string> Wrap(string text)
    {
        var output = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (paragraph.Length == 0) { output.Add(""); continue; }
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (Measure(candidate) <= PageWidth - 2 * Margin) { line = candidate; continue; }
                if (line.Length > 0) { output.Add(line); line = ""; }
                if (Measure(word) <= PageWidth - 2 * Margin) { line = word; continue; }
                foreach (var character in word)
                {
                    if (Measure(line + character) > PageWidth - 2 * Margin && line.Length > 0)
                    {
                        output.Add(line);
                        line = "";
                    }
                    line += character;
                }
            }
            output.Add(line);
        }
        return output;
    }

    private static double Measure(string text) => new FormattedText(text, CultureInfo.GetCultureInfo("tr-TR"),
        FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.Black, 1.0).WidthIncludingTrailingWhitespace;

    private static byte[] RenderPage(string[] lines)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, PageWidth, PageHeight));
            for (var i = 0; i < lines.Length; i++)
            {
                var formatted = new FormattedText(lines[i], CultureInfo.GetCultureInfo("tr-TR"),
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.Black, 1.0);
                dc.DrawText(formatted, new Point(Margin, Margin + i * LineHeight));
            }
        }
        var bitmap = new RenderTargetBitmap((int)Math.Round(PageWidth * 1.5), (int)Math.Round(PageHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new JpegBitmapEncoder { QualityLevel = 88 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void WritePdf(string path, IReadOnlyList<byte[]> images)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new List<long> { 0 };
        static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
        void Raw(string value) => stream.Write(Ascii(value));
        void Obj(int id, string body)
        {
            offsets.Add(stream.Position);
            Raw($"{id} 0 obj\n{body}\nendobj\n");
        }
        Raw("%PDF-1.4\n");
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var references = string.Join(" ", Enumerable.Range(0, images.Count).Select(i => $"{3 + i * 3} 0 R"));
        Obj(2, $"<< /Type /Pages /Kids [ {references} ] /Count {images.Count} >>");
        for (var i = 0; i < images.Count; i++)
        {
            var page = 3 + i * 3;
            Obj(page, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im0 {page + 1} 0 R >> >> /Contents {page + 2} 0 R >>");
            offsets.Add(stream.Position);
            Raw($"{page + 1} 0 obj\n<< /Type /XObject /Subtype /Image /Width {(int)Math.Round(PageWidth * 1.5)} /Height {(int)Math.Round(PageHeight * 1.5)} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {images[i].Length} >>\nstream\n");
            stream.Write(images[i]);
            Raw("\nendstream\nendobj\n");
            var command = Ascii("q 595 0 0 842 0 0 cm /Im0 Do Q\n");
            offsets.Add(stream.Position);
            Raw($"{page + 2} 0 obj\n<< /Length {command.Length} >>\nstream\n");
            stream.Write(command);
            Raw("endstream\nendobj\n");
        }
        var xref = stream.Position;
        Raw($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Raw($"{offset:0000000000} 00000 n \n");
        Raw($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
    }
}
