using Spectre.Console;

namespace Galileo.Chat.Client.UI;

/// <summary>Boot splash: draws the cat from the centre outward in a spiral.</summary>
public static class CatSplash
{
    // "##" pairs keep the cat roughly square in monospaced fonts (glyph H ≈ 2× W).
    private static readonly string[] _art =
    {
        "                                ##              ##",
        "                                ####          ####",
        "  ####                          ##################",
        "##########                      ##################",
        "  ##########                    ####  ######  ####",
        "      ######                    ####    ####    ##",
        "        ######                  ##################",
        "        ######        ########    ################",
        "        ######    ############    ##############  ",
        "        ######  ################      ########    ",
        "        ##########################                ",
        "        ############################              ",
        "        ####################################      ",
        "        ####################################      ",
        "        ####################################      ",
        "        ############################  ######      ",
        "        ########################      ######      ",
        "        ######################        ######      ",
        "        ####################          ######      ",
        "        ######################        ######      ",
        "        ########################      ######      ",
        "          ######################      ######      ",
    };

    public static void Play(IAnsiConsole console, int delayMsPerPixel = 1)
    {
        var height = _art.Length;
        var width = _art.Max(l => l.Length);

        // Clamp to 0 so the animation degrades on tiny windows instead of crashing.
        var startCol = Math.Max(0, (console.Profile.Width - width) / 2);
        var startRow = 1;

        var cursorWasVisible = TryGetCursorVisible();
        TrySetCursorVisible(false);
        try
        {
            console.Clear();
            foreach (var (r, c) in SpiralFromCentre(height, width))
            {
                if (r < 0 || r >= height || c < 0 || c >= _art[r].Length) continue;
                var ch = _art[r][c];
                if (ch == ' ') continue;

                try
                {
                    Console.SetCursorPosition(startCol + c, startRow + r);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Window shrank below the cat — skip rather than crash.
                    continue;
                }
                Console.Write(ch);

                if (delayMsPerPixel > 0)
                    Thread.Sleep(delayMsPerPixel);
            }
        }
        finally
        {
            TrySetCursorVisible(cursorWasVisible);
        }

        try { Console.SetCursorPosition(0, startRow + height + 1); }
        catch (ArgumentOutOfRangeException) { /* viewport too small; live with it */ }

        var disclaimer = new Panel(new Markup(
            "[grey70]DeepChat — chat interno na LAN, ponta a ponta.[/]\n" +
            "[grey50]O servidor não consegue ler as mensagens. Histórico apaga em 24h.[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Brand,
            Padding = new Padding(2, 0)
        };
        console.Write(disclaimer);
        console.WriteLine();
    }

    /// <summary>Centre-outward square spiral: right 1, down 1, left 2, up 2, right 3 …</summary>
    private static IEnumerable<(int row, int col)> SpiralFromCentre(int height, int width)
    {
        var r = height / 2;
        var c = width / 2;
        yield return (r, c);

        var dirs = new (int dr, int dc)[] { (0, 1), (1, 0), (0, -1), (-1, 0) };
        var dirIdx = 0;
        var step = 1;
        var maxSteps = 2 * Math.Max(height, width) + 4;

        while (step <= maxSteps)
        {
            // Step length grows after every pair of direction changes.
            for (var pair = 0; pair < 2; pair++)
            {
                var (dr, dc) = dirs[dirIdx];
                for (var i = 0; i < step; i++)
                {
                    r += dr;
                    c += dc;
                    yield return (r, c);
                }
                dirIdx = (dirIdx + 1) % 4;
            }
            step++;
        }
    }

    private static bool TryGetCursorVisible()
    {
        try { return OperatingSystem.IsWindows() ? Console.CursorVisible : true; }
        catch { return true; }
    }

    private static void TrySetCursorVisible(bool visible)
    {
        try { Console.CursorVisible = visible; }
        catch { /* terminal doesn't support it — ignore */ }
    }
}
