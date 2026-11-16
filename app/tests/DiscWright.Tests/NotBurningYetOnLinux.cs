using DiscWright.Core;
using DiscWright.Linux;

namespace DiscWright.Tests;

/// <summary>
/// What a Linux DiscWright says about drives and burning before it has a
/// recorder of its own.
/// </summary>
/// <remarks>
/// It is given to the window in place of a recorder, so everything the window
/// can ask a recorder is asked here. The answers have to be true on a machine
/// with a drive in it as much as on one without: this DiscWright not burning
/// is a fact about DiscWright, not about the machine.
/// </remarks>
public class NotBurningYetOnLinux
{
    private static readonly Recorder ADrive = new("id", "/dev/sr0", "HL-DT-ST BD-RE", 0, false, 0, []);

    [Fact]
    public void Finds_no_drives()
    {
        Assert.Empty(new NothingBurnsYet().All());
    }

    [Fact]
    public async Task Asked_to_burn_all_the_same_it_says_this_version_does_not()
    {
        Outcome<BurnedDisc> answer = await new NothingBurnsYet()
            .Burn("/home/somebody/Hollow Knight.iso", ADrive, null, CancellationToken.None);

        Assert.Equal(Refusals.BurningIsNotInThisVersion(), Assert.IsType<Outcome<BurnedDisc>.No>(answer).Refusal);
    }

    [Fact]
    public async Task Asked_to_erase_it_says_the_same()
    {
        Outcome<string> answer = await new NothingBurnsYet().BlankIt(ADrive, null, CancellationToken.None);

        Assert.Equal(Refusals.BurningIsNotInThisVersion(), Assert.IsType<Outcome<string>.No>(answer).Refusal);
    }

    [Fact]
    public void Does_not_claim_to_work_a_tray()
    {
        NothingBurnsYet nothing = new();

        Assert.False(nothing.CanShutItsOwnTray(ADrive));
        Assert.False(nothing.ShutIt(ADrive));
    }

    [Fact]
    public void The_refusal_is_about_this_version_and_not_about_the_machine()
    {
        Refusal refusal = Refusals.BurningIsNotInThisVersion();

        Assert.Equal("This version does not burn discs yet", refusal.Headline);

        // Not the sentence for a machine with no writer, which would be
        // false on a Linux machine that has one.
        Assert.NotEqual(Refusals.ThereIsNoDiscWriter().Headline, refusal.Headline);
        Assert.DoesNotContain("no disc writer", refusal.Because, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_refusal_says_the_image_is_safe_and_what_will_burn_it()
    {
        string because = Refusals.BurningIsNotInThisVersion().Because;

        Assert.Contains("The image is in the output folder, whole", because, StringComparison.Ordinal);

        // Named, because "use another program" sends somebody searching.
        Assert.Contains("K3b", because, StringComparison.Ordinal);
        Assert.Contains("Brasero", because, StringComparison.Ordinal);
        Assert.Contains("Xfburn", because, StringComparison.Ordinal);
    }
}
