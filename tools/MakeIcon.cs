using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EasyIpSwitch.Tools
{
    internal static class MakeIcon
    {
        private static void Main(string[] args)
        {
            string output = args.Length > 0 ? args[0] : "app.ico";
            int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

            List<byte[]> images = new List<byte[]>();
            List<int> imageSizes = new List<int>();

            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using (Bitmap bitmap = Render(size))
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    images.Add(stream.ToArray());
                    imageSizes.Add(size);
                }
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream file = new FileStream(output, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(file))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)images.Count);

                int offset = 6 + 16 * images.Count;
                for (int i = 0; i < images.Count; i++)
                {
                    int size = imageSizes[i];
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }

                for (int i = 0; i < images.Count; i++)
                {
                    writer.Write(images[i]);
                }
            }

            Console.WriteLine("Icône générée : " + Path.GetFullPath(output));
        }

        private static Bitmap Render(int size)
        {
            int supersample = size <= 64 ? 4 : 1;
            int canvas = size * supersample;
            Bitmap big = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(big))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float radius = canvas * 0.22f;
                RectangleF box = new RectangleF(canvas * 0.03f, canvas * 0.03f, canvas * 0.94f, canvas * 0.94f);
                using (GraphicsPath path = Rounded(box, radius))
                using (LinearGradientBrush brush = new LinearGradientBrush(box, ColorTranslator.FromHtml("#3B82F6"), ColorTranslator.FromHtml("#1E40AF"), 55f))
                {
                    g.FillPath(brush, path);
                }

                float cx = canvas / 2f;
                float cy = canvas / 2f;
                float len = canvas * 0.46f;
                float gap = canvas * 0.17f;
                float thick = canvas * 0.095f;
                float head = canvas * 0.17f;

                using (Pen pen = new Pen(Color.White, thick))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, cx - len / 2f, cy - gap, cx + len / 2f, cy - gap);
                    g.DrawLine(pen, cx - len / 2f, cy + gap, cx + len / 2f, cy + gap);
                }

                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    g.FillPolygon(brush, new PointF[]
                    {
                        new PointF(cx + len / 2f + head * 0.2f, cy - gap),
                        new PointF(cx + len / 2f - head, cy - gap - head * 0.8f),
                        new PointF(cx + len / 2f - head, cy - gap + head * 0.8f)
                    });
                    g.FillPolygon(brush, new PointF[]
                    {
                        new PointF(cx - len / 2f - head * 0.2f, cy + gap),
                        new PointF(cx - len / 2f + head, cy + gap - head * 0.8f),
                        new PointF(cx - len / 2f + head, cy + gap + head * 0.8f)
                    });
                }
            }

            if (supersample == 1)
            {
                return big;
            }

            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(big, new Rectangle(0, 0, size, size));
            }
            big.Dispose();
            return result;
        }

        private static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
