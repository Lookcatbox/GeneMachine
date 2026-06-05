using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChemistryReactionEditor;

static class ColorWheelImage
{
    public static ImageSource Create(int size)
    {
        int stride = size * 4;
        byte[] pixels = new byte[size * stride];
        double center = size / 2.0;
        double radius = center - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - center;
                double dy = y - center;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                int index = y * stride + x * 4;

                if (distance > radius)
                {
                    pixels[index + 3] = 0;
                    continue;
                }

                double hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
                double saturation = radius < 1e-6 ? 0 : distance / radius;
                Color color = ColorConversion.FromHsv(hue, saturation, 1);
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(
            size,
            size,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}
