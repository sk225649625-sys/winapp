using System.Windows;
using Microsoft.Web.WebView2.Core;
using ReelForge.Infrastructure;
using ReelForge.Web;

namespace ReelForge;

public partial class MainWindow : Window
{
    private AppPaths _paths = null!;
    private ApiRouter _router = null!;
    private NativeBridge _native = null!;
    private bool _ready;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        _ready = true;

        try
        {
            _paths = new AppPaths();
            _paths.EnsureDirectories();

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _paths.WebViewData,
                options: null);

            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // Local virtual origin only. No TCP/HTTP server is started.
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "reelforge.local",
                _paths.WebRoot,
                CoreWebView2HostResourceAccessKind.Allow);

            _router = new ApiRouter(_paths, environment);
            Browser.CoreWebView2.AddWebResourceRequestedFilter(
                "https://reelforge.local/*",
                CoreWebView2WebResourceContext.All);
            Browser.CoreWebView2.WebResourceRequested += _router.HandleRequest;

            _native = new NativeBridge(Browser.CoreWebView2, _paths);
            _native.Attach();

            Browser.CoreWebView2.Navigate("https://reelforge.local/editor.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ReelForge start nahi ho paya.\n\n{ex.Message}",
                "ReelForge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
