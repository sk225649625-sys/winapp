using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ReelForge.Infrastructure;
using ReelForge.Models;
using ReelForge.Services;

namespace ReelForge;

public partial class MainWindow : Window
{
    private readonly AppPaths _paths = new();
    private readonly ProjectStore _projects;
    private readonly RenderService _render;
    private readonly ObservableCollection<MediaItem> _media = new();
    private readonly ObservableCollection<TimelineItem> _timeline = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private ProjectModel _project = new();
    private bool _updatingSeek;
    private bool _isPlaying;
    private readonly string _projectName = "last.json";

    public MainWindow()
    {
        InitializeComponent();
        _paths.Ensure();
        _projects = new ProjectStore(_paths);
        _render = new RenderService(_paths);

        MediaList.ItemsSource = _media;
        TimelineList.ItemsSource = _timeline;

        _project = _projects.Load(_projectName);
        ApplyProjectToUi();

        _clock.Tick += Clock_Tick;
        _clock.Start();

        PreviewVideo.MediaOpened += PreviewVideo_MediaOpened;
        PreviewVideo.MediaEnded += (_, _) => PreviewVideo.Position = TimeSpan.Zero;
        PreviewVideo.MediaFailed += (_, e) => SetStatus("Preview failed: " + (e.ErrorException?.Message ?? "unknown"));
    }

    private void ApplyProjectToUi()
    {
        CmbRatio.SelectedIndex = _project.Ratio switch
        {
            "9:16" => 1,
            "1:1" => 2,
            "4:5" => 3,
            _ => 0
        };
        CmbQuality.SelectedIndex = _project.Quality == 720 ? 1 : _project.Quality == 480 ? 2 : 0;
        CmbFps.SelectedIndex = _project.Fps == 24 ? 1 : _project.Fps == 60 ? 2 : 0;

        _timeline.Clear();
        foreach (var clip in _project.Clips)
            _timeline.Add(clip);

        RefreshTimelineInfo();
    }

    private void SaveProjectFromUi()
    {
        _project.Name = _projectName;
        _project.Ratio = (CmbRatio.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "16:9";
        _project.Quality = ParseQuality();
        _project.Fps = int.TryParse((CmbFps.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var fps) ? fps : 30;
        _project.Clips = _timeline.ToList();
        _projects.Save(_project);
    }

    private int ParseQuality()
    {
        var s = (CmbQuality.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1080p";
        return int.TryParse(new string(s.TakeWhile(char.IsDigit).ToArray()), out var q) ? q : 1080;
    }

    private void BtnAddMixed_Click(object sender, RoutedEventArgs e) => AddMediaFiles("all");
    private void BtnAddVideo_Click(object sender, RoutedEventArgs e) => AddMediaFiles("video");
    private void BtnAddMusic_Click(object sender, RoutedEventArgs e) => AddMediaFiles("music");
    private void BtnAddSfx_Click(object sender, RoutedEventArgs e) => AddMediaFiles("sfx");
    private void BtnAddVoice_Click(object sender, RoutedEventArgs e) => AddMediaFiles("voice");

    private void AddMediaFiles(string mode)
    {
        var filter = mode switch
        {
            "video" => "Media|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.wmv;*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            "music" => "Audio|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma",
            "sfx" => "Sound effects|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac",
            "voice" => "Voice|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.m4v",
            _ => "All media|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.wmv;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma"
        };

        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = filter,
            Title = "ReelForge — local files choose karo"
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var file in dlg.FileNames)
        {
            try
            {
                var item = CreateMediaItem(file, mode);
                _media.Add(item);
            }
            catch (Exception ex)
            {
                SetStatus($"Add fail: {Path.GetFileName(file)} — {ex.Message}");
            }
        }

        SetStatus($"{dlg.FileNames.Length} local file(s) added");
    }

    private MediaItem CreateMediaItem(string file, string mode)
    {
        var fi = new FileInfo(file);
        if (!fi.Exists) throw new FileNotFoundException("file nahi mila", file);

        var ext = fi.Extension.ToLowerInvariant();
        string type;
        if (new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff" }.Contains(ext))
            type = "image";
        else if (new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac", ".wma" }.Contains(ext))
            type = "audio";
        else
            type = "video";

        return new MediaItem
        {
            Name = fi.Name,
            FullPath = fi.FullName,
            Type = type,
            Kind = type == "audio" ? mode : type,
            Size = fi.Length,
            DurationSeconds = type == "image" ? 5 : 0
        };
    }

    private void MediaList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MediaList.SelectedItem is MediaItem media)
            AddToTimeline(media);
    }

    private void MediaAddTimeline_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is MediaItem media)
            AddToTimeline(media);
    }

    private void BtnAddSelectedToTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (MediaList.SelectedItem is MediaItem media)
            AddToTimeline(media);
    }

    private void AddToTimeline(MediaItem media)
    {
        if (media.Type == "audio")
        {
            _project.Audio = _project.Audio.Append(new TimelineItem
            {
                Name = media.Name,
                FullPath = media.FullPath,
                Type = media.Type,
                Kind = media.Kind,
                DurationSeconds = media.DurationSeconds
            }).ToList();

            SetStatus($"{media.Name} audio track me add hua");
            SaveProjectFromUi();
            return;
        }

        _timeline.Add(new TimelineItem
        {
            Name = media.Name,
            FullPath = media.FullPath,
            Type = media.Type,
            Kind = media.Kind,
            DurationSeconds = media.DurationSeconds <= 0 ? 5 : media.DurationSeconds
        });

        RefreshTimelineInfo();
        SaveProjectFromUi();
        SetStatus($"{media.Name} timeline me add hua");
    }

    private void TimelineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimelineList.SelectedItem is TimelineItem item)
            ShowPreview(item);
        else
            ClearPreview();
    }

    private void ShowPreview(TimelineItem item)
    {
        InspectorName.Text = item.Name;
        DurationSlider.Value = Math.Clamp(item.DurationSeconds, 1, 30);
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewVideo.Visibility = Visibility.Collapsed;
        PreviewEmpty.Visibility = Visibility.Collapsed;
        PreviewVideo.Stop();

        if (!File.Exists(item.FullPath))
        {
            PreviewEmpty.Visibility = Visibility.Visible;
            PreviewEmpty.Text = "File mil nahi rahi:\n" + item.FullPath;
            SetStatus("Missing file: " + item.FullPath);
            return;
        }

        if (item.Type.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            PreviewImage.Source = new BitmapImage(new Uri(item.FullPath));
            PreviewImage.Visibility = Visibility.Visible;
            PreviewSeek.Maximum = Math.Max(1, item.DurationSeconds);
            PreviewSeek.Value = 0;
        }
        else if (item.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
        {
            PreviewVideo.Source = new Uri(item.FullPath);
            PreviewVideo.Visibility = Visibility.Visible;
            PreviewVideo.Play();
            _isPlaying = true;
        }
    }

    private void ClearPreview()
    {
        PreviewVideo.Stop();
        _isPlaying = false;
        PreviewVideo.Source = null;
        PreviewImage.Source = null;
        PreviewVideo.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewEmpty.Visibility = Visibility.Visible;
        PreviewEmpty.Text = "Timeline khaali hai";
        InspectorName.Text = "kuch select nahi";
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Position = PreviewVideo.Position - TimeSpan.FromSeconds(2);
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (PreviewVideo.Visibility == Visibility.Visible)
        {
            if (_isPlaying)
            {
                PreviewVideo.Pause();
                _isPlaying = false;
            }
            else
            {
                if (PreviewVideo.NaturalDuration.HasTimeSpan &&
                    PreviewVideo.Position >= PreviewVideo.NaturalDuration.TimeSpan)
                {
                    PreviewVideo.Position = TimeSpan.Zero;
                }

                PreviewVideo.Play();
                _isPlaying = true;
            }
        }
    }

    private void BtnResetPreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Stop();
        _isPlaying = false;
        PreviewVideo.Position = TimeSpan.Zero;
        PreviewSeek.Value = 0;
    }

    private void PreviewVideo_MediaOpened(object? sender, RoutedEventArgs e)
    {
        if (PreviewVideo.NaturalDuration.HasTimeSpan)
            PreviewSeek.Maximum = Math.Max(1, PreviewVideo.NaturalDuration.TimeSpan.TotalSeconds);
    }

    private void Clock_Tick(object? sender, EventArgs e)
    {
        if (PreviewVideo.Visibility != Visibility.Visible) return;
        _updatingSeek = true;
        PreviewSeek.Value = Math.Min(PreviewSeek.Maximum, PreviewVideo.Position.TotalSeconds);
        _updatingSeek = false;

        var total = PreviewVideo.NaturalDuration.HasTimeSpan ? PreviewVideo.NaturalDuration.TimeSpan : TimeSpan.Zero;
        TimeText.Text = $"{PreviewVideo.Position:mm\\:ss} / {total:mm\\:ss}";
    }

    private void PreviewSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSeek || PreviewVideo.Visibility != Visibility.Visible) return;
        try { PreviewVideo.Position = TimeSpan.FromSeconds(e.NewValue); } catch { }
    }

    private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TimelineList.SelectedItem is TimelineItem item)
        {
            item.DurationSeconds = Math.Round(e.NewValue, 1);
            SaveProjectFromUi();
        }
    }

    private void Settings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            SaveProjectFromUi();
    }

    private void BtnRemoveClip_Click(object sender, RoutedEventArgs e)
    {
        if (TimelineList.SelectedItem is TimelineItem item)
        {
            _timeline.Remove(item);
            RefreshTimelineInfo();
            SaveProjectFromUi();
            ClearPreview();
        }
    }

    private void BtnAddText_Click(object sender, RoutedEventArgs e)
        => SetStatus("Native text overlay UI ready; render overlay pipeline can be extended in RenderService.");

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_timeline.Count == 0)
        {
            MessageBox.Show(this, "Timeline khaali hai.", "ReelForge");
            return;
        }

        SaveProjectFromUi();
        BtnExport.IsEnabled = false;
        SetStatus("Native C++ render chal raha hai…");

        try
        {
            var output = await _render.RenderAsync(_project, CancellationToken.None);
            SetStatus("Export complete");
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{output}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus("Export failed");
            MessageBox.Show(this, ex.Message, "ReelForge export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExport.IsEnabled = true;
        }
    }

    private void BtnSaveProject_Click(object sender, RoutedEventArgs e)
    {
        SaveProjectFromUi();
        SetStatus("Project saved");
    }

    private void BtnLoadProject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "ReelForge projects|*.json",
            InitialDirectory = _paths.Projects
        };

        if (dlg.ShowDialog() != true) return;

        _project = _projects.Load(Path.GetFileName(dlg.FileName));
        ApplyProjectToUi();
        SetStatus("Project loaded");
    }

    private void BtnOpenExportFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_paths.Exports}\"")
        {
            UseShellExecute = true
        });
    }

    private void Window_DragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
        {
            try { _media.Add(CreateMediaItem(file, "all")); }
            catch { }
        }
        SetStatus($"{files.Length} file(s) local library me added");
    }

    private void Timeline_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        foreach (var file in (string[])e.Data.GetData(DataFormats.FileDrop))
        {
            try
            {
                var media = CreateMediaItem(file, "all");
                _media.Add(media);
                if (media.Type != "audio") AddToTimeline(media);
            }
            catch { }
        }
    }

    private void RefreshTimelineInfo()
        => TimelineInfo.Text = $"{_timeline.Count} clips";

    private void SetStatus(string text)
        => StatusText.Text = text;
}
