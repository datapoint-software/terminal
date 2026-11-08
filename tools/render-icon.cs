#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:package SkiaSharp@3.119.4
#:package SkiaSharp.NativeAssets.macOS@3.119.4

// Renders terminal-icon.svg to the files each platform needs: PNGs for Linux and the window, an .ico for Windows and an
// .iconset for macOS, which `iconutil -c icns` turns into an .icns. It reads only the rects and paths the icon uses.
// SkiaSharp is the renderer Avalonia already ships with the application. Runs on macOS.
// Usage, from the repository's root: dotnet run tools/render-icon.cs branding/terminal-icon.svg <folder>, then copy
// terminal-icon-512.png to branding/terminal-icon.png, terminal-icon.ico to branding/, and run
// iconutil -c icns <folder>/terminal-icon.iconset -o branding/terminal-icon.icns
using System.Globalization;
using System.Xml.Linq;
using SkiaSharp;

var shapes = XDocument.Load(args[0]).Root!.Elements().Where(e => e.Name.LocalName is "rect" or "path").ToList();
var folder = args[1];
Directory.CreateDirectory(folder);

var pngs = new Dictionary<int, byte[]>();
foreach (var size in IconRenderer.PngSizes)
{
    pngs[size] = IconRenderer.Png(shapes, size, inset: 0);
    File.WriteAllBytes(Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"terminal-icon-{size}.png")), pngs[size]);
}

IconRenderer.WriteIco(Path.Combine(folder, "terminal-icon.ico"), [.. IconRenderer.IcoSizes.Select(size => (size, pngs[size]))]);

// Apple's grid puts the icon's body in the middle 824 of 1024 points.
var iconset = Path.Combine(folder, "terminal-icon.iconset");
Directory.CreateDirectory(iconset);
foreach (var (points, scale) in IconRenderer.MacSizes)
{
    var name = string.Create(CultureInfo.InvariantCulture, $"icon_{points}x{points}{(scale == 1 ? string.Empty : "@2x")}.png");
    File.WriteAllBytes(Path.Combine(iconset, name), IconRenderer.Png(shapes, points * scale, inset: 100f / 1024f));
}

Console.WriteLine("ok");

internal static class IconRenderer
{
    public static readonly int[] PngSizes = [16, 24, 32, 48, 64, 128, 256, 512, 1024];

    public static readonly int[] IcoSizes = [16, 24, 32, 48, 64, 256];

    // In points, at 1x and 2x, as iconutil expects.
    public static readonly (int Points, int Scale)[] MacSizes = [(16, 1), (16, 2), (32, 1), (32, 2), (128, 1), (128, 2), (256, 1), (256, 2), (512, 1), (512, 2)];

    // inset: the share of the canvas left empty on each side.
    public static byte[] Png(IReadOnlyList<XElement> shapes, int size, float inset)
    {
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(size * inset, size * inset);
            canvas.Scale(size * (1 - (2 * inset)) / 1024f);
            foreach (var shape in shapes)
            {
                Draw(canvas, shape);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // An .ico of PNG images, which Windows reads since Vista.
    public static void WriteIco(string path, IReadOnlyList<(int Size, byte[] Png)> images)
    {
        using var ico = new BinaryWriter(File.Create(path));
        ico.Write((ushort)0);
        ico.Write((ushort)1);
        ico.Write((ushort)images.Count);
        var offset = 6 + (16 * images.Count);
        foreach (var (size, png) in images)
        {
            var side = (byte)(size >= 256 ? 0 : size);
            ico.Write(side);
            ico.Write(side);
            ico.Write((ushort)0);
            ico.Write((ushort)1);
            ico.Write((ushort)32);
            ico.Write(png.Length);
            ico.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            ico.Write(png);
        }
    }

    private static void Draw(SKCanvas canvas, XElement shape)
    {
        using var path = Outline(shape);
        var fill = (string?)shape.Attribute("fill") ?? "#000000";
        if (!string.Equals(fill, "none", StringComparison.Ordinal))
        {
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = Color(fill, Number(shape, "fill-opacity", 1)) };
            canvas.DrawPath(path, paint);
        }

        if ((string?)shape.Attribute("stroke") is not { } stroke || string.Equals(stroke, "none", StringComparison.Ordinal))
        {
            return;
        }

        using var pen = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = Color(stroke, 1),
            StrokeWidth = Number(shape, "stroke-width", 1),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, pen);
    }

    private static SKPath Outline(XElement shape)
    {
        if (string.Equals(shape.Name.LocalName, "path", StringComparison.Ordinal))
        {
            return SKPath.ParseSvgPathData(shape.Attribute("d")!.Value);
        }

        var path = new SKPath();
        var radius = Number(shape, "rx", 0);
        path.AddRoundRect(SKRect.Create(Number(shape, "x", 0), Number(shape, "y", 0), Number(shape, "width", 0), Number(shape, "height", 0)), radius, radius);
        return path;
    }

    private static float Number(XElement shape, string name, float fallback) =>
        shape.Attribute(name) is { } value ? float.Parse(value.Value, CultureInfo.InvariantCulture) : fallback;

    private static SKColor Color(string value, float opacity) => SKColor.Parse(value).WithAlpha((byte)Math.Round(255 * opacity));
}
