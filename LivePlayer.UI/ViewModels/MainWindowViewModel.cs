using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ATL.Playlist;
using Avalonia.Controls;
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

        private CustomMediaPlayer MediaPlayer { get; set; }

        [Reactive] public TrackModel? CurrentTrack { get; set; }
        [Reactive] public TimeSpan CurrentPosition { get; set; }


        [Reactive] public bool IsLooping { get; set; }
        [Reactive] public bool IsShuffle { get; set; }
        [Reactive] public bool IsFileOut { get; set; }

        [Reactive] public bool IsFlyoutOpen { get; set; } = true;
        private double LastWidth { get; set; } = 0;

        private double _lastVolume = 0.1;

        public double Volume
        {
            get
            {
                if (MediaPlayer.State != PlaybackState.Playing)
                {
                    var val2 = Math.Sqrt(Math.Sqrt(_lastVolume));
                    return Math.Round(val2 * 100, 3, MidpointRounding.ToZero);
                }
                
                //Natural volume curve
                var val = Math.Sqrt(Math.Sqrt(MediaPlayer.Volume));
                
                return Math.Round(val * 100, 3, MidpointRounding.ToZero);
            }
            set
            {
                var val = Math.Clamp(value / 100, 0.0, 1.0);
                val = Math.Pow(val, 4.0);
                
                MediaPlayer.Volume = val;
                _lastVolume = val;
                this.RaisePropertyChanged(nameof(Volume));
            }
        }

        [Reactive] public ObservableCollection<TrackModel> Queue { get; set; } = new();
        private ObservableCollection<TrackModel> LastPlayed { get; set; } = new();

        public int QueueCount => Queue.Count;
        public TimeSpan QueueLength => TimeSpan.FromSeconds(Queue.Sum(x => x.Length.TotalSeconds));

        [Reactive] public TrackModel? QueueSelectedTrack { get; set; }

        [Reactive] public string? UrlInput { get; set; }

        [Reactive] public bool IsPlaying { get; set; } = false;


        public MainWindowViewModel()
        {
            _random = new Random();
            _youtubeClient = new YoutubeClient();

            var opus = Bass.PluginLoad(@"bassopus.dll");
            var webm = Bass.PluginLoad(@"basswebm.dll");
            var aac = Bass.PluginLoad(@"bass_aac.dll");
            
            MediaPlayer = new CustomMediaPlayer()
            {
                Volume = _lastVolume
            };

            MediaPlayer.MediaEnded += MediaPlayer_EndReached;
            this.RaisePropertyChanged(nameof(Volume));

            _ = Task.Run(PositionWatcher);
        }

        private async Task PositionWatcher()
        {
            while (true)
            {
                await Task.Delay(100);
                if (MediaPlayer.State == PlaybackState.Playing)
                {
                    CurrentPosition = MediaPlayer.Position;
                }
            }
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
                while (nextTrack == CurrentTrack && Queue.Count > 1)
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
                        IsPlaying = false;
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

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    //MediaPlayer.Play(new Media(_libVlc, new Uri(CurrentTrack.DirectPath!)));
                    //MediaPlayer.Stop();
                    await MediaPlayer.LoadAsync(CurrentTrack.DirectPath!);
                    //Volume = _lastVolume;
                    MediaPlayer.Volume = _lastVolume;
                    IsPlaying = MediaPlayer.Play();
                    //Logger.Sink?.Log(LogEventLevel.Information, "MainVM", this, $"Playing {CurrentTrack.Title}");
                });
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);
                }
            });
        }

        //private void MediaPlayer_PositionChanged(object? sender, PropertyChangedEventArgs e)
        //{
        //    ////Just in case for load Playlist
        //    //this.RaisePropertyChanged(nameof(QueueLength));
        //    ////Probably needs math for true position
        //    //var totalSeconds = CurrentTrack!.Length.TotalSeconds;
        //    ////to long
        //    //var position = (long)(totalSeconds * e.Position);
        //    CurrentPosition = MediaPlayer.Position;
        //}

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

        public void OpenSourceRepo()
        {
            var ps = new ProcessStartInfo("https://github.com/Sekoree/LivePlayer")
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }

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
                var track = new TrackModel(_youtubeClient, Queue)
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
                    var t = new TrackModel(_youtubeClient, Queue)
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
                        Extensions = new List<string>() { "m3u" },
                        Name = "M3U Playlist"
                    }
                },
                InitialFileName = "Playlist.m3u",
                Title = "Save Current Playlist"
            };
            var dialogResult = await plsPathDialog.ShowAsync(parent);
            if (string.IsNullOrWhiteSpace(dialogResult))
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
                        Extensions = new List<string>() { "m3u" },
                        Name = "M3U Playlist"
                    }
                },
                Title = "Load Playlist (Only with YouTube URL supported)"
            };
            var dialogResult = await plsPathDialog.ShowAsync(parent);
            if (dialogResult == null || dialogResult.Length == 0)
            {
                return;
            }

            var pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(dialogResult[0]);
            Queue.Clear();
            foreach (var path in pls.FilePaths)
            {
                var track = new TrackModel(_youtubeClient, Queue)
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

        public async Task Play()
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

                await MediaPlayer.LoadAsync(CurrentTrack.DirectPath!);
                //Volume = _lastVolume;
                MediaPlayer.Volume = _lastVolume;
                IsPlaying = MediaPlayer.Play();
                CurrentTrack.IsPlaying = true;
                if (IsFileOut)
                {
                    await File.WriteAllTextAsync("Title.txt", CurrentTrack.Title);
                    await File.WriteAllTextAsync("Artist.txt", CurrentTrack.Artist);
                }

                QueueSelectedTrack = null;
                return;
            }
            
            //Volume = _lastVolume;
            MediaPlayer.Volume = _lastVolume;
            MediaPlayer.Play();
            IsPlaying = true;
        }
        
        public void Pause()
        {
            MediaPlayer.Pause();
            IsPlaying = false;
        }

        public void Stop()
        {
            if (CurrentTrack == null)
                return;

            CurrentTrack!.IsPlaying = false;
            CurrentPosition = TimeSpan.Zero;
            MediaPlayer.Stop();
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
            if (CurrentPosition.TotalSeconds > 5 || LastPlayed.Count == 0)
            {
                MediaPlayer.Position = TimeSpan.Zero;
            }
            else
            {
                CurrentTrack!.IsPlaying = false;
                var prevSong = LastPlayed[0];
                LastPlayed.RemoveAt(0);
                CurrentTrack = prevSong;
                CurrentTrack.IsPlaying = true;
                CurrentPosition = TimeSpan.Zero;
                
                await MediaPlayer.LoadAsync(CurrentTrack.DirectPath!);
                MediaPlayer.Volume = _lastVolume;
                //Volume = _lastVolume;
                IsPlaying = MediaPlayer.Play();
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