using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Common.WPF;

public sealed class ListViewColumnSorter
{
    private string? _lastSortColumn;
    private ListSortDirection _lastSortDirection;

    public void HandleHeaderClick(RoutedEventArgs e, ListView listView)
    {
        if (e.OriginalSource is not GridViewColumnHeader header)
            return;

        if (header.Tag is not string sortBy)
            return;

        var direction = _lastSortColumn == sortBy && _lastSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        var dataView = CollectionViewSource.GetDefaultView(listView.ItemsSource);

        if (dataView == null)
            return;

        dataView.SortDescriptions.Clear();
        dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
        dataView.Refresh();

        _lastSortColumn = sortBy;
        _lastSortDirection = direction;
    }
}