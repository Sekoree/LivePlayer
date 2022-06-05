using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATL.Playlist;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using LivePlayer.UI.Models;
using ManagedBass;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace LivePlayer.UI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        //private LibVLC _libVlc;
        private readonly Random _random;
        private readonly YoutubeClient _youtubeClient;

        private readonly CustomMediaPlayer _mediaPlayer;

        [Reactive] public TrackModel? CurrentTrack { get; set; }
        [Reactive] public TimeSpan CurrentPosition { get; set; }


        [Reactive] public bool IsLooping { get; set; }
        [Reactive] public bool IsShuffle { get; set; }
        [Reactive] public bool IsFileOut { get; set; }

        [Reactive] public bool IsPlaying { get; set; } = false;
        
        [Reactive] public bool CanFastForward { get; set; } = true;


        private double _lastVolume = 0.1;

        public double Volume
        {
            get
            {
                if (_mediaPlayer.State != PlaybackState.Playing)
                {
                    var val2 = Math.Sqrt(Math.Sqrt(_lastVolume));
                    return Math.Round(val2 * 100, 3, MidpointRounding.ToZero);
                }

                //Natural volume curve
                var val = Math.Sqrt(Math.Sqrt(_mediaPlayer.Volume));

                return Math.Round(val * 100, 3, MidpointRounding.ToZero);
            }
            set
            {
                var val = Math.Clamp(value / 100, 0.0, 1.0);
                val = Math.Pow(val, 4.0);

                _mediaPlayer.Volume = val;
                _lastVolume = val;
                this.RaisePropertyChanged(nameof(Volume));
            }
        }


        #region PlaylistTab

        [Reactive] public ObservableCollection<TrackModel> Playlist { get; set; } = new();
        [Reactive] public TrackModel? PlaylistSelectedTrack { get; set; }
        [Reactive] public string? PlaylistAddUrl { get; set; }

        #endregion

        #region NextUpTab

        [Reactive] public ObservableCollection<TrackModel> NextUp { get; set; } = new();
        [Reactive] public TrackModel? NextUpSelectedTrack { get; set; }
        [Reactive] public string? NextUpAddUrl { get; set; }
        [Reactive] public bool NextUpDoInsert { get; set; } = true;

        #endregion

        #region HistoryTab

        [Reactive] public ObservableCollection<TrackModel> History { get; set; } = new();
        [Reactive] public TrackModel? HistorySelectedTrack { get; set; }

        #endregion


        public MainWindowViewModel()
        {
            _random = new Random();
            _youtubeClient = new YoutubeClient();

            var opus = Bass.PluginLoad(@"bassopus.dll");
            var webm = Bass.PluginLoad(@"basswebm.dll");
            var aac = Bass.PluginLoad(@"bass_aac.dll");

            _mediaPlayer = new CustomMediaPlayer()
            {
                Volume = _lastVolume
            };

            _mediaPlayer.MediaEnded += MediaPlayer_EndReached;
            this.RaisePropertyChanged(nameof(Volume));

            _ = Task.Run(PositionWatcher);
        }

        private async Task PositionWatcher()
        {
            while (true)
            {
                await Task.Delay(100);
                if (_mediaPlayer.State == PlaybackState.Playing)
                {
                    CurrentPosition = _mediaPlayer.Position;
                }
            }
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            if (CurrentTrack == null)
                return;

            
            
            CanFastForward = false;
            //Start next song
            History.Insert(0, CurrentTrack);
            TrackModel? nextTrack = null;
            CurrentTrack!.IsPlaying = false;

            if (IsShuffle)
            {
                NextUp.RemoveAt(0);
                nextTrack = NextUp.FirstOrDefault();
                if (nextTrack == null)
                {
                    nextTrack = Playlist.FirstOrDefault(x => !IsLooping && x != CurrentTrack);
                    if (nextTrack == null)
                    {
                        CanFastForward = true;
                        return;
                    }

                    NextUp.Add(nextTrack);
                }

                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    var trackToAdd = Playlist[_random.Next(Playlist.Count)];
                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    if (NextUp.Contains(trackToAdd))
                        continue;
                    NextUp.Add(trackToAdd);
                }
            }
            else if (IsLooping)
            {
                NextUp.RemoveAt(0);
                nextTrack = NextUp.FirstOrDefault();
                if (nextTrack == null)
                {
                    nextTrack = Playlist.FirstOrDefault();
                    if (nextTrack == null)
                    {
                        CanFastForward = true;
                        return;
                    }

                    NextUp.Add(nextTrack);
                }

                var trackIndex = Playlist.IndexOf(nextTrack!);
                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    if (trackIndex + 1 >= Playlist.Count)
                        trackIndex = 0;
                    else
                        trackIndex++;
                    var nextTrackNextUp = Playlist[trackIndex];

                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    if (NextUp.Contains(nextTrackNextUp))
                        continue;
                    NextUp.Add(nextTrackNextUp);
                }
            }
            else
            {
                NextUp.RemoveAt(0);
                nextTrack = NextUp.FirstOrDefault();
                if (nextTrack == null)
                {
                    CanFastForward = true;
                    return;
                }

                var trackIndex = Playlist.IndexOf(nextTrack!);
                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    if (trackIndex + 1 >= Playlist.Count)
                        break;
                    trackIndex++;
                    var nextTrackNextUp = Playlist[trackIndex];

                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    if (NextUp.Contains(nextTrackNextUp))
                        continue;
                    NextUp.Add(nextTrackNextUp);
                }
            }

            //NextUp.Insert(0, nextTrack!);
            nextTrack.IsPlaying = true;
            CurrentTrack = nextTrack;
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await CurrentTrack!.GetDirectPath();
                _mediaPlayer.Stop();
                await _mediaPlayer.LoadAsync(CurrentTrack!.DirectPath);
                _mediaPlayer.Play();
                CanFastForward = true;
            
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);
                }
            });
        }

        public async Task PlayTrack(TrackModel? track)
        {
            if (track == null)
                return;
            if (CurrentTrack != null)
            {
                CurrentTrack.IsPlaying = false;
                History.Insert(0, CurrentTrack);
            }

            await track.GetDirectPath();

            if (IsShuffle)
            {
                NextUp.Clear();
                NextUp.Add(track);
                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    var nextTrack = Playlist[_random.Next(Playlist.Count)];
                    if (NextUp.Contains(nextTrack))
                        continue;
                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    NextUp.Add(nextTrack);
                }
            }
            else if (IsLooping)
            {
                NextUp.Clear();
                NextUp.Add(track);
                var trackIndex = Playlist.IndexOf(track);
                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    if (trackIndex + 1 >= Playlist.Count)
                        trackIndex = 0;
                    else
                        trackIndex++;
                    var nextTrack = Playlist[trackIndex];

                    if (NextUp.Contains(nextTrack))
                        continue;
                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    NextUp.Add(nextTrack);
                }
            }
            else
            {
                NextUp.Clear();
                NextUp.Add(track);
                var trackIndex = Playlist.IndexOf(track);
                while (NextUp.Count < 6 && Playlist.Count > 1)
                {
                    if (trackIndex + 1 >= Playlist.Count)
                        break;
                    trackIndex++;
                    var nextTrack = Playlist[trackIndex];

                    if (NextUp.Contains(nextTrack))
                        continue;
                    if (Playlist.All(x => NextUp.Any(y => x == y)))
                        break;
                    NextUp.Add(nextTrack);
                }
            }


            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                CurrentTrack = track;
                _mediaPlayer.Stop();
                await _mediaPlayer.LoadAsync(track.DirectPath!);
                _mediaPlayer.Volume = _lastVolume;
                CurrentTrack!.IsPlaying = true;
                IsPlaying = _mediaPlayer.Play();
                
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);
                }
            });
        }

        public async Task AddTracksToPlaylist()
        {
            if (string.IsNullOrWhiteSpace(PlaylistAddUrl))
                return;

            var videoId = VideoId.TryParse(PlaylistAddUrl);
            var playlistId = PlaylistId.TryParse(PlaylistAddUrl);
            if (videoId == null && playlistId == null)
            {
                PlaylistAddUrl = "Invalid URL";
                await Task.Delay(2500);
                PlaylistAddUrl = string.Empty;
                return;
            }

            if (videoId != null)
            {
                var video = await _youtubeClient.Videos.GetAsync(videoId.Value);
                //Maybe check if Video or Live
                var track = new TrackModel(_youtubeClient, Playlist)
                {
                    Artist = video.Author.ChannelTitle,
                    Title = video.Title,
                    Path = "https://www.youtube.com/watch?v=" + video.Id,
                    CoverUrl = video.Thumbnails.GetWithHighestResolution().Url,
                    Length = video.Duration.GetValueOrDefault(),
                };
                Playlist.Add(track);
            }

            else if (videoId == null && playlistId != null)
            {
                var plsVideos = await _youtubeClient.Playlists.GetVideosAsync(playlistId.Value);
                foreach (var video in plsVideos)
                {
                    var track = new TrackModel(_youtubeClient, Playlist)
                    {
                        Artist = video.Author.ChannelTitle,
                        Title = video.Title,
                        Path = "https://www.youtube.com/watch?v=" + video.Id,
                        CoverUrl = video.Thumbnails.GetWithHighestResolution().Url,
                        Length = video.Duration.GetValueOrDefault(),
                    };
                    Playlist.Add(track);
                }
            }

            PlaylistAddUrl = string.Empty;
        }
        
        
        public async Task AddTracksToNextUp()
        {
            if (string.IsNullOrWhiteSpace(NextUpAddUrl))
                return;

            var videoId = VideoId.TryParse(NextUpAddUrl);
            var playlistId = PlaylistId.TryParse(NextUpAddUrl);
            if (videoId == null && playlistId == null)
            {
                NextUpAddUrl = "Invalid URL";
                await Task.Delay(2500);
                NextUpAddUrl = string.Empty;
                return;
            }

            if (videoId != null)
            {
                var video = await _youtubeClient.Videos.GetAsync(videoId.Value);
                //Maybe check if Video or Live
                var track = new TrackModel(_youtubeClient)
                {
                    Artist = video.Author.ChannelTitle,
                    Title = video.Title,
                    Path = "https://www.youtube.com/watch?v=" + video.Id,
                    CoverUrl = video.Thumbnails.GetWithHighestResolution().Url,
                    Length = video.Duration.GetValueOrDefault(),
                };
                if (NextUpDoInsert)
                {
                    NextUp.Insert(1, track);
                }
                else
                {
                    NextUp.Add(track);
                }
            }

            else if (videoId == null && playlistId != null)
            {
                var plsVideos = await _youtubeClient.Playlists.GetVideosAsync(playlistId.Value);
                foreach (var video in plsVideos)
                {
                    var track = new TrackModel(_youtubeClient)
                    {
                        Artist = video.Author.ChannelTitle,
                        Title = video.Title,
                        Path = "https://www.youtube.com/watch?v=" + video.Id,
                        CoverUrl = video.Thumbnails.GetWithHighestResolution().Url,
                        Length = video.Duration.GetValueOrDefault(),
                    };
                    if (NextUpDoInsert)
                    {
                        NextUp.Insert(1, track);
                    }
                    else
                    {
                        NextUp.Add(track);
                    }
                }
            }

            NextUpAddUrl = string.Empty;
        }

        public async Task SavePlaylist()
        {
            var plsPathDialog = new SaveFileDialog()
            {
                DefaultExtension = "m3u",
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Extensions = new List<string>() { "m3u" },
                        Name = "M3U Playlist"
                    }
                },
                InitialFileName = "Playlist.m3u",
                Title = "Save Current Playlist"
            };
            var desktopLifetime = Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var dialogResult = await plsPathDialog.ShowAsync(desktopLifetime!.MainWindow);
            if (string.IsNullOrWhiteSpace(dialogResult))
            {
                return;
            }

            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(dialogResult);
            pls.FilePaths = Playlist.Select(t => t.Path!).ToList();
        }

        public async Task LoadPlaylist()
        {
            var plsPathDialog = new OpenFileDialog()
            {
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Extensions = new List<string>() { "m3u" },
                        Name = "M3U Playlist"
                    }
                },
                Title = "Load Playlist (Only with YouTube URL supported)"
            };
            var desktopLifetime = Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var dialogResult = await plsPathDialog.ShowAsync(desktopLifetime!.MainWindow);
            if (dialogResult == null || dialogResult.Length == 0)
            {
                return;
            }

            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(dialogResult[0]);
            Playlist.Clear();
            foreach (var path in pls.FilePaths)
            {
                var track = new TrackModel(_youtubeClient, Playlist)
                {
                    Path = path,
                    IsPlaying = false
                };
                _ = Task.Run(track.LoadInfoAsync);
                Playlist.Add(track);
            }
        }

        public void ClearQueue()
        {
            Playlist.Clear();
            var firstNextUp = NextUp.FirstOrDefault();
            NextUp.Clear();
            if (firstNextUp != null)
            {
                NextUp.Add(firstNextUp);
            }
        }

        #region MediaButtons

        public void Play()
        {
            _mediaPlayer.Volume = _lastVolume;
            _mediaPlayer.Play();
            IsPlaying = true;
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            IsPlaying = false;
        }

        public void Stop()
        {
            if (CurrentTrack == null)
                return;

            CurrentTrack!.IsPlaying = false;
            CurrentPosition = TimeSpan.Zero;
            _mediaPlayer.Stop();
            IsPlaying = false;

            if (!IsFileOut)
                return;

            File.Delete("Title.txt");
            File.Delete("Artist.txt");
        }

        public void FastForward()
        {
            MediaPlayer_EndReached(null, null!);
        }

        public async Task Rewind()
        {
            if (CurrentPosition.TotalSeconds > 5 || History.Count == 0)
            {
                _mediaPlayer.Position = TimeSpan.Zero;
            }
            else
            {
                CurrentTrack!.IsPlaying = false;
                var prevSong = History[0];
                History.RemoveAt(0);
                NextUp.Insert(0, prevSong);
                NextUp.RemoveAt(NextUp.Count - 1);
                CurrentTrack = prevSong;
                CurrentTrack.IsPlaying = true;
                CurrentPosition = TimeSpan.Zero;

                await _mediaPlayer.LoadAsync(CurrentTrack.DirectPath!);
                _mediaPlayer.Volume = _lastVolume;
                IsPlaying = _mediaPlayer.Play();
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);
                }
            }
        }

        #endregion

        #region Random stuff

        public void OpenSourceRepo()
        {
            var ps = new ProcessStartInfo("https://github.com/Sekoree/LivePlayer")
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

        #endregion
    }
}