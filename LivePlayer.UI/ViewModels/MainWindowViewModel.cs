using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATL.Playlist;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using LivePlayer.UI.Models;
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
        private readonly LibVLC _libVlc;
        private readonly Random _random;
        private readonly YoutubeClient _youtubeClient;

        [Reactive] public MediaPlayer MediaPlayer { get; set; }

        [Reactive] public TrackModel? CurrentTrack { get; set; }
        [Reactive] public TimeSpan CurrentPosition { get; set; }


        [Reactive] public bool IsLooping { get; set; }
        [Reactive] public bool IsShuffle { get; set; }
        [Reactive] public bool IsFileOut { get; set; }

        [Reactive] public bool IsFlyoutOpen { get; set; } = true;
        private double LastWidth { get; set; } = 0;

        public int Volume
        {
            get => MediaPlayer.Volume;
            set
            {
                MediaPlayer.Volume = value;
                this.RaisePropertyChanged(nameof(Volume));
            }
        }

        [Reactive] public ObservableCollection<TrackModel> Queue { get; set; } = new();
        private ObservableCollection<TrackModel> LastPlayed { get; set; } = new();
        
        public int QueueCount => Queue.Count;
        public TimeSpan QueueLength => TimeSpan.FromSeconds(Queue.Sum(x => x.Length.TotalSeconds));

        [Reactive] public TrackModel? QueueSelectedTrack { get; set; }

        [Reactive] public string? UrlInput { get; set; }


        public MainWindowViewModel()
        {
            _random = new Random();
            _youtubeClient = new YoutubeClient();
            Core.Initialize();
            _libVlc = new LibVLC();
            MediaPlayer = new MediaPlayer(_libVlc);

            MediaPlayer.PositionChanged += MediaPlayer_PositionChanged;
            MediaPlayer.EndReached += MediaPlayer_EndReached;

            //_ = Task.Run(Test);
        }

        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            if (CurrentTrack == null)
                return;
            
            //Start next song
            TrackModel? nextTrack = null;
            CurrentTrack!.IsPlaying = false;
            if (IsShuffle)
            {
                //Get random track
                nextTrack = Queue[_random.Next(Queue.Count)];
            }
            else
            {
                var nextIndex = Queue.IndexOf(CurrentTrack!) + 1;
                if (nextIndex >= Queue.Count)
                {
                    if (IsLooping)
                    {
                        nextTrack = Queue[0];
                    }
                    else
                    {
                        LastPlayed.Insert(0, CurrentTrack);
                        if (!IsFileOut)
                            return;

                        File.Delete("Title.txt");
                        File.Delete("Artist.txt");
                        return;
                    }
                }
                else
                {
                    nextTrack = Queue[nextIndex];
                }
            }

            LastPlayed.Insert(0, CurrentTrack);
            CurrentTrack = nextTrack;
            CurrentTrack!.IsPlaying = true;
            CurrentPosition = TimeSpan.Zero;
            _ = Task.Run(async () =>
            {
                //if (string.IsNullOrWhiteSpace(CurrentTrack.DirectPath))
                //{
                    await CurrentTrack.GetDirectPath();
                //}

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MediaPlayer.Play(new Media(_libVlc, new Uri(CurrentTrack.DirectPath!)));
                    //Logger.Sink?.Log(LogEventLevel.Information, "MainVM", this, $"Playing {CurrentTrack.Title}");
                });
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);   
                }
            });
        }

        private void MediaPlayer_PositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
        {
            //Just in case for load Playlist
            this.RaisePropertyChanged(nameof(QueueLength));
            //Probably needs math for true position
            var totalSeconds = CurrentTrack!.Length.TotalSeconds;
            //to long
            var position = (long)(totalSeconds * e.Position);
            CurrentPosition = TimeSpan.FromSeconds(position + 1);
        }

        public void OpenCloseFlyOut(Window window)
        {
            IsFlyoutOpen = !IsFlyoutOpen;
            //Idk lol

            if (IsFlyoutOpen)
            {
                window.SizeToContent = SizeToContent.Manual;
                window.Width = LastWidth;
            }
            else
            {
                LastWidth = window.ClientSize.Width;
                window.SizeToContent = SizeToContent.Width;
            }
        }
        
        //public async Task Test()
        //{
        //    //await Task.Delay(5000);
        //    var yt = new YoutubeClient();
        //    var vid = await yt.Videos.GetAsync("https://www.youtube.com/watch?v=4_J5lGW2CY0");
        //    var track = new TrackModel(_youtubeClient)
        //    {
        //        Artist = vid.Author.ChannelTitle,
        //        Title = vid.Title,
        //        Path = "https://www.youtube.com/watch?v=4_J5lGW2CY0",
        //        Length = vid.Duration,
        //        CoverUrl = vid.Thumbnails.GetWithHighestResolution().Url,
        //        IsPlaying = true
        //    };
        //    Dispatcher.UIThread.Post(() =>
        //    {
        //        Queue.Add(track);
        //        CurrentTrack = track;
        //        using var media = new Media(_libVlc, new Uri(track.DirectPath!));
        //        MediaPlayer.Play(media);
        //    });
        //}

        public async Task AddSongToQueue()
        {
            if (UrlInput == null)
            {
                return;
            }

            var videoId = VideoId.TryParse(UrlInput);
            var playlistId = PlaylistId.TryParse(UrlInput);

            if (videoId != null)
            {
                var vid = await _youtubeClient.Videos.GetAsync(UrlInput);
                var track = new TrackModel(_youtubeClient)
                {
                    Artist = vid.Author.ChannelTitle,
                    Title = vid.Title,
                    Path = $"https://www.youtube.com/watch?v={vid.Id}",
                    Length = vid.Duration ?? TimeSpan.Zero,
                    CoverUrl = vid.Thumbnails.GetWithHighestResolution().Url,
                    IsPlaying = false
                };
                Queue.Add(track);
                this.RaisePropertyChanged(nameof(QueueCount));
                this.RaisePropertyChanged(nameof(QueueLength));
                UrlInput = null;
                return;
            }
            else if (playlistId != null)
            {
                var tracks = await _youtubeClient.Playlists.GetVideosAsync(playlistId.Value);
                foreach (var track in tracks)
                {
                    var t = new TrackModel(_youtubeClient)
                    {
                        Artist = track.Author.ChannelTitle,
                        Title = track.Title,
                        Path = $"https://www.youtube.com/watch?v={track.Id}",
                        Length = track.Duration ?? TimeSpan.Zero,
                        CoverUrl = track.Thumbnails.GetWithHighestResolution().Url,
                        IsPlaying = false
                    };
                    Queue.Add(t);
                    this.RaisePropertyChanged(nameof(QueueCount));
                    this.RaisePropertyChanged(nameof(QueueLength));
                }
                UrlInput = null;
                return;
            }
            UrlInput = "Invalid URL";
            await Task.Delay(2500);
            UrlInput = null;
        }
        
        public void RemoveTrackFromQueue(TrackModel track)
        {
            Queue.Remove(track);
            this.RaisePropertyChanged(nameof(QueueCount));
            this.RaisePropertyChanged(nameof(QueueLength));
        }
        
        public async Task SavePlaylist(Window parent)
        {
            var plsPathDialog = new SaveFileDialog()
            {
                DefaultExtension = "m3u",
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Extensions = new List<string>() {"m3u"},
                        Name = "M3U Playlist"
                    }
                },
                InitialFileName = "Playlist.m3u",
                Title = "Save Current Playlist"
            };
            var dialogResult = await plsPathDialog.ShowAsync(parent);
            if(string.IsNullOrWhiteSpace(dialogResult))
            {
                return;
            }

            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(dialogResult);
            pls.FilePaths = Queue.Select(t => t.Path!).ToList();
        }
        
        public async Task LoadPlaylist(Window parent)
        {
            var plsPathDialog = new OpenFileDialog()
            {
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Extensions = new List<string>() {"m3u"},
                        Name = "M3U Playlist"
                    }
                },
                Title = "Load Playlist (Only with YouTube URL supported)"
            };
            var dialogResult = await plsPathDialog.ShowAsync(parent);
            if(dialogResult == null || dialogResult.Length == 0)
            {
                return;
            }
            
            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(dialogResult[0]);
            Queue.Clear();
            foreach (var path in pls.FilePaths)
            {
                var track = new TrackModel(_youtubeClient)
                {
                    Path = path,
                    IsPlaying = false
                };
                _ = Task.Run(track.LoadInfoAsync);
                Queue.Add(track);
            }
            this.RaisePropertyChanged(nameof(QueueCount));
            this.RaisePropertyChanged(nameof(QueueLength));
        }
        
        public void ClearQueue()
        {
            Queue.Clear();
            this.RaisePropertyChanged(nameof(QueueCount));
            this.RaisePropertyChanged(nameof(QueueLength));
        }

        #region MediaButtons

        public async Task PlayPause()
        {
            if (QueueSelectedTrack != null)
            {
                //Play selected
                if (CurrentTrack != null)
                {
                    CurrentTrack.IsPlaying = false;
                }

                CurrentTrack = QueueSelectedTrack;
                //if (string.IsNullOrWhiteSpace(CurrentTrack.DirectPath))
                //{
                    await CurrentTrack.GetDirectPath();
                //}

                using var media = new Media(_libVlc, new Uri(QueueSelectedTrack.DirectPath!));
                MediaPlayer.Play(media);
                CurrentTrack.IsPlaying = true;
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);   
                }
                QueueSelectedTrack = null;
                return;
            }

            MediaPlayer.SetPause(MediaPlayer.IsPlaying);
        }

        public void Stop()
        {
            if (CurrentTrack == null)
                return;

            CurrentTrack!.IsPlaying = false;
            CurrentPosition = TimeSpan.Zero;
            MediaPlayer.Stop();
            
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
            if (CurrentPosition.TotalSeconds > 5 || LastPlayed.Count == 0)
            {
                MediaPlayer.Position = 0f;
            }
            else
            {
                CurrentTrack!.IsPlaying = false;
                var prevSong = LastPlayed[0];
                LastPlayed.RemoveAt(0);
                CurrentTrack = prevSong;
                CurrentTrack.IsPlaying = true;
                CurrentPosition = TimeSpan.Zero;
                using var media = new Media(_libVlc, new Uri(prevSong.DirectPath!));
                MediaPlayer.Play(media);
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);   
                }
            }
        }

        #endregion
    }
}