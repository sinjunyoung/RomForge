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
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void ReleaseNotesBrowser_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
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
                <style>
                    body {
                        background: #252526;
                        color: #DDDDDD;
                        font-family: "Segoe UI Emoji", "Segoe UI Symbol", "Apple Color Emoji", "Segoe UI", sans-serif;
                        font-size: 20px;
                        line-height: 1.6;
                        margin: 14px;
                        overflow-wrap: break-word;
                    }
                    p, li, span, div, ul, ol, strong, em {
                        font-family: inherit;
                        font-size: 20px !important;
                        color: #DDDDDD !important;
                    }
                    strong, b {
                        font-weight: 600 !important;
                        color: #FFFFFF !important;
                    }
                    h1, h2, h3, h4 {
                        color: #FFFFFF !important;
                        border-bottom: 1px solid #444444;
                        padding-bottom: 6px;
                        font-size: 24px !important;
                        font-weight: 600;
                        margin-top: 18px;
                        margin-bottom: 10px;
                    }
                    a { color: #4DA6FF; text-decoration: none; }
                    a:hover { text-decoration: underline; }
                    code {
                        background: #333337;
                        padding: 3px 6px;
                        border-radius: 4px;
                        font-family: Consolas, monospace;
                        font-size: 18px;
                    }
                    pre {
                        background: #1E1E1E;
                        padding: 14px;
                        border-radius: 6px;
                        overflow-x: auto;
                    }
                    pre code { padding: 0; background: transparent; }
                    ul, ol { padding-left: 26px; margin: 10px 0; }
                    li { margin-bottom: 8px; }
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