using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LunaCopy.Native;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace LunaCopy.Services;

public static class IconGenerator
{
    public static DrawingImage CreateClipboardCheckDrawingImage(bool isActive)
    {
        Color primaryColor = isActive
            ? Color.FromRgb(0x3B, 0x82, 0xF6) // #3B82F6 Accent Blue
            : Color.FromRgb(0x6B, 0x72, 0x80); // #6B7280 Muted Gray

        Color clipColor = isActive
            ? Color.FromRgb(0x93, 0xC5, 0xFD) // Lighter Blue for top clip
            : Color.FromRgb(0x9C, 0xA3, 0xAF); // Lighter Gray for top clip

        var group = new DrawingGroup();

        // Invisible 64x64 bounds to keep consistent padding
        group.Children.Add(new GeometryDrawing(
            System.Windows.Media.Brushes.Transparent,
            null,
            new RectangleGeometry(new Rect(0, 0, 64, 64))));

        // Main clipboard board body
        var boardBrush = new SolidColorBrush(primaryColor);
        var boardGeo = new RectangleGeometry(new Rect(10, 12, 44, 46), 7, 7);
        group.Children.Add(new GeometryDrawing(boardBrush, null, boardGeo));

        // Inner dark cutout for modern depth OR solid badge with white checkmark
        // Top clipboard clip holder
        var clipBrush = new SolidColorBrush(clipColor);
        var clipPen = new Pen(new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1D)), 2.5);
        var clipGeo = new RectangleGeometry(new Rect(21, 5, 22, 12), 4, 4);
        group.Children.Add(new GeometryDrawing(clipBrush, clipPen, clipGeo));

        // Crisp white checkmark in the center of the board
        var checkGeometry = new StreamGeometry();
        using (var ctx = checkGeometry.Open())
        {
            ctx.BeginFigure(new Point(21, 36), false, false);
            ctx.LineTo(new Point(29, 44), true, true);
            ctx.LineTo(new Point(44, 27), true, true);
        }
        checkGeometry.Freeze();

        var checkPen = new Pen(System.Windows.Media.Brushes.White, 5.2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        checkPen.Freeze();

        group.Children.Add(new GeometryDrawing(null, checkPen, checkGeometry));
        group.Freeze();

        var drawingImage = new DrawingImage(group);
        drawingImage.Freeze();
        return drawingImage;
    }

    public static Icon CreateTrayIcon(bool isActive)
    {
        const int size = 32;
        var drawingImage = CreateClipboardCheckDrawingImage(isActive);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(drawingImage, new Rect(0, 0, size, size));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;

        using var bmp = new Bitmap(ms);
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
