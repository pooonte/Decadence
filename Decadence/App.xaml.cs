using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Загрузить состояние из ранее приостановленного приложения
                }

                Window.Current.Content = rootFrame;
            }


            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
                ApplicationView.PreferredLaunchViewSize = new Size(360, 640);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

                var view = ApplicationView.GetForCurrentView();

                // Устанавливаем минимальный размер (окно нельзя будет сделать меньше 360x640)
                view.SetPreferredMinSize(new Size(360, 640));

                // Пытаемся установить точный размер окна
                view.TryResizeView(new Size(360, 640));

                // Для имитации "неизменяемого" размера - подписываемся на событие
                view.VisibleBoundsChanged += (sender, args) =>
                {
                    var currentView = ApplicationView.GetForCurrentView();
                    if (currentView.VisibleBounds.Width != 360 || currentView.VisibleBounds.Height != 640)
                    {
                        currentView.TryResizeView(new Size(360, 640));
                    }
                };

                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
        public static event EventHandler<Windows.Storage.StorageFile> PlayRequested;
        public static void RaisePlayRequested(StorageFile file)
        {
            PlayRequested?.Invoke(null, file);
        }
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // TODO: Сохранить состояние приложения
            deferral.Complete();
        }
    }
}
