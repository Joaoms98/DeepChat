using Spectre.Console;

namespace Galileo.Chat.Client.UI;

public static class Theme
{
    public static readonly Style Brand = new(foreground: Color.SteelBlue1);
    public static readonly Style System = new(foreground: Color.Grey70);
    public static readonly Style Self = new(foreground: Color.SeaGreen1);
    public static readonly Style Other = new(foreground: Color.SkyBlue1);
    public static readonly Style Time = new(foreground: Color.Grey50);
    public static readonly Style Error = new(foreground: Color.Red);
    public static readonly Style Warning = new(foreground: Color.Yellow);
    public static readonly Style Success = new(foreground: Color.SpringGreen2_1);

    /// <summary>Corporate startup banner — figlet headline + boxed disclaimer.</summary>
    public static void RenderBanner(IAnsiConsole console)
    {
        console.Write(new FigletText("DeepChat").LeftJustified().Color(Color.SteelBlue1));

        var disclaimer = new Panel(new Markup(
            "[grey70]Chat interno na LAN — criptografia ponta a ponta.[/]\n" +
            "[grey50]O servidor não consegue ler as mensagens. Histórico apaga em 24h.[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Brand,
            Padding = new Padding(2, 0)
        };
        console.Write(disclaimer);
        console.WriteLine();
    }
}
