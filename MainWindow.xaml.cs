using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Text.Json;

namespace MiniPlayer
{
    public enum LayoutPosition { ButtonsLeft, ButtonsRight, TextTop, TextBottom }

    public class AppSettings
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public bool IsLocked { get; set; }
        public bool IsScrollVolumeEnabled { get; set; } = true;
        public LayoutPosition Layout { get; set; } = LayoutPosition.ButtonsLeft;
        public bool ShowIcon { get; set; } = true;
        public bool ShowThumbnail { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private string? _forcedSourceId; 
        private bool _isLocked = false;
        private bool _isScrollVolumeEnabled = true;
        private bool _showIcon = true;
        private bool _showThumbnail = false;
        private LayoutPosition _currentLayout = LayoutPosition.ButtonsLeft;
        private string? _currentProcessPath = null;
        private string? _currentProcessName = null;
        private DispatcherTimer? _zOrderTimer;
        private DispatcherTimer? _volumeDisplayTimer;
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += Window_SourceInitialized;
        }

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd != IntPtr.Zero) helper.Owner = taskbarHwnd;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            HwndSource.FromHwnd(hwnd).AddHook(HwndMessageHook);
            ForceOnTop();
        }

        private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE) handled = true;
            return IntPtr.Zero;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings(); 
            ApplyLayout();
            UpdateTheme();
            SystemEvents.UserPreferenceChanged += (s, args) => { if (args.Category == UserPreferenceCategory.General) Dispatcher.Invoke(UpdateTheme); };
            await InitializeMediaManager();
            _zOrderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _zOrderTimer.Tick += (s, args) => ForceOnTop();
            _zOrderTimer.Start();
        }

        private void ApplyLayout()
        {
            TrackInfo.MaxWidth = double.PositiveInfinity;
            Col1.Width = GridLength.Auto;
            Col2.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(ControlsPanel, 1);
            Grid.SetRow(ControlsPanel, 0);
            Grid.SetRowSpan(ControlsPanel, 1);
            Grid.SetColumn(TrackInfo, 2);
            Grid.SetRow(TrackInfo, 0);
            Grid.SetColumnSpan(TrackInfo, 1);
            
            ControlsPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            TrackInfo.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            TrackInfo.TextAlignment = TextAlignment.Left;
            TrackInfo.Margin = new Thickness(0);

            IconContainer.Visibility = _showIcon ? Visibility.Visible : Visibility.Collapsed;
            AppIcon.Visibility = (_showIcon && !_showThumbnail) ? Visibility.Visible : Visibility.Collapsed;
            ThumbnailImage.Visibility = (_showIcon && _showThumbnail) ? Visibility.Visible : Visibility.Collapsed;

            switch (_currentLayout)
            {
                case LayoutPosition.ButtonsLeft:
                    Grid.SetColumn(ControlsPanel, 1);
                    Grid.SetColumn(TrackInfo, 2);
                    TrackInfo.Margin = new Thickness(10, 0, 0, 0);
                    break;
                case LayoutPosition.ButtonsRight:
                    TrackInfo.MaxWidth = 150; 
                    Col1.Width = new GridLength(1, GridUnitType.Star);
                    Col2.Width = GridLength.Auto;
                    Grid.SetColumn(TrackInfo, 1);
                    Grid.SetColumn(ControlsPanel, 2);
                    TrackInfo.Margin = new Thickness(0, 0, 10, 0);
                    TrackInfo.TextAlignment = TextAlignment.Right;
                    TrackInfo.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    break;
                case LayoutPosition.TextTop:
                    Grid.SetColumn(TrackInfo, 1);
                    Grid.SetColumnSpan(TrackInfo, 2); 
                    Grid.SetRow(TrackInfo, 0);
                    Grid.SetColumn(ControlsPanel, 1);
                    Grid.SetRow(ControlsPanel, 1);
                    TrackInfo.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    TrackInfo.TextAlignment = TextAlignment.Center;
                    TrackInfo.Margin = new Thickness(0, 0, 0, 4);
                    break;
                case LayoutPosition.TextBottom:
                    Grid.SetColumn(ControlsPanel, 1);
                    Grid.SetRow(ControlsPanel, 0);
                    Grid.SetColumn(TrackInfo, 1);
                    Grid.SetColumnSpan(TrackInfo, 2);
                    Grid.SetRow(TrackInfo, 1);
                    TrackInfo.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    TrackInfo.TextAlignment = TextAlignment.Center;
                    TrackInfo.Margin = new Thickness(0, 4, 0, 0);
                    break;
            }

            this.UpdateLayout();
            if (this.IsLoaded) PositionWindow();
        }

        private void LoadSettings()
        {
            try {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(path)) {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                    if (settings != null) {
                        _currentLayout = settings.Layout;
                        _isLocked = settings.IsLocked;
                        _isScrollVolumeEnabled = settings.IsScrollVolumeEnabled;
                        _showIcon = settings.ShowIcon;
                        _showThumbnail = settings.ShowThumbnail;
                        if (settings.Top != 0) this.Top = settings.Top;
                        if (settings.Left != 0) this.Left = settings.Left;
                    }
                } else PositionWindow();
            } catch { PositionWindow(); }
        }

        private void SaveSettings()
        {
            try {
                var settings = new AppSettings { Top = this.Top, Left = this.Left, IsLocked = _isLocked, IsScrollVolumeEnabled = _isScrollVolumeEnabled, Layout = _currentLayout, ShowIcon = _showIcon, ShowThumbnail = _showThumbnail };
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"), JsonSerializer.Serialize(settings));
            } catch { }
        }

        protected override void OnClosed(EventArgs e) { SaveSettings(); base.OnClosed(e); }

        private void UpdateTheme()
        {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    if (key != null && (int)key.GetValue("SystemUsesLightTheme", 0) == 1) this.Foreground = System.Windows.Media.Brushes.Black;
                    else this.Foreground = System.Windows.Media.Brushes.White;
                }
            } catch { this.Foreground = System.Windows.Media.Brushes.White; }
        }

        private void ForceOnTop() { if (!SourceMenu.IsOpen && new WindowInteropHelper(this).Handle != IntPtr.Zero) SetWindowPos(new WindowInteropHelper(this).Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); }

        private void PositionWindow()
        {
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var workArea = SystemParameters.WorkArea;
            double taskbarHeight = screenHeight - workArea.Height;
            if (taskbarHeight <= 0) taskbarHeight = 48;
            double h = this.ActualHeight > 0 ? this.ActualHeight : 40;
            double w = this.ActualWidth > 0 ? this.ActualWidth : 200;
            if (this.Left <= 0 || this.Left > screenWidth) this.Left = screenWidth - w - 100;
            double targetTop = screenHeight - taskbarHeight + (taskbarHeight - h) / 2;
            if (targetTop + h > screenHeight) targetTop = screenHeight - h;
            this.Top = targetTop;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (!_isLocked && e.ChangedButton == MouseButton.Left) this.DragMove(); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); SaveSettings(); }

        private void TrackInfo_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isScrollVolumeEnabled) return;
            float delta = (e.Delta > 0) ? 0.05f : -0.05f;
            float newVol = AudioHelper.AdjustVolumeRobust(_currentProcessName ?? "", _currentProcessPath ?? "", delta);
            if (newVol >= 0) TrackInfo.Text = $"Volume: {newVol * 100:0}%";
            else TrackInfo.Text = "No active audio stream";
            if (_volumeDisplayTimer == null) { _volumeDisplayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _volumeDisplayTimer.Tick += (s, a) => { _volumeDisplayTimer.Stop(); UpdateUI(); }; }
            _volumeDisplayTimer.Stop(); _volumeDisplayTimer.Start();
            e.Handled = true;
        }

        private async Task InitializeMediaManager()
        {
            try { _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); if (_manager != null) { _manager.SessionsChanged += (s, a) => Dispatcher.Invoke(() => UpdateSession()); UpdateSession(); } } catch { }
        }

        private void ForceSession(GlobalSystemMediaTransportControlsSession session) { _forcedSourceId = session.SourceAppUserModelId; UpdateSession(session); }

        private void UpdateSession(GlobalSystemMediaTransportControlsSession? specificSession = null)
        {
            if (_manager == null) return;
            var target = specificSession ?? (string.IsNullOrEmpty(_forcedSourceId) ? _manager.GetCurrentSession() : _manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == _forcedSourceId)) ?? _manager.GetSessions().FirstOrDefault();
            if (_currentSession?.SourceAppUserModelId != target?.SourceAppUserModelId)
            {
                if (_currentSession != null) { _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged; _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged; }
                _currentSession = target;
                if (_currentSession != null) { _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged; _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged; UpdateAppIcon(_currentSession.SourceAppUserModelId); }
                else { AppIcon.Source = null; ThumbnailImage.Source = null; }
            }
            UpdateUI();
        }

        private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => Dispatcher.Invoke(UpdateUI);
        private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => Dispatcher.Invoke(UpdateUI);

        private void UpdateAppIcon(string sourceId)
        {
            _currentProcessName = null; _currentProcessPath = null;
            try {
                string cleanId = sourceId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(sourceId) : sourceId;
                if (cleanId.Contains(".")) cleanId = cleanId.Split('.').Last();
                var allProcesses = Process.GetProcesses();
                var process = allProcesses.FirstOrDefault(p => 
                {
                    try {
                        if (sourceId.IndexOf(p.ProcessName, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        if (p.ProcessName.IndexOf(cleanId, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    } catch { }
                    return false;
                });

                if (process != null) {
                    _currentProcessName = process.ProcessName;
                    if (process.MainModule != null) { 
                        _currentProcessPath = process.MainModule.FileName; 
                        AppIcon.Source = Imaging.CreateBitmapSourceFromHIcon(System.Drawing.Icon.ExtractAssociatedIcon(_currentProcessPath).Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); 
                    }
                }
            } catch { }
        }

        private async void UpdateUI()
        {
            if (_volumeDisplayTimer?.IsEnabled == true) return;
            if (_currentSession == null) { TrackInfo.Text = "No Media"; BtnPlayPause.Content = "\uE768"; return; }
            var info = _currentSession.GetPlaybackInfo();
            if (info != null) BtnPlayPause.Content = (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ? "\uE769" : "\uE768";
            try {
                var props = await _currentSession.TryGetMediaPropertiesAsync();
                if (props != null) {
                    TrackInfo.Text = string.IsNullOrEmpty(props.Artist) ? (props.Title ?? "Unknown") : $"{props.Title} - {props.Artist}";
                    if (_showThumbnail) await UpdateThumbnail(props.Thumbnail);
                }
            } catch { }
        }

        private async Task UpdateThumbnail(IRandomAccessStreamReference? thumbRef)
        {
            if (thumbRef == null) { ThumbnailImage.Source = null; return; }
            try {
                using (var stream = await thumbRef.OpenReadAsync()) {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream.AsStream();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ThumbnailImage.Source = bitmap;
                }
            } catch { ThumbnailImage.Source = null; }
        }

        private async void Prev_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TrySkipPreviousAsync(); }
        private async void PlayPause_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TryTogglePlayPauseAsync(); }
        private async void Next_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TrySkipNextAsync(); }

        private void SourceMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (_zOrderTimer != null) _zOrderTimer.Stop();
            SourceMenu.Items.Clear();
            if (_manager != null) {
                var allProcesses = Process.GetProcesses();
                foreach (var session in _manager.GetSessions()) {
                    var id = session.SourceAppUserModelId;
                    string displayName = id;
                    try {
                        string searchName = id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(id) : (id.Contains(".") ? id.Split('.').Last() : id);
                        var process = allProcesses.FirstOrDefault(p => p.ProcessName.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (process != null) {
                            try {
                                if (process.MainModule != null) {
                                    var desc = process.MainModule.FileVersionInfo.FileDescription;
                                    displayName = !string.IsNullOrWhiteSpace(desc) ? desc : process.ProcessName;
                                }
                            } catch { displayName = process.ProcessName; }
                        } else { displayName = searchName; }
                    } catch { displayName = id.Split('.').Last().Replace(".exe",""); }

                    if (!string.IsNullOrEmpty(displayName) && char.IsLower(displayName[0])) displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);

                    var item = new MenuItem { Header = displayName, IsCheckable = true, IsChecked = _currentSession?.SourceAppUserModelId == session.SourceAppUserModelId, Tag = session };
                    item.Click += (s, ev) => ForceSession((GlobalSystemMediaTransportControlsSession)((MenuItem)s).Tag);
                    SourceMenu.Items.Add(item);
                }
            }
            SourceMenu.Items.Add(new Separator());
            
            var viewItem = new MenuItem { Header = "View" };
            var iconItem = new MenuItem { Header = "Show Icon", IsCheckable = true, IsChecked = _showIcon };
            iconItem.Click += (s, ev) => { _showIcon = !_showIcon; ApplyLayout(); SaveSettings(); };
            viewItem.Items.Add(iconItem);

            var thumbItem = new MenuItem { Header = "Use Album Art", IsCheckable = true, IsChecked = _showThumbnail, IsEnabled = _showIcon };
            thumbItem.Click += (s, ev) => { _showThumbnail = !_showThumbnail; ApplyLayout(); UpdateUI(); SaveSettings(); };
            viewItem.Items.Add(thumbItem);
            SourceMenu.Items.Add(viewItem);

            var layoutItem = new MenuItem { Header = "Layout" };
            AddLayoutItem(layoutItem, "Buttons: Left", LayoutPosition.ButtonsLeft);
            AddLayoutItem(layoutItem, "Buttons: Right", LayoutPosition.ButtonsRight);
            AddLayoutItem(layoutItem, "Text: Top", LayoutPosition.TextTop);
            AddLayoutItem(layoutItem, "Text: Bottom", LayoutPosition.TextBottom);
            SourceMenu.Items.Add(layoutItem);

            var volItem = new MenuItem { Header = "Volume Scroll", IsCheckable = true, IsChecked = _isScrollVolumeEnabled };
            volItem.Click += (s, ev) => { _isScrollVolumeEnabled = !_isScrollVolumeEnabled; SaveSettings(); };
            SourceMenu.Items.Add(volItem);

            var lockItem = new MenuItem { Header = "Lock Position", IsCheckable = true, IsChecked = _isLocked };
            lockItem.Click += (s, ev) => { _isLocked = !_isLocked; SaveSettings(); };
            SourceMenu.Items.Add(lockItem);

            SourceMenu.Items.Add(new Separator());
            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, ev) => System.Windows.Application.Current.Shutdown();
            SourceMenu.Items.Add(exitItem);
        }
        private void AddLayoutItem(MenuItem parent, string header, LayoutPosition pos) {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = _currentLayout == pos, Tag = pos };
            item.Click += (s, ev) => { _currentLayout = (LayoutPosition)((MenuItem)s).Tag; ApplyLayout(); SaveSettings(); };
            parent.Items.Add(item);
        }
        private void SourceMenu_Closed(object sender, RoutedEventArgs e) { if (_zOrderTimer != null) _zOrderTimer.Start(); ForceOnTop(); }
    }
}
