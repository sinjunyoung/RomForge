
using Common;
using Markdig;
using RomForge.Core.Models;
using System.Windows;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class UpdateWindow : Window
{
    private readonly GitHubRelease _release;

    public UpdateWindow(GitHubRelease release)
    {
        InitializeComponent();
        _release = release;
        DataContext = release;
        Loaded += UpdateWindow_Loaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ReleaseNotesBrowser.EnsureCoreWebView2Async();

            ReleaseNotesBrowser.CoreWebView2.Settings.IsScriptEnabled = false;

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var markdown = Markdown.ToHtml(_release.Body ?? "업데이트 내용이 없습니다.", pipeline);
            ReleaseNotesBrowser.NavigateToString(CreateHtml(markdown));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"업데이트 내용을 표시할 수 없습니다.\n\n오류 내용: {ex.Message}");
        }
    }

    private static string CreateHtml(string markdown)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <style>
                    body {
                        background: #252526;
                        color: #DDDDDD;
                        font-family: "Segoe UI", sans-serif;
                        font-size: 14px;
                        line-height: 1.6;
                        margin: 12px;
                        overflow-wrap: break-word;
                    }
                    h1, h2, h3, h4 {
                        color: #FFFFFF;
                        border-bottom: 1px solid #444444;
                        padding-bottom: 6px;
                    }
                    a {
                        color: #4DA6FF;
                        text-decoration: none;
                    }
                    a:hover {
                        text-decoration: underline;
                    }
                    code {
                        background: #333337;
                        padding: 2px 5px;
                        border-radius: 4px;
                        font-family: Consolas, monospace;
                    }
                    pre {
                        background: #1E1E1E;
                        padding: 12px;
                        border-radius: 6px;
                        overflow-x: auto;
                    }
                    pre code {
                        padding: 0;
                        background: transparent;
                    }
                    blockquote {
                        border-left: 3px solid #007ACC;
                        margin-left: 0;
                        padding-left: 12px;
                        color: #AAAAAA;
                    }
                    table {
                        border-collapse: collapse;
                        width: 100%;
                    }
                    th, td {
                        border: 1px solid #444444;
                        padding: 8px;
                    }
                    th {
                        background: #333337;
                    }
                    hr {
                        border: 0;
                        border-top: 1px solid #444444;
                        margin: 20px 0;
                    }
                    img {
                        max-width: 100%;
                        height: auto;
                    }
                    details {
                        background: #303034;
                        border: 1px solid #444444;
                        border-radius: 6px;
                        padding: 10px 12px;
                        margin: 10px 0;
                    }
                    summary {
                        cursor: pointer;
                        font-weight: 600;
                        color: #FFFFFF;
                    }
                </style>
            </head>
            <body>
                {{markdown}}
            </body>
            </html>
            """;
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}