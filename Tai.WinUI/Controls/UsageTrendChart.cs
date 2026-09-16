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
        Loaded += (_, _) => Render();
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
            if (chart._collection != null) chart._collection.CollectionChanged -= chart.Items_CollectionChanged;
            chart._collection = args.NewValue as INotifyCollectionChanged;
            if (chart._collection != null) chart._collection.CollectionChanged += chart.Items_CollectionChanged;
            chart.Render();
        }));

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Render();

    private void Render()
    {
        if (!IsLoaded || ActualWidth < 1 || ActualHeight < 1) return;
        _canvas.Children.Clear();

        var points = (Items as IEnumerable<TrendPoint>)?.ToList() ?? new List<TrendPoint>();
        if (points.Count == 0)
        {
            AddEmptyMessage("此时间范围内暂无趋势数据");
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

        var coordinates = new Point?[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var x = points.Count == 1 ? left + plotWidth / 2 : left + plotWidth * index / (points.Count - 1);
            if (!points[index].IsFuture)
            {
                var y = plotBottom - points[index].Seconds / maximum * (plotHeight - 8);
                coordinates[index] = new Point(x, y);
            }
        }

        var visibleCoordinates = coordinates.Where(point => point.HasValue).Select(point => point!.Value).ToList();
        if (visibleCoordinates.Count > 1 && actualPoints.Any(point => point.Seconds > 0))
        {
            var areaPoints = new PointCollection { new(visibleCoordinates[0].X, plotBottom) };
            foreach (var point in visibleCoordinates) areaPoints.Add(point);
            areaPoints.Add(new Point(visibleCoordinates[^1].X, plotBottom));
            _canvas.Children.Add(new Polygon
            {
                Points = areaPoints,
                Fill = Brush("TaiAccentSoftBrush"),
                Opacity = 0.58
            });
        }

        if (visibleCoordinates.Count > 1)
        {
            var linePoints = new PointCollection();
            foreach (var point in visibleCoordinates) linePoints.Add(point);
            _canvas.Children.Add(new Polyline
            {
                Points = linePoints,
                Stroke = Brush("TaiAccentBrush"),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            });
        }

        if (actualPoints.All(point => point.Seconds <= 0))
            AddEmptyMessage("这个时间范围内还没有使用记录", top + plotHeight / 2 - 12);

        var labelStep = points.Count switch
        {
            <= 12 => 1,
            <= 24 => 3,
            _ => 5
        };

        for (var index = 0; index < points.Count; index++)
        {
            var x = points.Count == 1 ? left + plotWidth / 2 : left + plotWidth * index / (points.Count - 1);
            var coordinate = coordinates[index];
            if (coordinate.HasValue && points[index].Seconds > 0)
            {
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brush("TaiCardBackgroundBrush"),
                    Stroke = Brush("TaiAccentBrush"),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, coordinate.Value.X - 4);
                Canvas.SetTop(dot, coordinate.Value.Y - 4);
                ToolTipService.SetToolTip(dot, $"{points[index].Label}\n{points[index].Duration}");
                _canvas.Children.Add(dot);
            }

            if (index % labelStep != 0 && index != points.Count - 1) continue;
            var label = new TextBlock
            {
                Text = points[index].Label,
                Width = 68,
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Opacity = points[index].IsFuture ? 0.52 : 1,
                Foreground = Brush("TaiSecondaryTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, Math.Clamp(x - 34, left - 34, Math.Max(left - 34, width - 68)));
            Canvas.SetTop(label, plotBottom + 9);
            _canvas.Children.Add(label);
        }
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
        if (seconds < 60 * 60) return $"{Math.Round(seconds / 60):0}分";
        var hours = seconds / 3600;
        return Math.Abs(hours - Math.Round(hours)) < 0.01
            ? $"{hours:0}小时"
            : $"{hours:0.#}小时";
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
