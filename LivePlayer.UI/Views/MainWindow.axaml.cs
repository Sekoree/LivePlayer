using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LivePlayer.UI.Controls;
using LivePlayer.UI.Models;
using LivePlayer.UI.ViewModels;

namespace LivePlayer.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (DataContext is MainWindowViewModel dContext)
            {
                _ = Task.Run(() => dContext.AddSongToQueue());
            }
        }

        private void InputElement_OnDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel dContext) 
                return;
            if (sender is not QueueEntry { DataContext: TrackModel track }) 
                return;
            
            dContext.QueueSelectedTrack = track;
            _ = Task.Run(() => dContext.Play());
        }
    }
}