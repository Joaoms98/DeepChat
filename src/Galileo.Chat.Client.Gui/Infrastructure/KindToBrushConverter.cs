using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Galileo.Chat.Client.Gui.Models;

namespace Galileo.Chat.Client.Gui.Infrastructure;

/// <summary>Maps a <see cref="ChatMessageKind"/> to the themed brush for that line.</summary>
public sealed class KindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is ChatMessageKind kind
            ? kind switch
            {
                ChatMessageKind.Self => "SelfBrush",
                ChatMessageKind.Other => "OtherBrush",
                ChatMessageKind.DirectMessage => "DmBrush",
                ChatMessageKind.Warning => "WarningBrush",
                ChatMessageKind.Error => "ErrorBrush",
                ChatMessageKind.Success => "SuccessBrush",
                _ => "SystemBrush"
            }
            : "TextBrush";

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
