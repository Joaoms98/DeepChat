namespace Galileo.Chat.Client.Gui;

/// <summary>
/// Placeholder compilado apenas em builds não-Windows. A GUI usa WPF, que só
/// existe no Windows; este tipo permite que a solução compile em CI/Linux sem
/// arrastar o código WPF. Ver Galileo.Chat.Client.Gui.csproj.
/// </summary>
internal static class PlatformUnsupported
{
    public const string Message = "DeepChat GUI (WPF) é suportada apenas no Windows.";
}
