using Decadence.Models;
using Singleton;
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
        private DispatcherTimer _gcTimer;
        // 🔹 Плейлисты (лёгкий список)
        public static List<Playlist> CurrentPlaylists { get; set; }

        // 🔹 Безопасное событие (очищается при сворачивании)
        public static event Action PlaylistsUpdated;
        private DispatcherTimer _memoryDebugTimer; // ВРЕМЕННО, для диагностики

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            Windows.System.MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;

#if DEBUG
            _memoryDebugTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _memoryDebugTimer.Tick += (s, e) =>
            {
                var usage = Windows.System.MemoryManager.AppMemoryUsage / 1024 / 1024;
                var limit = Windows.System.MemoryManager.AppMemoryUsageLimit / 1024 / 1024;
                var level = Windows.System.MemoryManager.AppMemoryUsageLevel;
                System.Diagnostics.Debug.WriteLine($"📈 Память: {usage}MB / {limit}MB, уровень: {level}");
            };
            _memoryDebugTimer.Start();
#endif
        }

        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            var level = Windows.System.MemoryManager.AppMemoryUsageLevel;

            if (level == Windows.System.AppMemoryUsageLevel.High ||
                level == Windows.System.AppMemoryUsageLevel.OverLimit)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Память: {level}, принудительная сборка");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
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

            _gcTimer?.Stop();
            MediaPlayerSingleton.Shutdown();

            // Полная сборка при сворачивании
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            deferral.Complete();
        }

        public static void NotifyPlaylistsUpdated() => PlaylistsUpdated?.Invoke();

        public static bool UseExperimentalBackground
        {
            get => (bool)(Windows.Storage.ApplicationData.Current.LocalSettings.Values["ExpBg"] ?? false);
            set => Windows.Storage.ApplicationData.Current.LocalSettings.Values["ExpBg"] = value;
        }
    }
}