using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LivePlayer.UI.Models;
using LivePlayer.UI.ViewModels;

namespace LivePlayer.UI.Controls;

public partial class NextUp : UserControl
{
    public NextUp()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    
    private void NextUpElement_OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel dContext) 
            return;
        if (sender is not QueueEntry { DataContext: TrackModel track }) 
            return;
            
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await dContext.PlayTrack(track);
        });
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is MainWindowViewModel dContext)
        {
            _ = Task.Run(() => dContext.AddTracksToNextUp());
        }
    }
}