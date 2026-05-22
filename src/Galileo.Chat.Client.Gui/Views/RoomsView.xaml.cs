using System.Windows.Controls;
using Galileo.Chat.Client.Gui.ViewModels;

namespace Galileo.Chat.Client.Gui.Views;

public partial class RoomsView : UserControl
{
    public RoomsView()
    {
        InitializeComponent();
        PassphraseInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is RoomsViewModel vm)
                vm.Passphrase = PassphraseInput.Password;
        };
    }
}
