using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using System.Diagnostics;
using System.IO;

namespace MiniPlayerDeskBand
{
    public partial class PlayerControl : UserControl
    {
        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private GlobalSystemMediaTransportControlsSession _currentSession;

        public PlayerControl()
        {
            InitializeComponent();
            this.Loaded += PlayerControl_Loaded;
        }

        private async void PlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeMediaManager();
        }

        private async Task InitializeMediaManager()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_manager == null) return;
                _manager.SessionsChanged += Manager_SessionsChanged;
                UpdateSession();
            }
            catch (Exception ex) 
            {
                TrackInfo.Text = "Error init media";
            }
        }

        private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Dispatcher.Invoke(() => UpdateSession());
        }

        private void UpdateSession()
        {
            if (_manager == null) return;
            var sessions = _manager.GetSessions();
            var targetSession = _manager.GetCurrentSession();
            if (targetSession == null && sessions.Count > 0) targetSession = sessions.First();

            if (_currentSession?.SourceAppUserModelId != targetSession?.SourceAppUserModelId)
            {
                if (_currentSession != null)
                {
                    _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                    _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                }
                _currentSession = targetSession;
                if (_currentSession != null)
                {
                    _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
                    _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
                    UpdateAppIcon(_currentSession.SourceAppUserModelId);
                }
                else
                {
                    AppIcon.Source = null;
                }
            }
            UpdateUI();
        }

        private void UpdateAppIcon(string sourceId)
        {
            try
            {
                string processName = sourceId.Contains(".") ? System.IO.Path.GetFileNameWithoutExtension(sourceId) : sourceId;
                processName = processName.ToLower();
                var process = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.ToLower().Contains(processName));
                if (process?.MainModule != null)
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                    if (icon != null)
                    {
                        AppIcon.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch { }
        }

        private async void UpdateUI()
        {
            if (_currentSession == null)
            {
                TrackInfo.Text = "No Media";
                BtnPlayPause.Content = "\uE768"; 
                return;
            }
            
            try 
            {
                var info = _currentSession.GetPlaybackInfo();
                if (info != null)
                {
                    BtnPlayPause.Content = (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ? "\uE769" : "\uE768"; 
                }

                var props = await _currentSession.TryGetMediaPropertiesAsync();
                if (props != null)
                {
                    string title = string.IsNullOrEmpty(props.Title) ? "Unknown" : props.Title;
                    TrackInfo.Text = string.IsNullOrEmpty(props.Artist) ? title : $"{title} - {props.Artist}";
                }
            }
            catch { }
        }

        private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => Dispatcher.Invoke(UpdateUI);
        private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => Dispatcher.Invoke(UpdateUI);

        private async void Prev_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TrySkipPreviousAsync(); }
        private async void PlayPause_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TryTogglePlayPauseAsync(); }
        private async void Next_Click(object sender, RoutedEventArgs e) { if (_currentSession != null) await _currentSession.TrySkipNextAsync(); }
    }
}
