using Decadence.Models;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

public class LibraryPanelBase
{
    public Grid Panel { get; set; }
    public ListView MainList { get; set; }
    public Grid SongsPanel { get; set; }
    public ListView SongsList { get; set; }
    public TextBlock TitleText { get; set; }
    public TextBlock SelectedName { get; set; }
    public Button BackButton { get; set; }

    public void ShowMainList()
    {
        MainList.Visibility = Visibility.Visible;
        SongsPanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        TitleText.Visibility = Visibility.Visible;
        SelectedName.Visibility = Visibility.Collapsed;
    }

    public void ShowSongs(string name, List<TrackItem> tracks)
    {
        for (int i = 0; i < tracks.Count; i++)
            tracks[i].TrackNumber = i + 1;

        SelectedName.Text = name;
        SongsList.ItemsSource = tracks;

        MainList.Visibility = Visibility.Collapsed;
        SongsPanel.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        TitleText.Visibility = Visibility.Collapsed;
        SelectedName.Visibility = Visibility.Visible;
    }

    public void Close()
    {
        Panel.Visibility = Visibility.Collapsed;
        ShowMainList();
    }
}