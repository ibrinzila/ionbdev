using System.Globalization;
using Avalonia.Data.Converters;

namespace PortWhisperer.Desktop.ViewModels;

public static class Converters
{
    public static readonly IValueConverter ShowAllText =
        new FuncValueConverter<bool, string>(showAll =>
            showAll ? "Dev Only" : "Show All");

    public static readonly IValueConverter WatchText =
        new FuncValueConverter<bool, string>(watching =>
            watching ? "⏹ Stop" : "👁 Watch");
}
