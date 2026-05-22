using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Galileo.Chat.Client.Gui.Infrastructure;

/// <summary>Small modal password prompt, built in code to avoid an extra XAML file.</summary>
public static class PasswordPrompt
{
    /// <summary>Shows a modal asking for a secret. Returns null if cancelled.</summary>
    public static string? Ask(Window? owner, string message)
    {
        var bg = (Brush?)Application.Current?.TryFindResource("BgBrush") ?? Brushes.Black;
        var fg = (Brush?)Application.Current?.TryFindResource("TextBrush") ?? Brushes.White;

        var box = new PasswordBox { Margin = new Thickness(0, 8, 0, 12), MinWidth = 280 };

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, MinWidth = 80 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, Foreground = fg, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = "DeepChat",
            Content = panel,
            Background = bg,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = owner
        };

        ok.Click += (_, _) => { dialog.DialogResult = true; };
        dialog.Loaded += (_, _) => box.Focus();

        return dialog.ShowDialog() == true ? box.Password : null;
    }
}
