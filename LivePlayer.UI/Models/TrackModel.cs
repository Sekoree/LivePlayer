using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace LivePlayer.UI.Models;

public class TrackModel : ReactiveObject
{
    private readonly YoutubeClient _client;
    [Reactive] public string? Title { get; set; }
    [Reactive] public string? Artist { get; set; }
    [Reactive] public string? Path { get; set; }
    [Reactive] public string? DirectPath { get; set; }
    [Reactive] public TimeSpan Length { get; set; } = TimeSpan.Zero;
    [Reactive] public string? CoverUrl { get; set; }
    [Reactive] public bool IsPlaying { get; set; }
    [Reactive] public bool CanDelete { get; set; }
    private readonly ObservableCollection<TrackModel>? _parent;

    public TrackModel(YoutubeClient client, ObservableCollection<TrackModel>? parent = null)
    {
        _client = client;
        CanDelete = parent != null;
        _parent = parent;
    }

    public async Task<string> GetDirectPath()
    {
        var streamInfoSet = await _client.Videos.Streams.GetManifestAsync(Path!);
        var streamInfo = streamInfoSet.GetAudioOnlyStreams()
            .Where(x => x.Container.Name == "webm")
            .GetWithHighestBitrate();
        DirectPath = streamInfo.Url;
        return streamInfo.Url;
    }

    public async Task LoadInfoAsync()
    {
        if (string.IsNullOrEmpty(Path))
            return;
        
        var video = await _client.Videos.GetAsync(Path!);
        Title = video.Title;
        Artist = video.Author.ChannelTitle;
        Length = video.Duration ?? Length;
        CoverUrl = video.Thumbnails.GetWithHighestResolution().Url;
    }
    
    public void Delete()
    {
        if (_parent == null)
            return;
        _parent.Remove(this);
    }
}