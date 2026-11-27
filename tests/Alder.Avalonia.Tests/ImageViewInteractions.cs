using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Alder.Avalonia.Tests;

public sealed class ImageViewInteractions
{
    [AvaloniaFact]
    public void CollectionNavigationRetainsSelectionAndUpdatesFromTheSelectedItem()
    {
        var first = Photo("First", Brushes.Orange);
        var second = Photo("Second", Brushes.Pink);
        var items = new ObservableCollection<AlderImageViewItem> { first, second };
        var view = new AlderImageView { ItemsSource = items };
        var window = Show(view);
        try
        {
            Assert.Same(first, view.SelectedItem);
            var viewport = Part<AlderImageViewport>(view, "PART_Viewport");
            viewport.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump(window);
            Assert.Same(second, view.SelectedItem);
            Assert.True(viewport.IsFit);
            items.Insert(0, Photo("New first item", Brushes.Cyan));
            Pump(window);
            Assert.Equal(2, view.SelectedIndex);
            Assert.Same(second, view.SelectedItem);

            second.Title = "Renamed";
            second.Source = null;
            Pump(window);
            Assert.Equal("Renamed", Part<TextBlock>(view, "PART_Title").Text);
            Assert.True(Part<TextBlock>(view, "PART_Empty").IsVisible);
            Assert.True(Part<Control>(view, "PART_Strip").IsVisible);
            second.Source = first.Source;
            Pump(window);
            Assert.False(Part<TextBlock>(view, "PART_Empty").IsVisible);
            view.ShowThumbnails = false;
            Assert.False(Part<Control>(view, "PART_Strip").IsVisible);
            view.ShowThumbnails = true;
            Pump(window);
            Assert.Equal(3, Part<ItemsControl>(view, "PART_Thumbnails").GetRealizedContainers().Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThumbnailStripKeepsVisibleButtonsAfterAppendAndRevealsAnOffscreenSelection()
    {
        var items = new ObservableCollection<AlderImageViewItem>(
            Enumerable.Range(0, 200).Select(i => Photo($"Photo {i}", Brushes.Pink)));
        var view = new AlderImageView { ItemsSource = items };
        var window = Show(view);
        try
        {
            var strip = Part<ItemsControl>(view, "PART_Thumbnails");
            var first = Assert.IsAssignableFrom<Button>(strip.ContainerFromIndex(0));
            Assert.InRange(strip.GetRealizedContainers().Count(), 1, 12);
            var appended = Photo("Tail", Brushes.Orange);
            items.Add(appended);
            Pump(window);
            Assert.Same(first, strip.ContainerFromIndex(0));
            view.SelectedIndex = items.Count - 1;
            Pump(window);
            var tail = Assert.IsAssignableFrom<Button>(strip.ContainerFromIndex(view.SelectedIndex));
            Assert.Contains("selected", tail.Classes);
            Assert.Same(appended, view.SelectedItem);
            Assert.InRange(strip.GetRealizedContainers().Count(), 1, 12);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InlineZoomUsesPointerAnchorAndSupportsCapturedDragAndFit()
    {
        var view = new AlderImageView { Source = Photo("Photo", Brushes.Pink).Source, ViewportHeight = 270 };
        var window = Show(view);
        try
        {
            var viewport = Part<AlderImageViewport>(view, "PART_Viewport");
            var local = viewport.ImageBounds.Center + new Vector(35, 12);
            var cursor = viewport.TranslatePoint(local, window)!.Value;
            var before = viewport.ImageBounds;
            window.MouseWheel(cursor, new Vector(0, 9));
            Pump(window);
            var after = viewport.ImageBounds;
            Assert.True(after.Width > viewport.Bounds.Width);
            Assert.InRange(Math.Abs((local.X - before.X) / before.Width - (local.X - after.X) / after.Width), 0, .001);

            window.MouseDown(cursor, MouseButton.Left);
            window.MouseMove(cursor + new Vector(44, 30), RawInputModifiers.LeftMouseButton);
            window.MouseUp(cursor + new Vector(44, 30), MouseButton.Left);
            Pump(window);
            Assert.True(viewport.ImageBounds.X > after.X + 25);
            Assert.True(viewport.ImageBounds.Y > after.Y + 15);
            window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
            Assert.Equal(1, view.Zoom);
            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
            Assert.True(viewport.IsFit);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ImmersiveReparentsTheViewportRestoresFocusAndCleansUpOnDetach()
    {
        var view = new AlderImageView { Items = { Photo("One", Brushes.Orange), Photo("Two", Brushes.Green) } };
        var host = new Border { Child = view };
        var window = Show(host);
        try
        {
            var viewport = Part<AlderImageViewport>(view, "PART_Viewport");
            var button = Part<Button>(view, "PART_Open");
            var inlineHeight = view.Bounds.Height;
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
            Pump(window);
            Assert.True(view.IsImmersive);
            var overlay = Part<ThemeVariantScope>(window, "AlderImageViewOverlay");
            Assert.Same(viewport, Part<AlderImageViewport>(overlay, "PART_Viewport"));
            Assert.True(Part<Button>(overlay, "PART_Close").IsFocused);
            Assert.Equal(inlineHeight, view.Bounds.Height);
            Assert.True(AlderMotion.GetReduced(viewport));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Assert.Equal(1, view.SelectedIndex);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump(window);
            Assert.False(view.IsImmersive);
            Assert.True(button.IsFocused);
            Assert.Same(viewport, Part<AlderImageViewport>(view, "PART_Viewport"));
            Assert.True(view.TryOpenImmersive());
            host.Child = null;
            Pump(window);
            Assert.False(view.IsImmersive);
            Assert.DoesNotContain(window.GetVisualDescendants(), visual => visual.Name == "AlderImageViewOverlay");
        }
        finally { window.Close(); }
    }

    private static AlderImageViewItem Photo(string title, IBrush fill) => new()
    {
        Title = title,
        Source = new DrawingImage
        {
            Drawing = new GeometryDrawing { Brush = fill, Geometry = new RectangleGeometry(new Rect(0, 0, 640, 480)) }
        }
    };

    private static Window Show(Control content)
    {
        var window = new AlderTestWindow { Width = 570, Height = 580, Content = content };
        AlderMotion.SetReduced(window, true);
        window.Show();
        Pump(window);
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
