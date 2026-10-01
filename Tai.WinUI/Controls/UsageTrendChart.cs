using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Tai.WinUI.Services;
using Windows.Foundation;

namespace Tai.WinUI.Controls;

public sealed class UsageTrendChart : UserControl
{
    private readonly Canvas _canvas = new();
    private INotifyCollectionChanged? _collection;

    public UsageTrendChart()
    {
        MinHeight = 220;
        Content = _canvas;
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => { Subscribe(); Render(); };
        Unloaded += (_, _) => Unsubscribe();
    }

    public object? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(object),
        typeof(UsageTrendChart),
        new PropertyMetadata(null, (dependencyObject, args) =>
        {
            var chart = (UsageTrendChart)dependencyObject;
            chart.Subscribe();
            chart.Render();
        }));

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Render();

    private void Subscribe()
    {
        Unsubscribe();
        if (!IsLoaded) return;
        _collection = Items as INotifyCollectionChanged;
        if (_collection != null) _collection.CollectionChanged += Items_CollectionChanged;
    }

    private void Unsubscribe()
    {
        if (_collection != null) _collection.CollectionChanged -= Items_CollectionChanged;
        _collection = null;
    }

    private void Render()
    {
        if (!IsLoaded || ActualWidth < 1 || ActualHeight < 1) return;
        _canvas.Children.Clear();

        var points = (Items as IEnumerable<TrendPoint>)?.ToList() ?? new List<TrendPoint>();
        if (points.Count == 0)
        {
            AddEmptyMessage(Tai.WinUI.Services.L.Text("此时间范围内暂无趋势数据"));
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        const double left = 58;
        const double right = 10;
        const double top = 12;
        const double labelHeight = 34;
        var plotBottom = Math.Max(top + 20, height - labelHeight);
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, plotBottom - top);

        var actualPoints = points.Where(point => !point.IsFuture).ToList();
        var maximum = GetNiceMaximum(actualPoints.Count == 0 ? 0 : actualPoints.Max(point => point.Seconds));
        DrawGrid(width, top, plotBottom, plotHeight, maximum, left, right);

        var slotWidth = plotWidth / points.Count;
        var barWidth = Math.Max(3, Math.Min(38, slotWidth * 0.56));
        var labelStep = Math.Max(1, (int)Math.Ceiling(points.Count * 42d / plotWidth));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = left + slotWidth * (index + 0.5);
            if (!point.IsFuture)
            {
                var barHeight = Math.Max(3, point.Seconds / maximum * (plotHeight - 8));
                var bar = new Button
                {
                    Style = (Style)Application.Current.Resources["TaiFilledButtonStyle"],
                    Width = barWidth, Height = barHeight, MinWidth = 0, MinHeight = 0,
                    Padding = new Thickness(0), BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(4, 4, 2, 2),
                    Background = Brush(point.Seconds > 0 ? "TaiChartBrush" : "TaiCardMutedBrush"),
                    Foreground = Brush("TaiPrimaryTextBrush"),
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };
                var description = $"{point.Label} · {point.Duration}";
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(bar, description);
                ToolTipService.SetToolTip(bar, description);
                var flyout = new Flyout { Content = new TextBlock { Text = description } };
                bar.Flyout = flyout;
                Canvas.SetLeft(bar, x - barWidth / 2);
                Canvas.SetTop(bar, plotBottom - barHeight);
                _canvas.Children.Add(bar);
            }
            if (index % labelStep != 0 && index != points.Count - 1) continue;
            // Do not collide the penultimate label with the last one on narrow charts.
            if (index != points.Count - 1 && points.Count - 1 - index < labelStep) continue;
            var label = new TextBlock
            {
                Text = point.Label.Contains(' ') ? point.Label[(point.Label.LastIndexOf(' ') + 1)..] : point.Label,
                Width = 44, FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Foreground = Brush("TaiSecondaryTextBrush")
            };
            Canvas.SetLeft(label, Math.Clamp(x - 22, left - 12, width - 44));
            Canvas.SetTop(label, plotBottom + 10);
            _canvas.Children.Add(label);
        }
        if (actualPoints.All(point => point.Seconds <= 0))
            AddEmptyMessage(Tai.WinUI.Services.L.Text("这个时间范围内还没有使用记录"), top + plotHeight / 2 - 12);
    }

    private void DrawGrid(double width, double top, double plotBottom, double plotHeight, double maximum, double left, double right)
    {
        for (var row = 0; row < 4; row++)
        {
            var y = top + plotHeight * row / 3;
            _canvas.Children.Add(new Line
            {
                X1 = left,
                X2 = width - right,
                Y1 = y,
                Y2 = y,
                Stroke = Brush("TaiDividerBrush"),
                StrokeThickness = 1
            });

            var value = maximum * (3 - row) / 3;
            var label = new TextBlock
            {
                Text = FormatAxisValue(value),
                Width = 48,
                FontSize = 11,
                TextAlignment = TextAlignment.Right,
                Foreground = Brush("TaiSecondaryTextBrush")
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, Math.Clamp(y - 8, top - 2, plotBottom - 15));
            _canvas.Children.Add(label);
        }
    }

    private void AddEmptyMessage(string text, double? top = null)
    {
        var message = new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(40, ActualWidth - 78),
            Foreground = Brush("TaiSecondaryTextBrush")
        };
        Canvas.SetLeft(message, 68);
        Canvas.SetTop(message, top ?? Math.Max(12, ActualHeight / 2 - 12));
        _canvas.Children.Add(message);
    }

    private static double GetNiceMaximum(double value)
    {
        if (value <= 15 * 60) return 15 * 60;
        if (value <= 60 * 60) return Math.Ceiling(value / (15 * 60)) * 15 * 60;
        return Math.Ceiling(value / (60 * 60)) * 60 * 60;
    }

    private static string FormatAxisValue(double seconds)
    {
        if (seconds <= 0) return "0";
        if (seconds < 60 * 60) return Tai.WinUI.Services.L.IsEnglish
            ? $"{Math.Round(seconds / 60):0} min" : $"{Math.Round(seconds / 60):0}分";
        var hours = seconds / 3600;
        if (Tai.WinUI.Services.L.IsEnglish) return $"{hours:0.#} hr";
        return Math.Abs(hours - Math.Round(hours)) < 0.01
            ? $"{hours:0}小时"
            : $"{hours:0.#}小时";
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
