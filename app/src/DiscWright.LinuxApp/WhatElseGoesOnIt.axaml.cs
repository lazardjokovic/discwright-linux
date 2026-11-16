using Avalonia.Controls;
using Avalonia.Interactivity;

using DiscWright.ViewModels;

namespace DiscWright.LinuxApp;

/// <summary>
/// Everything that goes on a disc and is not a game: the icon, the menu's
/// background, music, a manual, extras, and the two pictures for printing.
/// </summary>
/// <remarks>
/// <c>WhatElseGoesOnIt</c> in the Windows app. Seven things to choose and
/// seven to clear, each handed to the screen, which decides what a choice
/// means. A picture is looked at the moment it is chosen, by the screen, so
/// what is wrong with it is said then, with the file named, and not at the
/// end of a build.
/// </remarks>
public partial class WhatElseGoesOnIt : Window
{
    private static readonly AKindOfFile[] Pictures =
    [
        new("Pictures", ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff"]),
        new("Anything", ["*"]),
    ];

    private static readonly AKindOfFile[] MusicFiles =
    [
        new("Music", ["*.mp3", "*.wav", "*.wma", "*.m4a"]),
        new("Anything", ["*"]),
    ];

    private static readonly AKindOfFile[] Manuals =
    [
        new("A manual", ["*.pdf", "*.txt", "*.htm", "*.html", "*.rtf", "*.chm"]),
        new("Anything", ["*"]),
    ];

    private readonly MakeADisc _screen;
    private readonly IAskTheDesktop _desktop;

    public WhatElseGoesOnIt(MakeADisc screen, IAskTheDesktop desktop)
    {
        _screen = screen;
        _desktop = desktop;

        InitializeComponent();
        DataContext = screen;
    }

    private async void ChooseTheDiscIcon(object? sender, RoutedEventArgs e) =>
        await TakeAPicture("The icon the disc wears", path => _screen.DiscIcon = path);

    private void ClearTheDiscIcon(object? sender, RoutedEventArgs e) => _screen.DiscIcon = null;

    private async void ChooseTheBackground(object? sender, RoutedEventArgs e) =>
        await TakeAPicture("The picture the menu is drawn on", path => _screen.Background = path);

    private void ClearTheBackground(object? sender, RoutedEventArgs e) => _screen.Background = null;

    private void PickAColour(object? sender, RoutedEventArgs e)
    {
        if (sender is Control swatch && swatch.DataContext is string colour)
        {
            _screen.BackgroundColourText = colour;
        }
    }

    private async void ChooseTheCover(object? sender, RoutedEventArgs e) =>
        await TakeAPicture("The picture for the printed cover", path => _screen.Cover = path);

    private void ClearTheCover(object? sender, RoutedEventArgs e) => _screen.Cover = null;

    private async void ChooseTheDiscFace(object? sender, RoutedEventArgs e) =>
        await TakeAPicture("The picture for the printed disc face", path => _screen.DiscFace = path);

    private void ClearTheDiscFace(object? sender, RoutedEventArgs e) => _screen.DiscFace = null;

    private async void ChooseTheMusic(object? sender, RoutedEventArgs e) =>
        await TakeAFile("What plays behind the menu", MusicFiles, path => _screen.Music = path);

    private void ClearTheMusic(object? sender, RoutedEventArgs e) => _screen.Music = null;

    private async void ChooseTheManual(object? sender, RoutedEventArgs e) =>
        await TakeAFile("The manual", Manuals, path => _screen.Manual = path);

    private void ClearTheManual(object? sender, RoutedEventArgs e) => _screen.Manual = null;

    private async void ChooseTheExtras(object? sender, RoutedEventArgs e)
    {
        if (await _desktop.ForAFolder(this, "A folder of extras to put on the disc") is string folder)
        {
            _screen.ExtrasFolder = folder;
        }
    }

    private void ClearTheExtras(object? sender, RoutedEventArgs e) => _screen.ExtrasFolder = null;

    private void Done(object? sender, RoutedEventArgs e) => Close();

    private Task TakeAPicture(string title, Action<string> keep) =>
        TakeAFile(
            title,
            Pictures,
            path =>
            {
                // Said now, with the file named, rather than at the end of a
                // build in somebody else's words.
                if (_screen.ThatPictureIsUsable(path))
                {
                    keep(path);
                }
            });

    private async Task TakeAFile(string title, IReadOnlyList<AKindOfFile> kinds, Action<string> keep)
    {
        if (await _desktop.ForAFile(this, title, kinds) is string chosen)
        {
            keep(chosen);
        }
    }
}
