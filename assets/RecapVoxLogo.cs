using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Generates matching PNG and Windows icon assets from a single vector-like drawing.
internal static class RecapVoxLogo
{
    static void Main(string[] args)
    {
        string directory = args[0];
        Directory.CreateDirectory(directory);
        using (Bitmap bitmap = Draw(256))
            bitmap.Save(Path.Combine(directory, "recapvox-logo.png"), ImageFormat.Png);
        using (Bitmap bitmap = Draw(64))
        {
            IntPtr handle = bitmap.GetHicon();
            using (Icon icon = Icon.FromHandle(handle))
            using (FileStream file = File.Create(Path.Combine(directory, "recapvox.ico")))
                icon.Save(file);
        }
    }

    private static Bitmap Draw(int size)
    {
        Bitmap image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(image))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 256f, size / 256f);
            using (GraphicsPath tile = RoundRect(5, 5, 246, 246, 55))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(19, 43, 72)))
                g.FillPath(background, tile);

            // Conversation bubble and its continuation point.
            using (Pen bubble = new Pen(Color.FromArgb(79, 216, 225), 16))
            {
                bubble.StartCap = LineCap.Round;
                bubble.EndCap = LineCap.Round;
                bubble.LineJoin = LineJoin.Round;
                g.DrawArc(bubble, 49, 48, 158, 144, 20, 308);
                g.DrawLines(bubble, new[] { new Point(71, 174), new Point(62, 211), new Point(104, 188) });
            }

            // Three audio bars evoke live speech.
            using (Pen wave = new Pen(Color.White, 15))
            {
                wave.StartCap = LineCap.Round;
                wave.EndCap = LineCap.Round;
                g.DrawLine(wave, 91, 115, 91, 136);
                g.DrawLine(wave, 123, 91, 123, 160);
                g.DrawLine(wave, 155, 106, 155, 145);
            }
        }
        return image;
    }

    private static GraphicsPath RoundRect(int x, int y, int width, int height, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
