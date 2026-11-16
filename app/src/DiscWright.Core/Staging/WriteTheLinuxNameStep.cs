namespace DiscWright.Core.Staging;

/// <summary>
/// Names the disc for a Linux desktop: <c>.xdg-volume-info</c> at the top,
/// and the icon beside the Windows one as a PNG.
/// </summary>
/// <remarks>
/// <para>
/// Only when asked for (<see cref="DiscOptions.LinuxNameAndIcon"/>). Windows
/// never reads either file and Linux never reads <c>autorun.inf</c>, so a
/// disc with both is the same disc on Windows and a named one on Linux.
/// </para>
/// <para>
/// <strong>After the artwork and after the menu</strong>, because the PNG is
/// made out of whichever icon the disc ended up with, and those are the two
/// steps that put one there: the artwork step writes a chosen icon, and the
/// menu step writes DiscWright's own when nobody chose one. Run before them
/// this finds no icon and names the disc without a picture, and still looks
/// like it worked.
/// </para>
/// <para>
/// A disc with no icon at all, which is one with no menu and nothing chosen,
/// is still named. The name is the half that matters: without this file a
/// Linux desktop shows the volume id, which is sixteen capitals at most.
/// </para>
/// </remarks>
public sealed class WriteTheLinuxNameStep(IFileSystem files) : IBuildStep
{
    /// <inheritdoc />
    public string Says => "naming the disc for Linux";

    /// <inheritdoc />
    public bool AppliesTo(BuildContext context) => context.Project.Options.LinuxNameAndIcon;

    /// <inheritdoc />
    public Task Run(BuildContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string? picture = null;

        // Read back off the disc being made, and not asked of whatever drew
        // it, so this is the icon that is really there. Nothing there, or
        // nothing this can read as an icon, and the disc is named without a
        // picture: never a file naming a picture that was not written.
        if (context.TheIconOnTheDisc is string icon
            && files.ReadBytes(context.On(icon)) is byte[] ico
            && TheLinuxIcon.FromTheWindowsOne(ico) is byte[] png)
        {
            picture = TheLinuxIcon.NameBeside(icon);
            context.Writer.WriteBytes(context.On(picture), png);
        }

        context.Writer.WriteBytes(
            context.On(XdgVolumeInfo.FileName),
            XdgVolumeInfo.For(context.WhatThisDiscIsCalled, picture));

        return Task.CompletedTask;
    }
}
