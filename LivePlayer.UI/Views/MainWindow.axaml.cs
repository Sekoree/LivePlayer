using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
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
    }
}