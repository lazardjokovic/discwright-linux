using DiscWright.Core;

namespace DiscWright.Linux;

/// <summary>
/// The drives, the burner, the eraser and the tray of a Linux DiscWright that
/// does not write discs yet: no drives, and a plain no to everything else.
/// </summary>
/// <remarks>
/// <para>
/// The window is given a machine whole or not at all, so the four ports that
/// are about a recorder have to be answered by something before the recorder
/// exists. This answers them honestly. It finds no drives, which is what the
/// window already knows how to show, and asked to burn or erase all the same
/// it refuses in words that say why and where the image is.
/// </para>
/// <para>
/// It goes when the recorder arrives, over xorriso, on a machine with a real
/// drive in it. Nothing else has to change on that day: the window asks the
/// same four ports.
/// </para>
/// </remarks>
public sealed class NothingBurnsYet : IFindRecorders, IBurnDiscs, IEraseDiscs, IWorkTheTray
{
    public IReadOnlyList<Recorder> All() => [];

    public Task<Outcome<BurnedDisc>> Burn(
        string imagePath, Recorder onto, IProgress<BurnProgress>? progress, CancellationToken ct) =>
        Task.FromResult<Outcome<BurnedDisc>>(new Outcome<BurnedDisc>.No(Refusals.BurningIsNotInThisVersion()));

    public Task<Outcome<string>> BlankIt(Recorder disc, IProgress<EraseProgress>? progress, CancellationToken ct) =>
        Task.FromResult<Outcome<string>>(new Outcome<string>.No(Refusals.BurningIsNotInThisVersion()));

    public bool ShutIt(Recorder drive) => false;

    public bool CanShutItsOwnTray(Recorder drive) => false;
}
