using Decadence.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
namespace Decadence
{
    sealed partial class App : Application
    {
        // 🔹 Состояние воспроизведения (лёгкие ссылки, допустимо)
        public static TrackItem CurrentTrack { get; set; }
        public static List<TrackItem> CurrentPlaylist { get; set; }
        public static int CurrentPlaylistIndex { get; set; }
        public static RepeatMode CurrentRepeatMode { get; set; }

        // 🔹 Плейлисты (лёгкий список)
        public static List<Playlist> CurrentPlaylists { get; set; }

        // 🔹 Безопасное событие (очищается при сворачивании)
        public static event Action PlaylistsUpdated;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                // 🔹 Держим в памяти только 1 страницу. Остальные уничтожаются при уходе.
                rootFrame.CacheSize = 1;

                Window.Current.Content = rootFrame;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Восстановить состояние если нужно
                }

                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();

                // Настройки окна (оставил твои)
                ApplicationView.PreferredLaunchViewSize = new Size(360, 640);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(360, 640));

                if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile")
                {
                    ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
                }
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        public static event EventHandler<StorageFile> PlayRequested;
        public static void RaisePlayRequested(StorageFile file) => PlayRequested?.Invoke(null, file);

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            // Очищаем статический кэш только при закрытии
            if (MainPage._staticGeometryCache != null)
            {
                foreach (var geo in MainPage._staticGeometryCache)
                {
                    try { geo.Dispose(); } catch { }
                }
                MainPage._staticGeometryCache = null;
                MainPage._cacheInitialized = false;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            deferral.Complete();
        }

        public static void NotifyPlaylistsUpdated() => PlaylistsUpdated?.Invoke();
    }
}