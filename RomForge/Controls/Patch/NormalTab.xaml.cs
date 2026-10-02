using LibHac.Ncm;
using Microsoft.Win32;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services.Patch;
using RomForge.Core.UI.Helpers;
using RomForge.ViewModels;
using RomForge.ViewModels.Patch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace RomForge.Controls.Patch;

public partial class NormalTab : UserControl
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private static class PatchExtensions
    {
        public static readonly string[] AllowedExtensions = [".ips", ".bps", ".ups", ".ppf", ".aps", ".xdelta", ".dcp"];
        public static string FileFilter => $"패치 파일|{string.Join(";", AllowedExtensions.Select(ext => "*" + ext))}|모든 파일|*.*";
    }

    public NormalTab()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (ViewModel != null)
            {
                ViewModel.PatchVM.NormalVM.RequestSourceSelectionAsync = ShowSourceSelectionDialogAsync;
                ViewModel.PatchVM.NormalVM.SourceHash.RequestGdRomLayoutAsync = ShowGdRomLayoutDialogAsync;
            }
        };
    }

    private Task<GdRomHashLayout> ShowGdRomLayoutDialogAsync()
    {
        var tcs = new TaskCompletionSource<GdRomHashLayout>();

        Dispatcher.Invoke(() =>
        {
            var bgBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            var textBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
            var headerBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
            var subBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

            var window = new Window
            {
                Title = "해시 계산 기준 선택",
                Width = 520,
                SizeToContent = SizeToContent.Height,
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = bgBrush,
                FontFamily = new FontFamily("Segoe UI")
            };

            window.SourceInitialized += (_, _) =>
            {
                IntPtr hWnd = new WindowInteropHelper(window).Handle;
                int value = 1;

                _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
            };

            var header = new TextBlock
            {
                Text = "드림캐스트(GD-ROM) CHD입니다. 해시를 계산할 기준을 선택하세요.",
                Margin = new Thickness(16, 16, 16, 8),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                Foreground = headerBrush
            };

            var description = new TextBlock
            {
                Text = "CHD에는 원본이 cue/bin이었는지 gdi였는지 기록되지 않습니다.\n\n"
                     + "• GDI 기준: 패치가 실제로 사용하는 파일 (TOSEC 배치)\n"
                     + "• CUE 기준: Redump cue/bin 원본과 비교할 때 (pregap을 원래 위치로 복원)",
                Margin = new Thickness(16, 0, 16, 16),
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush
            };

            var hint = new TextBlock
            {
                Text = "창을 닫으면 GDI 기준으로 계산합니다.",
                Margin = new Thickness(16, 0, 16, 12),
                Foreground = subBrush,
                FontSize = 11
            };

            var chosen = GdRomHashLayout.Gdi;

            var gdiButton = new Button
            {
                Content = "GDI 기준",
                Width = 100,
                Margin = new Thickness(4, 0, 0, 0),
                IsDefault = true,
                Style = (Style)Application.Current!.FindResource("RunButton")
            };
            var cueButton = new Button
            {
                Content = "CUE 기준",
                Width = 100,
                Margin = new Thickness(4, 0, 0, 0),
                Style = (Style)Application.Current!.FindResource("GrayButton")
            };

            gdiButton.Click += (_, _) =>
            {
                chosen = GdRomHashLayout.Gdi;
                window.DialogResult = true;
            };

            cueButton.Click += (_, _) =>
            {
                chosen = GdRomHashLayout.Cue;
                window.DialogResult = true;
            };

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };
            buttonsPanel.Children.Add(gdiButton);
            buttonsPanel.Children.Add(cueButton);

            var root = new StackPanel();
            root.Children.Add(header);
            root.Children.Add(description);
            root.Children.Add(hint);
            root.Children.Add(buttonsPanel);

            window.Content = root;

            window.ShowDialog();

            tcs.SetResult(chosen);
        });

        return tcs.Task;
    }

    private Task<string?> ShowSourceSelectionDialogAsync(IReadOnlyList<ArchiveCandidate> candidates)
    {
        var tcs = new TaskCompletionSource<string?>();

        Dispatcher.Invoke(() =>
        {
            var bgBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            var listBoxBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
            var borderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
            var textBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
            var headerBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
            var hoverBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
            var selectedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));

            var window = new Window
            {
                Title = "원본 롬 파일 선택",
                Width = 480,
                Height = 380,
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = bgBrush,
                FontFamily = new FontFamily("Segoe UI")
            };

            window.SourceInitialized += (_, _) =>
            {
                IntPtr hWnd = new WindowInteropHelper(window).Handle;
                int value = 1;

                _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
            };

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, textBrush));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 2)));

            var itemTemplate = new ControlTemplate(typeof(ListBoxItem));
            FrameworkElementFactory itemBorder = new(typeof(Border))
            {
                Name = "ItemBorder"
            };
            itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            itemBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var itemContent = new FrameworkElementFactory(typeof(ContentPresenter));
            itemBorder.AppendChild(itemContent);
            itemTemplate.VisualTree = itemBorder;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBrush) { TargetName = "ItemBorder" });
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, selectedBrush) { TargetName = "ItemBorder" });
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemTemplate.Triggers.Add(hoverTrigger);
            itemTemplate.Triggers.Add(selectedTrigger);

            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));

            var listBox = new ListBox
            {
                Margin = new Thickness(12, 8, 12, 8),
                Background = listBoxBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                ItemContainerStyle = itemStyle
            };

            foreach (var candidate in candidates)
            {
                listBox.Items.Add(new ListBoxItem
                {
                    Content = $"{Path.GetFileName(candidate.EntryKey)}  ({FormatSize(candidate.Size)})",
                    Tag = candidate.EntryKey
                });
            }

            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;

            var header = new TextBlock
            {
                Text = "압축 안에 패치 대상 후보가 여러 개입니다. 패치할 원본 파일을 선택하세요:",
                Margin = new Thickness(12, 12, 12, 0),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                Foreground = headerBrush
            };

            var okButton = new Button
            {
                Content = "선택",
                Width = 80,
                Margin = new Thickness(4, 0, 0, 0),
                IsDefault = true,
                Style = (Style)Application.Current!.FindResource("RunButton")
            };
            var cancelButton = new Button
            {
                Content = "취소",
                Width = 80,
                Margin = new Thickness(4, 0, 0, 0),
                IsCancel = true,
                Style = (Style)Application.Current!.FindResource("GrayButton")
            };

            string? selectedPath = null;

            okButton.Click += (_, _) =>
            {
                selectedPath = (listBox.SelectedItem as ListBoxItem)?.Tag as string;
                window.DialogResult = selectedPath is not null;
            };

            cancelButton.Click += (_, _) => window.DialogResult = false;

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(12, 0, 12, 12)
            };
            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);

            var root = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(buttonsPanel, Dock.Bottom);
            root.Children.Add(header);
            root.Children.Add(buttonsPanel);
            root.Children.Add(listBox);

            window.Content = root;

            bool? result = window.ShowDialog();

            tcs.SetResult(result == true ? selectedPath : null);
        });

        return tcs.Task;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }

    private void NormalSourceDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "원본 파일 선택" };

        if (dlg.ShowDialog() == true)
            ViewModel.PatchVM.NormalVM.SourcePath = dlg.FileName;
    }

    private void NormalSourceDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var patchFiles = files
                .Where(f => PatchExtensions.AllowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var sourceFiles = files.Except(patchFiles).ToList();

            if (patchFiles.Count > 0)
                ViewModel.PatchVM.NormalVM.PatchSlots[0].FilePath = patchFiles[0];

            if (sourceFiles.Count > 0)
                ViewModel.PatchVM.NormalVM.SourcePath = sourceFiles[0];
        }
    }

    private void NormalPatchDrop_Click(object sender, MouseButtonEventArgs e)
    {
        var slot = GetPatchSlot(sender);

        var dlg = new OpenFileDialog
        {
            Title = "패치 파일 선택",
            Filter = PatchExtensions.FileFilter
        };

        if (dlg.ShowDialog() == true)
            slot.FilePath = dlg.FileName;
    }

    private void NormalPatchDrop_Drop(object sender, DragEventArgs e)
    {
        var slot = GetPatchSlot(sender);

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var patchFiles = files
                .Where(f => PatchExtensions.AllowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var sourceFiles = files.Except(patchFiles).ToList();

            if (patchFiles.Count > 0)
                slot.FilePath = patchFiles[0];

            if (sourceFiles.Count > 0)
                ViewModel.PatchVM.NormalVM.SourcePath = sourceFiles[0];
        }
    }

    private PatchSlotViewModel GetPatchSlot(object sender)
    {
        if (sender is FrameworkElement { DataContext: PatchSlotViewModel slot })
            return slot;

        return ViewModel.PatchVM.NormalVM.PatchSlots[0];
    }

    private void AddPatch_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PatchVM.NormalVM.AddPatchSlot();
    }

    private void ResetNamingFormat_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PatchVM.NormalVM.NamingFormat = PatchVersionInfoExtractor.DefaultNamingFormat;
    }
}