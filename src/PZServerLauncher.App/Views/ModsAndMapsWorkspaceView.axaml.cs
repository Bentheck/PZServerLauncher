using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using PZServerLauncher.App.ViewModels;

namespace PZServerLauncher.App.Views;

public partial class ModsAndMapsWorkspaceView : UserControl
{
    public ModsAndMapsWorkspaceView()
    {
        InitializeComponent();
    }

    private async void OnModEditorDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not ModsAndMapsWorkspaceViewModel.ModEditorItemViewModel item ||
            !item.IsActive)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var data = new DataTransfer();
        var rowId = item.RowId.ToString(CultureInfo.InvariantCulture);
        data.Add(DataTransferItem.CreateText(rowId));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private void OnModEditorRowDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not ModsAndMapsWorkspaceViewModel.ModEditorItemViewModel item ||
            !item.IsActive)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var dragText = e.DataTransfer.TryGetText();
        if (string.IsNullOrWhiteSpace(dragText))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnModEditorRowDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not ModsAndMapsWorkspaceViewModel.ModEditorItemViewModel targetItem ||
            !targetItem.IsActive ||
            DataContext is not ModsAndMapsWorkspaceViewModel viewModel)
        {
            return;
        }

        var payload = e.DataTransfer.TryGetText();
        if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var draggedRowId))
        {
            return;
        }

        viewModel.TryReorderActiveModRow(draggedRowId, targetItem.RowId);
        e.Handled = true;
    }
}
