using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LivePlayer.UI.Controls;

public partial class MediaControls : UserControl
{
    public MediaControls()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<MediaControls, bool>(nameof(IsPlaying));

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }
    
    public static readonly StyledProperty<bool> CanFastRewindProperty =
        AvaloniaProperty.Register<MediaControls, bool>(nameof(CanFastRewind));

    public bool CanFastRewind
    {
        get => GetValue(CanFastRewindProperty);
        set => SetValue(CanFastRewindProperty, value);
    }
    
    public static readonly StyledProperty<bool> CanFastForwardProperty =
        AvaloniaProperty.Register<MediaControls, bool>(nameof(CanFastForward));

    public bool CanFastForward
    {
        get => GetValue(CanFastForwardProperty);
        set => SetValue(CanFastForwardProperty, value);
    }
    
    public static readonly DirectProperty<MediaControls, ICommand> PlayCommandProperty =
        AvaloniaProperty.RegisterDirect<MediaControls, ICommand>(nameof(PlayCommand),
            control => control.PlayCommand, (control, command) => control.PlayCommand = command);
    
    private ICommand _playCommand;
    
    public ICommand PlayCommand
    {
        get => _playCommand;
        set => SetAndRaise(PlayCommandProperty, ref _playCommand, value);
    }
    
    public static readonly DirectProperty<MediaControls, ICommand> PauseCommandProperty =
        AvaloniaProperty.RegisterDirect<MediaControls, ICommand>(nameof(PauseCommand),
            control => control.PauseCommand, (control, command) => control.PauseCommand = command);
    
    private ICommand _pauseCommand;
    
    public ICommand PauseCommand
    {
        get => _pauseCommand;
        set => SetAndRaise(PauseCommandProperty, ref _pauseCommand, value);
    }
    
    public static readonly DirectProperty<MediaControls, ICommand> StopCommandProperty =
        AvaloniaProperty.RegisterDirect<MediaControls, ICommand>(nameof(StopCommand),
            control => control.StopCommand, (control, command) => control.StopCommand = command);
    
    private ICommand _stopCommand;
    
    public ICommand StopCommand
    {
        get => _stopCommand;
        set => SetAndRaise(StopCommandProperty, ref _stopCommand, value);
    }
    
    public static readonly DirectProperty<MediaControls, ICommand> FastRewindCommandProperty =
        AvaloniaProperty.RegisterDirect<MediaControls, ICommand>(nameof(FastRewindCommand),
            control => control.FastRewindCommand, (control, command) => control.FastRewindCommand = command);
    
    private ICommand _fastRewindCommand;
    
    public ICommand FastRewindCommand
    {
        get => _fastRewindCommand;
        set => SetAndRaise(FastRewindCommandProperty, ref _fastRewindCommand, value);
    }
    
    public static readonly DirectProperty<MediaControls, ICommand> FastForwardCommandProperty =
        AvaloniaProperty.RegisterDirect<MediaControls, ICommand>(nameof(FastForwardCommand),
            control => control.FastForwardCommand, (control, command) => control.FastForwardCommand = command);
    
    private ICommand _fastForwardCommand;
    
    public ICommand FastForwardCommand
    {
        get => _fastForwardCommand;
        set => SetAndRaise(FastForwardCommandProperty, ref _fastForwardCommand, value);
    }
    
}