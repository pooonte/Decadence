using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Decadence.Helpers
{
    public static class Hotkeys
    {
        // 🔹 События (MainPage сам решит что делать)
        public static event Action EscapePressed;
        public static event Action LeftPressed;
        public static event Action RightPressed;

        public static void Enable(Page page)
        {
            page.KeyDown += Page_KeyDown;
        }

        public static void Disable(Page page)
        {
            page.KeyDown -= Page_KeyDown;
        }

        private static void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                EscapePressed?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.A)
            {
                LeftPressed?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.D)
            {
                RightPressed?.Invoke();
                e.Handled = true;
            }
        }
    }
}