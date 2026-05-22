using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Galileo.Chat.Client.Gui.Infrastructure;
using Galileo.Chat.Client.Gui.ViewModels;

namespace Galileo.Chat.Client.Gui.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _vm;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => InputBox.Focus();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.Messages.CollectionChanged -= OnMessagesChanged;

        _vm = e.NewValue as ChatViewModel;
        if (_vm is null) return;

        _vm.Messages.CollectionChanged += OnMessagesChanged;
        _vm.PassphrasePrompt = msg => PasswordPrompt.Ask(Window.GetWindow(this), msg);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            MessageScroll.ScrollToEnd();
    }
}
