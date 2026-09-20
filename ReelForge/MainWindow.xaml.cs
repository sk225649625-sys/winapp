using Microsoft.Web.WebView2.Core;
using System.Windows;
using ReelForge.Infrastructure;
using ReelForge.Web;

namespace ReelForge;

public partial class MainWindow : Window
{
    private AppPaths _paths = null!;
    private ApiRouter _router = null!;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _paths = new AppPaths();
        _paths.EnsureDirectories();

        _router = new ApiRouter(_paths);
        await Browser.EnsureCoreWebView2Async();

        Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
        Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

        Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += _router.HandleRequest;

        Browser.CoreWebView2.Navigate("https://reelforge.local/editor.html");
    }
}
