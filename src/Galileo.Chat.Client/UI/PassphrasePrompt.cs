using Spectre.Console;

namespace Galileo.Chat.Client.UI;

/// <summary>
/// Double-entry passphrase prompt. Typed in plaintext: Spectre's Secret('•')
/// silently drops input on some Windows conhost setups; until that's fixed,
/// shoulder-surfing is the lesser threat on a trusted LAN.
/// </summary>
public static class PassphrasePrompt
{
    public static string Ask(IAnsiConsole console, string label)
    {
        console.MarkupLine("[grey50]Atenção: a passphrase aparece em texto na tela. Não compartilhe screenshots após digitar.[/]");
        while (true)
        {
            var first = console.Ask<string>($"[steelblue1]{label}[/]:");
            var second = console.Ask<string>("[steelblue1]Confirme[/]:");

            if (string.Equals(first, second, StringComparison.Ordinal))
                return first;

            console.MarkupLine("[yellow]! As duas passphrases não conferem. Tente novamente.[/]");
        }
    }
}
