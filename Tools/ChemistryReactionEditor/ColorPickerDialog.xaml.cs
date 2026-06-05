using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ChemistryReactionEditor;

public partial class ColorPickerDialog : Window
{
    const double WheelSize = 228;
    const double WheelRadius = 113;

    bool includeAlpha;
    byte alpha = 255;
    double hue;
    double saturation = 1;
    double value = 1;
    bool suppressHexUpdate;
    bool draggingWheel;
    bool draggingBrightness;

    public string SelectedColorHex { get; private set; } = "#FFFFFF";

    public ColorPickerDialog(string initialHex, Window? owner)
    {
        InitializeComponent();
        Owner = owner;

        Color initial = ColorConversion.NormalizeOrDefault(initialHex);
        includeAlpha = !string.IsNullOrWhiteSpace(initialHex) &&
                       initialHex.Trim().Length == 9;
        alpha = initial.A;

        (hue, saturation, value) = ColorConversion.ToHsv(initial);
        SelectedColorHex = ColorConversion.ToHex(initial, includeAlpha);

        Loaded += (_, _) =>
        {
            WheelImage.Source = ColorWheelImage.Create(228);
            UpdateBrightnessGradient();
            ApplyCurrentColor();
            HexBox.Text = SelectedColorHex;
        };
    }

    void Wheel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        draggingWheel = true;
        ((UIElement)sender).CaptureMouse();
        UpdateWheelFromPoint(e.GetPosition((IInputElement)sender));
    }

    void Wheel_MouseMove(object sender, MouseEventArgs e)
    {
        if (!draggingWheel)
            return;
        UpdateWheelFromPoint(e.GetPosition((IInputElement)sender));
    }

    void Wheel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        draggingWheel = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    void Brightness_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        draggingBrightness = true;
        ((UIElement)sender).CaptureMouse();
        UpdateBrightnessFromPoint(e.GetPosition((IInputElement)sender));
    }

    void Brightness_MouseMove(object sender, MouseEventArgs e)
    {
        if (!draggingBrightness)
            return;
        UpdateBrightnessFromPoint(e.GetPosition((IInputElement)sender));
    }

    void Brightness_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        draggingBrightness = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    void UpdateWheelFromPoint(Point point)
    {
        double center = WheelSize / 2;
        double dx = point.X - center;
        double dy = point.Y - center;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance > WheelRadius)
        {
            double scale = WheelRadius / distance;
            dx *= scale;
            dy *= scale;
            distance = WheelRadius;
        }

        hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
        saturation = WheelRadius < 1e-6 ? 0 : distance / WheelRadius;
        UpdateBrightnessGradient();
        ApplyCurrentColor();
    }

    void UpdateBrightnessFromPoint(Point point)
    {
        double height = Math.Max(BrightnessGradient.ActualHeight, 1);
        value = Math.Clamp(1 - point.Y / height, 0, 1);
        ApplyCurrentColor();
    }

    void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressHexUpdate)
            return;

        string text = HexBox.Text.Trim();
        if (!text.StartsWith('#'))
            text = "#" + text;

        if (!ColorConversion.TryParseHex(text, out Color color))
        {
            HexErrorText.Visibility = Visibility.Visible;
            return;
        }

        HexErrorText.Visibility = Visibility.Collapsed;
        includeAlpha = text.Length == 9;
        alpha = color.A;
        (hue, saturation, value) = ColorConversion.ToHsv(color);
        UpdateBrightnessGradient();
        UpdatePreview(color);
        UpdateWheelThumb();
        UpdateBrightnessThumb();
    }

    void HexBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (HexErrorText.Visibility == Visibility.Visible)
            return;

        suppressHexUpdate = true;
        HexBox.Text = SelectedColorHex;
        suppressHexUpdate = false;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        string text = HexBox.Text.Trim();
        if (!text.StartsWith('#'))
            text = "#" + text;

        if (!ColorConversion.TryParseHex(text, out Color color))
        {
            HexErrorText.Visibility = Visibility.Visible;
            HexBox.Focus();
            return;
        }

        SelectedColorHex = ColorConversion.ToHex(color, text.Length == 9);
        DialogResult = true;
    }

    void ApplyCurrentColor()
    {
        Color color = ColorConversion.FromHsv(hue, saturation, value, alpha);
        SelectedColorHex = ColorConversion.ToHex(color, includeAlpha);
        UpdatePreview(color);
        UpdateWheelThumb();
        UpdateBrightnessThumb();

        suppressHexUpdate = true;
        HexBox.Text = SelectedColorHex;
        suppressHexUpdate = false;
        HexErrorText.Visibility = Visibility.Collapsed;
    }

    void UpdatePreview(Color color)
    {
        PreviewBorder.Background = new SolidColorBrush(color);
    }

    void UpdateWheelThumb()
    {
        double center = WheelSize / 2;
        double distance = saturation * WheelRadius;
        double radians = hue * Math.PI / 180.0;
        double x = center + Math.Cos(radians) * distance - WheelThumb.Width / 2;
        double y = center + Math.Sin(radians) * distance - WheelThumb.Height / 2;
        Canvas.SetLeft(WheelThumb, x);
        Canvas.SetTop(WheelThumb, y);
    }

    void UpdateBrightnessGradient()
    {
        Color top = ColorConversion.FromHsv(hue, saturation, 1, alpha);
        Color bottom = ColorConversion.FromHsv(hue, saturation, 0, alpha);
        BrightnessGradient.Fill = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1));
    }

    void UpdateBrightnessThumb()
    {
        double height = Math.Max(BrightnessGradient.ActualHeight, 228);
        double y = (1 - value) * height - BrightnessThumb.Height / 2;
        Canvas.SetTop(BrightnessThumb, Math.Clamp(y, 0, height - BrightnessThumb.Height));
    }
}
