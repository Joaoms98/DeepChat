using System.Windows.Controls;
using Galileo.Chat.Client.Gui.ViewModels;

namespace Galileo.Chat.Client.Gui.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        // PasswordBox can't bind for security reasons — push changes to the VM manually.
        PasswordInput.PasswordChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = PasswordInput.Password;
        };
    }
}
