using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PZServerLauncher.App.ViewModels;

namespace PZServerLauncher.App.Views;

public partial class SandboxWorkspaceView : UserControl
{
    public SandboxWorkspaceView()
    {
        InitializeComponent();
    }

    private void OnOpenCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SandboxWorkspaceViewModel viewModel ||
            sender is not Control control ||
            control.DataContext is not SandboxCategoryViewModel category)
        {
            return;
        }

        viewModel.SelectCategoryCommand.Execute(category);
        Dispatcher.UIThread.Post(() =>
        {
            this.FindControl<Border>("SelectedCategoryAnchor")?.BringIntoView();
        }, DispatcherPriority.Background);
    }

    private void OnPreviousCategoriesClick(object? sender, RoutedEventArgs e) =>
        ScrollCategories(-1);

    private void OnNextCategoriesClick(object? sender, RoutedEventArgs e) =>
        ScrollCategories(1);

    private void OnCategoryScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            UpdateCategoryArrowState(scrollViewer);
        }
    }

    private void ScrollCategories(int direction)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("CategoryScrollViewer");
        if (scrollViewer is null)
        {
            return;
        }

        var maximumOffset = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var pageSize = Math.Max(158, scrollViewer.Viewport.Width * 0.75);
        var targetOffset = Math.Clamp(scrollViewer.Offset.X + direction * pageSize, 0, maximumOffset);
        scrollViewer.Offset = new Vector(targetOffset, scrollViewer.Offset.Y);
        UpdateCategoryArrowState(scrollViewer);
    }

    private void UpdateCategoryArrowState(ScrollViewer scrollViewer)
    {
        var maximumOffset = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var previousButton = this.FindControl<Button>("PreviousCategoriesButton");
        var nextButton = this.FindControl<Button>("NextCategoriesButton");
        if (previousButton is not null)
        {
            previousButton.IsEnabled = scrollViewer.Offset.X > 0.5;
        }

        if (nextButton is not null)
        {
            nextButton.IsEnabled = scrollViewer.Offset.X < maximumOffset - 0.5;
        }
    }
}
