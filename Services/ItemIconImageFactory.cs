using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CostamaEditor.Services;

namespace CostamaEditor.Services;

public static class ItemIconImageFactory
{
    public static BitmapSource Create(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

        if (frame.Format == PixelFormats.Indexed8 && frame.Palette is { Colors.Count: > 0 })
        {
            return CreateIndexedImage(frame);
        }

        if (frame.Format == PixelFormats.Bgr24)
            return CreateMagentaKeyedImage(frame);

        frame.Freeze();
        return frame;
    }

    private static BitmapSource CreateIndexedImage(BitmapFrame frame)
    {
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var indexedPixels = new byte[checked(width * height)];
        frame.CopyPixels(indexedPixels, width, 0);
        var palette = frame.Palette!.Colors;
        var transparentByIndexZero = CornersUsePaletteIndexZero(indexedPixels, width, height);
        var transparentByMagenta = !transparentByIndexZero && CornersUseMagentaColor(indexedPixels, width, height, palette);
        if (!transparentByIndexZero && !transparentByMagenta)
        {
            frame.Freeze();
            return frame;
        }

        var bgraPixels = new byte[checked(indexedPixels.Length * 4)];
        for (var sourceIndex = 0; sourceIndex < indexedPixels.Length; sourceIndex++)
        {
            var paletteIndex = indexedPixels[sourceIndex];
            if (paletteIndex >= palette.Count)
                throw new InvalidDataException(LocalizationService.Get("Icon.PaletteIndexInvalid"));

            var color = palette[paletteIndex];
            var targetIndex = sourceIndex * 4;
            bgraPixels[targetIndex] = color.B;
            bgraPixels[targetIndex + 1] = color.G;
            bgraPixels[targetIndex + 2] = color.R;
            var isTransparent = transparentByIndexZero ? paletteIndex == 0 : IsMagenta(color);
            bgraPixels[targetIndex + 3] = isTransparent ? (byte)0 : (byte)255;
        }

        return CreateBgraImage(frame, bgraPixels, width, height);
    }

    private static BitmapSource CreateMagentaKeyedImage(BitmapFrame frame)
    {
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var bgrPixels = new byte[checked(width * height * 3)];
        frame.CopyPixels(bgrPixels, checked(width * 3), 0);
        if (!CornersAreMagenta(bgrPixels, width, height))
        {
            frame.Freeze();
            return frame;
        }

        var bgraPixels = new byte[checked(width * height * 4)];
        for (var sourceIndex = 0; sourceIndex < width * height; sourceIndex++)
        {
            var sourceOffset = sourceIndex * 3;
            var targetOffset = sourceIndex * 4;
            var blue = bgrPixels[sourceOffset];
            var green = bgrPixels[sourceOffset + 1];
            var red = bgrPixels[sourceOffset + 2];
            bgraPixels[targetOffset] = blue;
            bgraPixels[targetOffset + 1] = green;
            bgraPixels[targetOffset + 2] = red;
            bgraPixels[targetOffset + 3] = red == 255 && green == 0 && blue == 255 ? (byte)0 : (byte)255;
        }

        return CreateBgraImage(frame, bgraPixels, width, height);
    }

    private static bool CornersUsePaletteIndexZero(byte[] pixels, int width, int height) =>
        width > 0 && height > 0 && pixels[0] == 0 && pixels[width - 1] == 0 &&
        pixels[(height - 1) * width] == 0 && pixels[(height * width) - 1] == 0;

    private static bool CornersUseMagentaColor(byte[] pixels, int width, int height, IList<Color> palette) =>
        width > 0 && height > 0 && IsMagentaAt(pixels[0], palette) && IsMagentaAt(pixels[width - 1], palette) &&
        IsMagentaAt(pixels[(height - 1) * width], palette) && IsMagentaAt(pixels[(height * width) - 1], palette);

    private static bool IsMagentaAt(byte paletteIndex, IList<Color> palette) =>
        paletteIndex < palette.Count && IsMagenta(palette[paletteIndex]);

    private static bool IsMagenta(Color color) => color.R == 255 && color.G == 0 && color.B == 255;

    private static bool CornersAreMagenta(byte[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return false;

        static bool IsMagenta(byte[] data, int pixelOffset) =>
            data[pixelOffset] == 255 && data[pixelOffset + 1] == 0 && data[pixelOffset + 2] == 255;

        var topRight = (width - 1) * 3;
        var bottomLeft = (height - 1) * width * 3;
        var bottomRight = (height * width - 1) * 3;
        return IsMagenta(pixels, 0) && IsMagenta(pixels, topRight) &&
               IsMagenta(pixels, bottomLeft) && IsMagenta(pixels, bottomRight);
    }

    private static BitmapSource CreateBgraImage(BitmapFrame frame, byte[] pixels, int width, int height)
    {
        var dpiX = frame.DpiX > 0 ? frame.DpiX : 96;
        var dpiY = frame.DpiY > 0 ? frame.DpiY : 96;
        var image = BitmapSource.Create(width, height, dpiX, dpiY, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }
}
