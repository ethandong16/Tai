using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Tai.WinUI.ViewModels;
using Windows.Foundation;

namespace Tai.WinUI.Controls;

/// <summary>A native, non-animated ring with an accessible summary of the same category data.</summary>
public sealed class CategoryRingChart : UserControl
{
    private readonly Grid _root = new();
    private readonly Canvas _arcs = new();
    private readonly TextBlock _value = new() { FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _label = new() { FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 90 };
    private INotifyCollectionChanged? _collection;

    public CategoryRingChart()
    {
        Content = _root;
        _root.Children.Add(_arcs);
        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 3 };
        center.Children.Add(_value);
        center.Children.Add(_label);
        _root.Children.Add(center);
        _value.Foreground = Resource("TaiPrimaryTextBrush");
        _label.Foreground = Resource("TaiSecondaryTextBrush");
        Loaded += (_, _) => { Subscribe(); Render(); };
        Unloaded += (_, _) => Unsubscribe();
        SizeChanged += (_, _) => Render();
    }

    public object? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(object), typeof(CategoryRingChart),
        new PropertyMetadata(null, (sender, _) => { var chart = (CategoryRingChart)sender; chart.Subscribe(); chart.Render(); }));

    private void Subscribe()
    {
        Unsubscribe();
        if (!IsLoaded) return;
        _collection = Items as INotifyCollectionChanged;
        if (_collection != null) _collection.CollectionChanged += Changed;
    }

    private void Unsubscribe()
    {
        if (_collection != null) _collection.CollectionChanged -= Changed;
        _collection = null;
    }

    private void Changed(object? sender, NotifyCollectionChangedEventArgs e) => Render();

    private void Render()
    {
        if (!IsLoaded || ActualWidth < 1 || ActualHeight < 1) return;
        _arcs.Children.Clear();
        var values = (Items as IEnumerable<CategoryUsage>)?.Where(item => item.Percent > 0).ToList() ?? [];
        var size = Math.Min(ActualWidth, ActualHeight);
        const double thickness = 14;
        var radius = Math.Max(1, (size - thickness) / 2);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var track = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = Resource("TaiCardMutedBrush"), StrokeThickness = thickness };
        Canvas.SetLeft(track, center.X - radius);
        Canvas.SetTop(track, center.Y - radius);
        _arcs.Children.Add(track);
        var total = values.Sum(item => item.Percent);
        var angle = -Math.PI / 2;
        foreach (var item in values)
        {
            var sweep = item.Percent / (double)total * Math.PI * 2;
            if (values.Count == 1)
            {
                track.Stroke = item.Accent;
                break;
            }
            var start = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            angle += sweep;
            var end = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment { Point = end, Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise, IsLargeArc = sweep > Math.PI });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            _arcs.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Data = geometry, Stroke = item.Accent, StrokeThickness = thickness });
        }
        var largest = values.MaxBy(item => item.Percent);
        _value.Text = largest?.PercentText ?? "—";
        _label.Text = largest == null ? Tai.WinUI.Services.L.Text("暂无记录") : Tai.WinUI.Services.L.Text(largest.Name);
        AutomationProperties.SetName(this, values.Count == 0 ? Tai.WinUI.Services.L.Text("分类占比：暂无记录")
            : (Tai.WinUI.Services.L.IsEnglish ? "Category breakdown: " : "分类占比：")
              + string.Join(Tai.WinUI.Services.L.IsEnglish ? ", " : "，", values.Select(item => $"{Tai.WinUI.Services.L.Text(item.Name)} {item.PercentText}")));
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
