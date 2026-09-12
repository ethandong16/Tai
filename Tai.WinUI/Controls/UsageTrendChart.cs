using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Tai.WinUI.Services;
using Windows.Foundation;
using System.Collections.Specialized;

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
            _canvas.Children.Add(new TextBlock
            {
                Text = "此时间范围内暂无趋势数据",
                Foreground = Brush("TaiSecondaryTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        const double left = 10;
        const double right = 10;
        const double top = 12;
        const double labelHeight = 34;
        var plotBottom = Math.Max(top + 20, height - labelHeight);
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, plotBottom - top);

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
        }

        var max = Math.Max(1, points.Max(item => item.Seconds));
        var coordinates = new PointCollection();
        for (var index = 0; index < points.Count; index++)
        {
            var x = points.Count == 1 ? left + plotWidth / 2 : left + plotWidth * index / (points.Count - 1);
            var y = plotBottom - points[index].Seconds / max * (plotHeight - 8);
            coordinates.Add(new Point(x, y));
        }

        var areaPoints = new PointCollection { new(left, plotBottom) };
        foreach (var point in coordinates) areaPoints.Add(point);
        areaPoints.Add(new Point(width - right, plotBottom));
        _canvas.Children.Add(new Polygon
        {
            Points = areaPoints,
            Fill = Brush("TaiAccentSoftBrush"),
            Opacity = 0.72
        });
        _canvas.Children.Add(new Polyline
        {
            Points = coordinates,
            Stroke = Brush("TaiAccentBrush"),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        });

        var labelStep = points.Count switch
        {
            <= 12 => 1,
            <= 24 => 3,
            _ => 5
        };
        for (var index = 0; index < points.Count; index++)
        {
            var coordinate = coordinates[index];
            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brush("TaiCardBackgroundBrush"),
                Stroke = Brush("TaiAccentBrush"),
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, coordinate.X - 5);
            Canvas.SetTop(dot, coordinate.Y - 5);
            ToolTipService.SetToolTip(dot, $"{points[index].Label}\n{points[index].Duration}");
            _canvas.Children.Add(dot);

            if (index % labelStep != 0 && index != points.Count - 1) continue;
            var label = new TextBlock
            {
                Text = points[index].Label,
                Width = 68,
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Foreground = Brush("TaiSecondaryTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, Math.Clamp(coordinate.X - 34, 0, Math.Max(0, width - 68)));
            Canvas.SetTop(label, plotBottom + 9);
            _canvas.Children.Add(label);
        }
    }

    private static Brush Brush(string key) =>
        (Brush)Application.Current.Resources[key];
}
