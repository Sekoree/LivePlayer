using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Markup.Xaml;
using LivePlayer.UI.Models;

namespace LivePlayer.UI.Controls;

[PseudoClasses(":isPlaying")]
public partial class QueueEntry : UserControl
{
    public QueueEntry()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<QueueEntry, bool>(nameof(IsPlaying));
    
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly StyledProperty<TrackModel> TrackProperty =
        AvaloniaProperty.Register<QueueEntry, TrackModel>(nameof(Track));
    
    public TrackModel Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            PseudoClasses.Set(":isPlaying", change.NewValue.GetValueOrDefault<bool>());
        }
    }
}