using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Input;

namespace Decadence
{
    public sealed partial class FullButtonsPanel : UserControl
    {
        public bool IsVisible => RootGrid.Visibility == Visibility.Visible;
        public event ItemClickEventHandler TrackClicked;
        public event EventHandler BackClicked;
        public event EventHandler<TrackItem> AddToPlaylistRequested;

        private ObservableCollection<TrackItem> _allTracks;
        private string _currentSort = "По названию";

        public FullButtonsPanel()
        {
            this.InitializeComponent();
            TracksListView.ItemClick += TracksListView_ItemClick;

            if (SortCombo != null)
                SortCombo.SelectionChanged += SortCombo_SelectionChanged;
        }

        public void SetTracks(ObservableCollection<TrackItem> tracks)
        {
            _allTracks = tracks;
            ApplySorting();
        }

        private void ApplySorting()
        {
            if (_allTracks == null) return;

            List<TrackItem> sorted;
            switch (_currentSort)
            {
                case "По исполнителю":
                    sorted = _allTracks.OrderBy(t => t.Artist).ThenBy(t => t.Title).ToList();
                    break;
                case "По альбому":
                    sorted = _allTracks.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber).ThenBy(t => t.Title).ToList();
                    break;
                case "По длительности":
                    sorted = _allTracks.OrderBy(t => t.Duration).ToList();
                    break;
                default:
                    sorted = _allTracks.OrderBy(t => t.Title).ToList();
                    break;
            }

            // 🔹 Очищаем и наполняем существующую коллекцию вместо создания новой
            if (_sortedTracks == null)
                _sortedTracks = new ObservableCollection<TrackItem>();
            else
                _sortedTracks.Clear();

            foreach (var track in sorted)
                _sortedTracks.Add(track);

            TracksListView.ItemsSource = _sortedTracks;
        }
        public void Clear()
        {
            if (TracksListView != null)
                TracksListView.ItemsSource = null;
            this.DataContext = null;
            GC.Collect(0, GCCollectionMode.Optimized);  // 🔹 только поколение 0
        }
        private ObservableCollection<TrackItem> _sortedTracks;
        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortCombo.SelectedItem is ComboBoxItem item)
            {
                _currentSort = item.Content.ToString();
                ApplySorting();
            }
        }

        public void Show()
        {
            RootGrid.Visibility = Visibility.Visible;
            RootGrid.Opacity = 0;
            PanelOpenAnimation.Begin();
        }

        public void Hide()
        {
            PanelCloseAnimation.Completed -= PanelCloseAnimation_Completed;
            PanelCloseAnimation.Completed += PanelCloseAnimation_Completed;
            PanelCloseAnimation.Begin();
        }
        private void PanelCloseAnimation_Completed(object sender, object e)
        {
            RootGrid.Visibility = Visibility.Collapsed;
            ((CompositeTransform)RootGrid.RenderTransform).TranslateY = 0;
        }

        private void TracksListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            TrackClicked?.Invoke(this, e);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            BackClicked?.Invoke(this, EventArgs.Empty);
        }

        private void TracksListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as TrackItem;
            if (item != null)
            {
                var menu = new MenuFlyout();
                var addItem = new MenuFlyoutItem { Text = "Добавить в плейлист" };
                addItem.Click += (s, args) => AddToPlaylistRequested?.Invoke(this, item);
                menu.Items.Add(addItem);
                menu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
            }
        }
    }
}