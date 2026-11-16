# DiscWright for Linux

Read this first. It carries the decisions and working rules from where this repo
was started, so work can continue on any machine.

## What this is

A Linux version of [DiscWright](https://github.com/lazardjokovic/discwright), which
turns a GOG offline installer into a burnable game disc: the game's own icon and
title in Windows Explorer, a menu on double-click, and the game's name and icon on
a Linux desktop.

Related repositories:

- `lazardjokovic/discwright` - the Windows app, a PowerShell/WinForms script. **It
  is the reference.** When the two tools disagree about what a disc contains, this
  repo is the one that is wrong.
- `lazardjokovic/discwright.com` - the website. Its `site/index.html` names the
  current Windows version in one place and has to be bumped on every release.

Both repos carry a `ROADMAP.md`, and which one an item belongs in follows the
same rule as the code: anything about **the disc** is the Windows repo's, because
the disc is the same one. This repo's roadmap is about **Linux**: what a Linux
machine can do with a disc, and how the tool reaches the people who use one.

## Decisions already made

Made on 2026-09-18, with the reasons, so they are not reopened by accident:

1. **Python.** Present on every Linux desktop, readable by the people who will run
   it, and GTK4 gives it a native window later. Standard library only where
   possible; add a dependency only for a real need (Pillow will be one, for icons).
2. **Command line first, but the window is the app.** The command came first as a
   build order, not as a statement of what this is: the disc was the hard part and
   the part that had to be proven, and a command is the shortest way to prove it.
   What people use is the window, here as much as on Windows, and the command line
   is for scripting a disc or building one over SSH. Docs lead with the window,
   anything that makes the window harder to install or to understand is a bug in
   the same sense a wrong disc is, and a feature is not ported until the window
   offers it (2026-09-27, from the owner).
3. **The same disc as Windows.** Not a Linux-flavoured disc. A disc burned on
   either machine must behave identically on both: AutoRun menu, icon and label on
   Windows; name and icon on Linux. GOG's Linux `.sh` installers are a later
   feature on top of that, not part of the first milestone.

## How the port is done

- **Port the Windows function, and its reasoning.** Every function in
  `DiscWright.ps1` carries a comment explaining a failure it was written to avoid.
  Keep those in the docstring; they are why the code looks the way it does.
- **Golden files from a real Windows build.** `tests/fixtures/windows-0.7.2/` holds
  files Windows DiscWright 0.7.2 wrote, copied byte for byte off a real disc. Tests
  compare against them. `.gitattributes` keeps git from touching their line
  endings: `autorun.inf` is CRLF on purpose.
- **Port the Windows test cases too.** The Windows suite is
  `tests/DiscWright.Tests.ps1` in the other repo. Porting a function means porting
  its tests, so both tools agree on the same inputs.

## Facts about the disc, already measured

- **Volume id:** at most 16 of `A-Z a-z 0-9 _`, trimmed of underscores after the
  cut, `DISC` if nothing survives. Not what Windows shows; Explorer shows the full
  label from `autorun.inf`.
- **`autorun.inf`:** CRLF with a trailing CRLF, system ANSI codepage
  (Windows-1252 in practice), no BOM. AutoRun has no Unicode mode.
- **`.xdg-volume-info`:** UTF-8, no BOM, LF. A BOM makes GKeyFile read nothing.
- **Filesystems.** Windows DiscWright writes UDF 2.50 through IMAPI, plus ISO9660
  and Joliet when "readable on Windows XP and older" is ticked. IMAPI refuses any
  file over **2 GiB** in ISO9660 (measured; 0.7.2 fixed the app's wrong 4 GiB
  assumption). That 2 GiB is IMAPI's limit, not the format's: ISO9660 itself holds
  a file up to one byte under 4 GiB, and GOG splits its installers one byte under
  that.
- **xorriso writes the disc.** Measured in `docs/xorriso-spike.md`: an ISO built by
  xorriso from the folder Windows DiscWright staged mounts on Windows with the same
  label, Explorer reads its `autorun.inf`, and all 12 files are byte-identical,
  including a 4 GiB installer part. It is ISO9660 + Joliet rather than UDF, which
  also makes it readable on Windows XP, something the Windows tool cannot offer
  for a game that came in parts. Command:
  `xorriso -as mkisofs -iso-level 3 -J -joliet-long -r -V <volume id> -o <iso> <folder>`.

## Working here

Developed on Windows through **WSL2 (Ubuntu 24.04)** so far; works the same on real
Linux.

```sh
python3 -m venv ~/.venvs/discwright
~/.venvs/discwright/bin/pip install -e ".[dev]"
~/.venvs/discwright/bin/python -m pytest
```

xorriso is needed for anything that builds an ISO: `sudo apt install xorriso`.

- **Real GOG downloads.** Tests marked for them are skipped unless
  `DISCWRIGHT_GOG_DIR` points at a folder holding one GOG download per subfolder
  (`Alan Wake`, `Dead Space`, `Hollow Knight`, `The Witcher` on the original
  machine, where it was `F:\DWdemo`, `/mnt/f/DWdemo` from WSL). Their expected
  values are what Windows DiscWright 0.7.2 said about the same folders.
- **The staging comparison** also needs `DISCWRIGHT_STAGE_DIR`: somewhere to
  stage on the same filesystem as the real installers, so they are hard-linked
  rather than copied. On the original machine the installers are on `C:` behind
  junctions in `F:\DWdemo`, so it was `/mnt/c/Users/lazar/AppData/Local/Temp/dwlinuxstage`.
  The reference is `F:\DWdemo\out\disc`, which Windows DiscWright staged from
  the settings in `F:\DWdemo\out\discproject.json`.
- **`tools/mutate.py`** breaks the code in ways a real mistake would and fails if
  any breakage gets past the suite. CI runs it. Add a mutation when adding a rule
  worth protecting.
- **`tools/wsl-test.sh`** runs the suite from WSL when the repo lives on the
  Windows side, with the virtual environment kept on the Linux side.
- **`.claude/skills/goldens`** is the procedure for remaking the files this port
  is tested against, all of which Windows DiscWright itself produces: the menu
  template, the reference menus, the project file and the background. Read it
  before touching anything under `tests/fixtures`, and note that
  `tools/windows/Make-ProjectReference.ps1` now writes the project fixture that
  used to be made by hand.

What WSL cannot show: a Linux desktop mounting the disc and displaying its name
and icon, because WSL has no desktop automounter. That needs a real Linux machine
or VM, once per change to the disc's identity files.

## Working rules

These are the owner's, carried over from the Windows project. Follow them here.

- **Test everything before a merge, and audit for gaps honestly.** When asked
  whether something is tested, list what the change touches and what proves each
  part. Prefer closing a gap over noting it. Say which claims rest on a local run
  and which on CI.
- **Prove a test can fail.** Break the code on purpose and watch the matching test
  fail before trusting a green run.
- **Say when the machine is in use and when it is free**, if a run takes over the
  desktop or runs long.
- **One PR at a time.** Merge, wait, update the next branch, then merge it.
  Merging two at once left one behind main.
- **`main` is protected, the same as in the Windows repo.** Changes arrive only by
  a squash-merged PR, with all four required checks (`pytest (3.10)`,
  `pytest (3.12)`, and since 2026-11-16 `windows suite` and `linux suite`)
  passing on a branch that is up to date with `main`. No force pushes, no
  deleting `main`, and `v*` release tags cannot be moved or deleted once pushed.
  If a CI job is renamed, the required check in the `main` ruleset has to be
  renamed with it, or every PR waits for a check that never reports.
- **Commits** end with `Co-Authored-By:` naming the model that wrote them, as in
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`, Fable in a Fable
  session and Opus in an Opus one, and nothing else: no session links. **PR descriptions** end with
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- **Keep the owner's real name out of anything public.** The published identity is
  the project's own: the winget publisher is `DiscWright`, not a person.
- **Thank people when crediting them**, in the same sentence as what they did, and
  link every mention of their handle with the platform named. Ask before naming
  anyone publicly.
- **Text written for the owner to send to other people** (Reddit, forums, email)
  uses no dashes between clauses, neither ` - ` nor `—`. Commas and full stops
  instead. It has to sound like him.
- **A release is not done until every place that names the version says so**,
  including the website.

## Where things stand

**Right now (2026-11-15): the Linux version is being rebuilt in C#, on the
Windows app's own code.** The owner decided it that day, after more requests on
Reddit. The Python port below followed the PowerShell app, which has retired:
DiscWright 2.0 is C#, and it refuses a project file the Python port writes.

- **`app/` is a copy of `lazardjokovic/discwright`** at the commit named in
  `WINDOWS-MAIN`. Its `Core`, `Menu`, `Staging` and `ViewModels` already build
  on Linux, and the Linux work grows beside them: Linux edges over xorriso and
  an Avalonia window. When Windows `main` moves, a pull request here catches
  up and changes `WINDOWS-MAIN`.
- **The `discwright` repository is not touched for Linux work** until this is
  done and moved there, the way the C# rewrite was moved. Whether it lands on
  `main` or a branch, and which version number it takes, is decided then.
- **The Python port stays where it is and its checks stay green** until then,
  because `pytest (3.10)` and `pytest (3.12)` are required on `main`.
- **Pull requests are merged when the Linux VM and CI are green**, without
  waiting for the owner, each with screenshots or GIFs where there is anything
  to see. Nothing is tagged or released without his word.
- **The commit line names the model that wrote the commit**, Fable in a Fable
  session and Opus in an Opus one.

**Where the C# work has got to (2026-11-15):**

- The plan, the first spike and the inventory of the Windows app are on the
  original machine in `C:\Users\lazar\DiscWright-Lab\linux-plan`
  (`ARCHITECTURE-linux.md`, `spike-1.md`, `inventory.md`).
- **`app/tests/DiscWright.LinuxTests`** compiles the Windows suite's own test
  files for Linux, where they sit. What it leaves out is listed in its project
  file with reasons. `waiting-on-linux.txt` beside it names the tests that
  compile and do not pass on Linux yet, and `the-suite.py` fails the run when
  one of them starts passing, so a fix has to take its line out. Build that
  project, then run `python3 the-suite.py`.
- **`app/src/DiscWright.Machine`** is the plain .NET half of `Platform`,
  compiled a second time for `net10.0` without moving a file.
- **Files in `app/` are not moved or renamed**, so that catching up with
  Windows `main` stays a clean diff. New things are added beside the old.
- Fixed so far in shared code: a place on the disc lands in a real folder on
  Linux (`BuildContext.On`), and a name on the disc is held to Windows' own
  forbidden characters (`WindowsFileNames`).
- **`app/src/DiscWright.Linux`** holds the Linux answers, each behind the port
  the Windows one is behind: `VersionResourceFacts` (an installer's name, which
  .NET cannot read on Linux), `XorrisoImageMaker` (the ISO, with `IRunPrograms`
  as the seam its tests play xorriso back through), `SkiaImages` (the icon and
  the menu background, with SkiaSharp, the first dependency). `JolietNames` in
  `Core` refuses names Windows could not read off such a disc.
- The file system, the stage writer and the hasher need no Linux version: the
  Windows ones are plain .NET and pass their tests on Linux through `Machine`.
- **The tests written in Windows paths stay as they are until the move.** They
  are most of `waiting-on-linux.txt`. Rewriting them means editing 56 of the
  Windows app's test files here while another session changes them there. New
  tests are written in the machine's own paths and run on both systems.
- **A run on the VM sends what is committed** (`git archive HEAD:app`), never
  the working folder. An ignored file once passed there and failed in CI.
- A whole disc is built on Linux by one test (`AWholeDiscWithoutWindows`),
  and an image made that way mounts on Windows with every file intact. Nobody
  has yet watched its menu run.
- **`app/src/DiscWright.LinuxApp`** is the window, Avalonia 12, the twin of
  `DiscWright.App` over the same view models. So far it is the window itself
  and nothing in it. **`app/tests/DiscWright.LinuxAppTests`** opens it for
  real with no screen and draws it with real Skia; it is an xunit v3 project
  because Avalonia's test support is, so it runs as its own step and is not
  counted in the number `DEVELOPING.md` gives.
- **SkiaSharp in `DiscWright.Linux` is the version Avalonia brings**, and has
  to follow it. Two versions in one app passed every test and died as the
  published file started. `tools/see-the-published-app-start.sh` publishes
  the one file and looks for its window, and CI runs it under `xvfb-run`.
- The third spike (size, start-up, KDE's folder picker, pressing a button
  with no screen) is written up in `DiscWright-Lab\linux-plan\spike-3.md`.
- A project's name has no second dot (`LinuxApp`, not `App.Linux`): `TheReadme`
  reads the table of projects with a pattern that stops at one.
- **The look** is `Look.axaml` and `LookInTheDark.axaml` in `LinuxApp`: the
  Windows palette, its text styles as classes (`Classes="Title"`) and its
  buttons as control themes (`Theme="{StaticResource Ordinary}"`), by the
  same names. `TheLookOnLinux` reads the Windows app's own palette files and
  holds the Linux one to them. The window follows the desktop's light or
  dark scheme, live, seen on KDE. The face is Inter, carried in the app.
- **Ink, size and face are set on the window and inherited**, never by a
  style on `TextBlock`: that reaches the words inside every button and beats
  what the button asked for.
- **A fault planted with a pattern across two lines does not plant on the
  VM**, where the files arrive with CRLF. Plant by position in the file, and
  have the script say NOT PLANTED when nothing changed.
- **`pkill -f` with the app's path kills the shell that typed it**, since the
  path is in that shell's own command line. `pkill -x DiscWright.Linu`.
- **The controls are ours too**: the text box, checkbox, dropdown and its
  rows, the tree and its rows, list rows, the drawer and the scrollbars are
  control themes in `Look.axaml`, each used in `TheControlsOnLinux` with the
  pointer and the keyboard as well as looked at. The tree steps in by
  nineteen the Windows template's way (a nineteen wide opener column and a
  nineteen margin), not Avalonia's.
- **`IsEffectivelyVisible` is true of a control that was never put on a
  window.** Ask `CanBeSeen()` in `WhatWasDrawn`.
- **A fault planted by swapping a state for `:focus` is no fault where the
  test clicks**, because clicking focuses. Swap for `:pressed`. And move the
  pointer away before judging what the keyboard did.
- **The picture code opens its own files and hands Skia bytes.** The Windows
  suite once failed in CI with "background.png is being used by another
  process" on a temp folder being cleared. Never reproduced, cause not
  proven. If it comes back the picture code is ruled out.
- `OffTheRow`, the cross at a row's right edge, is not carried yet: it is
  shown by the row being under the pointer, and wants the real rows to test
  against. It comes with the tree in the main window.
- **`TheLinuxMachine` in `LinuxApp` is the composition root**, the twin of
  what `App.xaml.cs` builds on Windows, port for port. The window is handed
  `MakeADisc`, the same screen as on Windows, and shows the name and the
  version from it. The log lands in `~/.local/share/DiscWright/logs`.
- **Drives, burning, erasing and the tray are `NothingBurnsYet`** until
  there is a recorder over xorriso: no drives, and a refusal that says this
  version does not burn and names K3b, Brasero and Xfburn. It is a fact about
  DiscWright and not about the machine, so it is not `ThereIsNoDiscWriter`.
- **The autorun codepage follows the machine's locale**, as on Windows, and
  is not fixed at 1252 as the plan said. The owner has not been asked.
- **The window's tests are stamped 0.0.1 on purpose.** Stamped like the app,
  an app that read its version from whatever started the process passed.
- **A build made from `git archive` carries no commit after the plus**, so
  anything about the commit has to be asked of a function, not the assembly.
- The window's tests run over the real Linux machine (`TheWindow`). Fakes
  for a screen a test can steer are still to be written: the Windows
  suite's are private classes inside its test files.
- **`WorthAsking` and `SayingSo`** are the two small windows in front of the
  main one. `WorthAsking` is the Windows one, awaited instead of blocking,
  with the cross as the other answer. `SayingSo` is what a message box is on
  Windows, and a refusal in it can be read by a test.
- **An awaited answer in a test gets a time limit** (`WaitAsync`). A window
  that never shuts otherwise hangs the whole run and reports nothing.
- **A key released on a window that shut on the press throws.** Release only
  if the window is still showing.
- **The main window's top line is on**: the look, Open the log, Start a new
  disc, Open one you built, Save it for later, each a handler from
  `MainWindow.xaml.cs` with its words. A handler that asks a question is
  `async void` and awaits it.
- **`IAskTheDesktop` is the window's one seam**: a file from somebody, and a
  thing opened in whatever opens it. `TheDesktop` is the real one, and
  tests answer with `ADesktopThatAnswers`. Folders will come through it too.
- **The opener starting is not the opener working.** `TheDesktop.Open` runs
  xdg-open itself, gives it two seconds, and counts a quick exit with
  anything but nought as not opened, so the window can say where the thing
  is. Found by pressing the link on the real desktop, not by a test.
- **An app started over ssh has none of the session's own variables.**
  xdg-open needs `KDE_SESSION_VERSION=6` to find Plasma's opener. Set it,
  and `KDE_FULL_SESSION=true`, when driving the real window on the VM.
- **What the window keeps survives between runs on the VM**
  (`~/.local/share/DiscWright/appearance.txt`). Remove it before a run that
  presses by position, or the words have moved.
- **The look's control says "following the desktop" here**, through a third
  argument to `ChoosingTheLook`. What is kept in the note is unchanged.
- **Not on the top line yet: the link that asks about a newer version.** It
  asks about the Windows app's releases, and what a Linux DiscWright's
  version is has not been decided. The owner has to say.
- **`AFaultNobodyExpected`** hears a fault inside a press through the
  dispatcher, writes it down whole, says it in a box and lets the program
  carry on, as the Windows app does. It is what catches a fault in an
  `async void` handler, which has no caller to land on.
- **How Add a game will ask its questions** (decided 2026-11-16, not built):
  `TakingFoldersOn` asks through four plain functions that return on the
  spot, which WPF's blocking dialogs allow. Here the walk runs on a worker
  thread, each question is put to the window's thread and the worker waits
  for the answer, and each step is applied on the window's thread. No change
  to shared code or its 25 tests, and the disk is read off the window's
  thread. A nested dispatcher frame would be closer to WPF and cannot be
  answered by a headless test, which is stuck inside the press.
- **The list of what goes on the disc is on the window**: the tree over
  `WhatGoesOnIt` with the Windows row (tick, name at the row's own width,
  kind, size in one column, the cross), the line that adds it up, and the
  six lines under it. Picking, the cross, Delete, the link that takes
  unticked rows off, and renaming in place all work.
- **A row of Avalonia's tree keeps a double press to itself.** It opens and
  shuts on one and says the press is dealt with, so a `DoubleTapped` written
  in the markup never fires. The window listens with `handledEventsToo`.
- **A text box here keeps every Delete**, even with nothing left to delete.
  The check the Windows window needs for that did nothing here and was taken
  out after a planted fault showed it.
- **The cross is shown by `StackPanel.ARow:pointerover`**, the row's own
  line, as two styles at the foot of `Look.axaml`. A tree row is under the
  pointer whenever anything inside it is.
- **A file in a GOG download that is not the installer or one of its parts
  is not part of the game.** It is "left behind", and asked about. A test
  that wants a long name on a row has to give it to the installer.
- **A shell command with an apostrophe in a heredoc after another heredoc
  can fail to parse, and then nothing in it runs.** Write the script, the
  commit message and the pull request's words to files and name the files.
- Not on the list yet: Add a game and the preview button above it, drag and
  drop, the link that turns a manual into an extra, and the picked game's
  own manual and extras. Games reach the list in tests through the screen.
- **`WhatElseWasInThere` in `ViewModels`** is everything the "what else was
  in that folder" question knows, with no window in it, and with tests in
  the shared suite. On Windows the same logic is inside the dialog's code
  and untested. The Windows dialog is not touched and keeps its own copy
  until the two apps are brought together.
- **All four question windows exist here**: `WorthAsking`, `SayingSo`,
  `WhatElseIsInThere` (over the class above) and `PickOne`. Each is awaited.
  `WhatElseIsInThere` writes its question and answer to the log, which the
  Windows one does not.
- **A double press on a list takes a row on the second press going down**,
  so the window is gone before the pointer comes up. A test that sends the
  release anyway throws on a disposed window.
- **Add a game works**, from the press and from folders named on the
  command line. The walk runs on a worker thread and comes back to the
  window's thread for every question and every step (`OnTheWindow`), as
  decided above. One walk at a time: a second press while a question stands
  is let pass.
- **A test of anything that walks on a worker waits for a thing, not for a
  time.** `Until(() => ..., "what it was waiting for")` in
  `AddingAGameOnLinux` runs the window's thread until the thing is so and
  names it when five seconds pass without it.
- **An add-on hangs under its game. It is not a second game.** `Games` still
  counts one, and the add-on is a row in the tree.
- **A folder of games added as one game goes on to ask which file starts
  it**, since the installers in it are files that could.
- **A disc folder DiscWright built, sitting inside a folder of games, is
  offered as a game**, with `autorun.inf` and `Start Here.hta` as extras.
  That is shared code, so the Windows app does the same. The owner has been
  told and has not decided.
- **The question windows show a minimise button on KDE.** The Windows ones
  show none. Not looked into.
- Not on the window yet: drag and drop onto the list, the preview button,
  the link that turns a manual into an extra, the picked game's own manual
  and extras, and the version link.
- **A disc can be built from the Linux window** (2026-11-16). The right hand
  column (the name, the size, splitting, where it goes, More options) and
  the bottom line (what it will make, why not and the way out, a worry and
  its way out, Build it) are on. `BuildingFromTheLinuxWindow` presses Build
  it and reads the image back, and it was done by hand on the VM's KDE
  desktop with the published file.
- **Avalonia's text box has no setting for capitals.** The name is put in
  capitals as it is typed, by `KeepTheNameInCapitals`, cursor kept.
- **What the bottom line says the button means is worded for Linux**: this
  version does not burn, and K3b, Brasero or Xfburn will burn the ISO. There
  is no Burn or Erase button, and no drive picker.
- **"Readable by Windows 98 and Mac OS 9" is not on the Linux window and is
  not coming**: a disc made with xorriso is laid out the older way already.
- **The VM's screen is 768 tall and the window is drawn at 740.** To press
  the bottom line by position, make the window shorter first
  (`xdotool windowsize`), and read the height it really took. Fitting the
  window to the screen (`OnThisScreen` on Windows) is not carried yet.
- **The preview button is on the window, dead, and says why on hover**
  (`ToolTip.ShowOnDisabled`): the menu is a Windows page and nothing on
  Linux shows one. The owner's decision of 2026-11-15.
- **Fitting the window to the screen is written and parked**, on the branch
  `fitting-the-linux-window-to-its-screen`, not merged. On KDE the window
  opens at 920 by 686 on a 768 tall screen with or without it (measured both
  ways with the published file): Plasma's window manager shrinks a window to
  fit by itself. A line that changes nothing on any desktop there is to try
  it on is not merged. Try the branch on GNOME when there is one.
- **The window for artwork, music and extras is on** (`WhatElseGoesOnIt`),
  opened from the main window: the icon, the menu background or a colour,
  music, a manual, extras, and the cover and disc face. A picture is looked
  at as it is chosen and refused then, with the reason, if it is not one.
- **The window's tests run on the Windows runner too**, as a step of
  `windows suite`. They draw with no screen. A file held open after it was
  shown can only be noticed there, and was planted and caught there.
- **A run on the VM is judged by its exit code, taken before anything is
  piped.** `run | tail && next` runs `next` whatever happened: on 2026-11-16
  a pull request was opened over a red suite that way.
- **`WhatADiscIsReadBackAgainst.Says_which_disc_of_the_set_it_is_working_on`
  fails about one run in fifty on Linux**, twice so far (#32 on GitHub, #50
  on the VM). It is the Windows app's own test, about a progress report
  arriving while a disc is read back. Not looked into, not fixed.
- **A game's own manual and extras are on the window**: while a game is
  picked, the line under the list names it and offers Add a manual and Add
  extras. A row that is a game's own says what it is as a link, and a press
  makes the manual an extra or back.
- **A game's own sit in a group under the game that starts shut**, and a
  press on that link moves the row to the other group, which may be shut
  too, so the row can go from under the pointer. Shared code. A test that
  wants the row has to open down to it each time (`OpenDownTo`).
- **A branch stacked on an open pull request carries that pull request's
  commit.** If the commit is amended before it is merged, the stacked
  branch conflicts on rebase: `git rebase --skip` the old one.
- **A folder dropped on the list is taken on**, and so is the installer's
  own exe, which brings its game. Seen on the VM's KDE desktop: a folder
  dragged out of Dolphin onto the empty list, and again over the rows, where
  it was added once and not twice.
- **A real drag is done with xdotool in steps**: `mousedown`, thirty small
  `mousemove`s, `mouseup`. A pointer that jumps is not a drag. Put the two
  windows side by side and take a picture first, to read off where the
  thing to drag is.
- **In Avalonia whether an element takes drops is inherited** from what it
  sits in. Saying so on the panel is enough for the tree. On Windows the
  tree has to say so itself.
- **The line that listens for the drop is held by no test.** Nothing
  headless can drop a file. Taking that line out passes every test, and
  only the real drag shows it.
- **A sentence on the window is a promise.** The empty list said dropping
  worked for seven pull requests before it did, copied from Windows with a
  test holding the words. Read what a copied sentence claims.
- **The Linux name and icon are a disc option** (`LinuxNameAndIcon`, the
  project file's old `LinuxInfo` key): `.xdg-volume-info` at the top and the
  icon as a PNG beside the Windows one. On for a new disc on Linux through
  `TheMachine.ANewDiscStartsWith`, off on Windows. The tick is on the Linux
  window only. **The Windows window has no tick for it yet**: that comes
  when the two apps are brought together, and until then a project saved on
  Linux with it on builds with it on Windows and cannot be unticked there.
- **The PNG is the largest frame of the icon already on the disc**, taken
  out of the file and written by `APngFile`, which is plain .NET: no
  drawing library, the same on both machines. With no icon chosen it is
  DiscWright's own, `AUTORUN/menu.png`, 64 across.
- **Seen on the VM (2026-11-16):** the image mounted through udisks, and
  GIO (gvfs 1.60) names the mount by the file, "FIXTURE GAME: DIRECTORS
  CUT" where the volume is `FIXTURE_GAME_DIR`, and gives the PNG as its
  icon. Dolphin on the same machine shows the volume id and a plain disc:
  KDE does not read the file, and the tick's hover says so. `gio mount -li`
  does not print an icon that is a file. Ask `Gio.VolumeMonitor` from
  python3 (`m.get_name()`, `m.get_icon().to_string()`). Not seen: a GNOME
  desktop drawing it.
- **A test disc goes in over ssh** with `udisksctl loop-setup -r -f the.iso`
  and `udisksctl mount -b /dev/loopN`. A polkit rule on the VM
  (`/etc/polkit-1/rules.d/49-dw-test-discs.rules`) lets `dw` do that with
  nobody at the screen. gvfs was installed there for this.
- **`While_a_disc_is_being_built_nothing_that_would_change_it_can_be_pressed`**
  walks the window during a run and names everything alive. A new box
  needs no line there; one that stays alive on purpose goes in
  `StillAliveDuringARun` with its reason.
- **Do not drive the VM's desktop while a fault run has it loaded.** Xorg
  fell over in its display driver on a pointer event at load 16, and the
  session came back with no panel. `sudo systemctl reboot` and forty
  seconds put it right.
- Not on the window yet: the drive picker and what is in the drive, and
  the version link.
- Next: the recorder, which needs a real machine with a drive; packaging
  (AppImage and tarball); and a GNOME desktop to look at.
- The Linux VM is `DiscWright-Linux`, Ubuntu 26.04 with KDE, .NET SDK in
  `~/.dotnet`. Send `app/` to it with tar over `dwlinux.sh` and run the suite
  there before every pull request.
- **Typing a backslash into a shell heredoc halves it or breaks the script**,
  here as in the Windows repo. Write the script to a file with forward slashes.

Everything from here down is the Python port, kept as the record of what was
measured.

**Before that (2026-09-30): testing on real Linux, before any release.** Every
part of the Windows app's disc-making is ported, with the command line and the
window, and all of it passes in WSL and CI. What has never been seen is a real
Linux desktop. Arch came off the dual boot and **Kubuntu is the machine**.

### If you are the session on Kubuntu, start here

Nothing is half finished and nothing needs writing before testing can start.
`main` is green, there are no open pull requests, and the work is to find out
what a real KDE desktop does with this.

```sh
sudo apt install git python3-venv python3-gi gir1.2-gtk-4.0 python3-pil xorriso \
                 fonts-dejavu-core nodejs gh desktop-file-utils
git clone https://github.com/lazardjokovic/discwright-linux
cd discwright-linux
python3 -m venv --system-site-packages .venv     # the flag matters, see the README
.venv/bin/pip install -e ".[dev]"
tools/linux-report.sh                            # --mutate for the full run
```

`tools/linux-report.sh` writes down what the machine is, runs the suite, builds
a disc end to end and leaves a real ISO in `~/discwright-check/out/`. Then
`docs/testing-on-linux.md` is the list, and its by-hand section is the part no
script can answer: **what the desktop shows for a mounted disc**, whether the
application menu entry appears with its icon, and what the folder question
looks like on KDE's own dialogs. Screenshots beat sentences for all three.

Three things to expect rather than be surprised by:

- **KDE may ignore `.xdg-volume-info` entirely.** GNOME reads it through gvfs;
  KDE mounts with Solid. If the disc shows its volume id and a generic icon,
  that is a fact to write down, not a bug to rush a fix for.
- **The file dialogs will be Plasma's**, through the desktop portal, not GTK's.
  That they open and return the folder picked is the check.
- **The window will not look like Breeze**, since GTK 4 uses whatever GTK theme
  is set. Worth a screenshot, not worth a fix.

**Do not release, tag or bump a version.** The release waits until the owner has
seen the results. Anything that does need fixing is fixed the way everything
here is: a test that fails first, then the fix, in a pull request.

**Level with Windows 0.9.1 for what this port does, as of 2026-10-02**, which is
not the same as having everything 0.9.1 has. Two things came across and two
deliberately did not.

Came across: a disc built from folders of game files now shows **Play from disc**
and no Install, because the executable is the game. Windows found that by burning
a disc and looking at the screen, and the port carried the same template and the
same bug. And the project file is at **schema 10**, carrying `CoverPath` and
`DiscArtPath`. Nothing here prints anything; they are read and written so that a
project opened and saved on Linux does not come back with somebody's choice
quietly gone.

Did not come across: **printing** and **burning**. Printing means rendering the
whole format table with Pillow; burning on Linux is a different world of
`growisofs`, `cdrecord` and `wodim`. Both are projects rather than ports, and
neither is worth starting while this is paused and nobody is running it.

**Caught up with Windows 0.8.0 (2026-09-26), and finished since:** a game no longer has to be a GOG
download. `folder_info` in `games.py` takes any folder of game files and keeps
its shape, `layout.py` places each file by its path under that folder rather
than by name, the menu offers **Open Folder** where there is nothing to install,
and the project file records which kind each entry is, at schema 9. The command
line asks with `--files` and `--installer`, since it cannot show a dialog, and
`--game` prints what to type when the folder it was given is not a GOG download.
The window asks the question Windows asks in a dialog, with its wording in
`form.py` so it can be read without a screen.

Since then, and all on `main`: the README says how to **install** it, with the
steps run on Ubuntu 24.04 rather than written from memory; `packaging/desktop`
puts DiscWright in the **application menu** with its own icon;
`tools/linux-report.sh` collects the machine, the suite and a built disc into
one report; `ROADMAP.md` says what a Linux machine can and cannot do with a
disc; and the build now **says when the disc's own files land on top of a
game's**, which the Windows app does too.

- Ported and tested against Windows output: `text.py` (control characters, volume
  id, ANSI encoding), `autorun.py`, `xdg.py`.
- `games.py` reads a GOG download folder: the installer, its parts, gaps in the
  part numbering, and the game's name. `pe.py` reads that name out of the
  installer's version resource with the standard library. Both agree with Windows
  on the four real GOG downloads. Matching is case-insensitive on purpose, since
  Windows gets that for free and Linux does not.
- Add-ons (DLC, patches, mods) are read by `add_on_info` in `games.py`. Any
  `.exe` is accepted, not just `setup_*`, and the name comes from the file name
  rather than the version resource, because every GOG patch reports the base
  game's name there. A patch is named by the version it moves **to**, at the
  front, so two patches of one game do not read the same on a clipped button.
  Agrees with Windows on all seven real GOG patches.
- Which game an add-on belongs to (`parent_index`) is set by the caller.
- `layout.py` holds the rules for where everything goes on the disc: the icon's
  name, reserved names at the root, numbered game folders, add-ons filed under
  their game, and the menu's view of all of it. Pure rules, no files touched.
  Paths on the disc are written with backslashes, because the menu runs on
  Windows. Name comparisons ignore case, because the disc is read on Windows too.
- **A Windows bug found while porting, now fixed on both sides:** Windows
  DiscWright 0.7.2 cut a game's folder name at 48 characters and re-trimmed only
  whitespace, so a cut landing on a dot left a trailing dot. Windows drops
  trailing dots from folder names silently, so the folder and the menu's path
  disagreed and that game's Install button pointed at nothing. `layout.py` trims
  dots after the cut, and Windows DiscWright 0.7.3 does the same.
- **Porting can find bugs in the reference.** When the Windows app does something
  that cannot be right, measure it, fix it here, and fix it there too, as a
  separate PR in the Windows repo with a test that fails first. Do not copy a bug
  just because the reference has it.
- `stage.py` lays the disc out as a folder: installers hard-linked where
  possible, each entry's own manual and extras, the disc icons, the menu
  background, the disc-wide manual and extras, music, the menu, extra content
  behind the reserved-name guard, `autorun.inf` and `.xdg-volume-info`. An old
  disc folder is set aside, never wiped, because the build may be reading from
  it. It stages every one of the 12 files Windows staged for the Alan Wake demo:
  10 byte for byte and 2 as the same picture (`SAME_PICTURE` in
  `tests/test_stage.py`). The file lists must match exactly, so anything a later
  Windows version adds to the disc shows up there.
- One deliberate difference from Windows: settings that cannot be built (no
  icon, one that is not a readable image, a menu with no background or an
  unreadable one) are refused **before** anything is copied. Windows only finds
  out after the installers are in place.
- `icons.py` checks a picked icon or background, and makes the disc's two icons:
  a seven-frame `.ico` assembled by hand to match Windows' structure exactly
  (256px frame as PNG, the rest as 32-bit bitmaps), and a 256px PNG for Linux.
  Pillow is the first dependency. Bytes cannot match Windows', so tests check
  the structure exactly and the picture within a tolerance chosen by
  measurement (`tools/icon_diff.py`), ignoring the outermost pixel.
- **Two Windows icon bugs found while porting, both fixed in Windows 0.7.4:**
  the Linux PNG came out as noise (or failed, or blurred) for any icon with a
  256px PNG frame, which is most real game icons, and every icon frame had a
  see-through rim. The Alan Wake reference folder on the original machine was
  staged by 0.7.1, so `tests/test_stage.py` still compares the Linux PNG with
  the icon's own 256px frame; it can compare with the reference once that is
  restaged.
- `background.py` composes the menu background: the artwork scaled to cover
  760x480, darkened, a panel fading toward the buttons, an optional divider and
  an optional title. Tests compare it with what Windows 0.7.4 composed from the
  same two drawn pictures (`tests/fixtures/background`, made by
  `tools/background_sources.py` and
  `tools/windows/Make-BackgroundReference.ps1`), within tolerances measured by
  `tools/background_diff.py`. The title cannot match Windows' pixels: Windows
  draws it in Bahnschrift, which Linux does not have, so it is DejaVu Sans Bold
  here (or another common bold sans), placed by its ink where Windows' ink sits
  and held to the same height.
- **Two more Windows bugs found porting the background, both fixed in Windows
  0.7.5:**
  - **A light line along the top of the button panel.** GDI+ antialiases its
    rectangle fills with pixel centres on whole numbers, so a fill starting at 0
    covers half of pixel 0. The darkening and the panel are both drawn that
    way: the top row and left column get half the darkening, and so does the
    panel's first column. On bright artwork it shows as a one-pixel light line.
    `half_pixel_lines` in `tools/background_diff.py` names the lines; tests
    leave them out of the comparison, since the reference is 0.7.4's, and check
    this port darkens them fully. The divider's top pixel had the same defect.
  - **A long title runs off the menu.** Windows stops shrinking the title at
    12pt whether or not it fits, and nothing limits its length. "Warhammer
    40,000: Dawn of War - Game of the Year Edition" is 451px at 12pt with 416px
    of room: under the panel on the right, cut off at the menu's edge on the
    left. This port carries on down to 6pt when it has to.
- `menu.py` writes `AUTORUN/menu.hta`. The menu is **copied, not ported**:
  `src/discwright/menu.hta.in` is the Windows template, lifted verbatim out of
  `New-MenuHta` by `tools/menu_template.py`, and `menu.py` fills its
  placeholders with the same escaping. The output matches Windows byte for byte
  on every case in `tests/fixtures/menu/cases.json` (references written by
  `tools/windows/Make-MenuReference.ps1`) and on the real Alan Wake disc. When
  the Windows menu changes, rerun `tools/menu_template.py`, then the reference
  script, then the tests. The tests parse the menu's script with Node where it
  is installed (CI has it, WSL does not); on Windows, `cscript //E:JScript`
  parses it with the real engine.
- **A Windows menu bug found while porting, fixed in Windows 0.7.5:**
  `New-MenuHta` filled its placeholders with eleven chained replaces, so text
  already filled in was filled in again. A game named `Game %%BTNS%% Edition`
  got the button list pasted inside its string literal, and the whole menu
  failed to compile under JScript (measured). Unlikely, since GOG names never
  hold `%%`, but a game can be renamed to anything. Both sides fill them in one
  pass now. One Windows quirk **is** matched on purpose, because it is harmless and
  keeps the bytes equal: the window's application name keeps the Kelvin sign
  and the dotted capital I, which .NET's case-insensitive `[A-Za-z]` matches,
  and they reach the file as `?`.
- `iso.py` writes the ISO with xorriso, and `build.py` is the whole build:
  stage, then ISO, then the old disc folder goes. The command is
  `xorriso -as mkisofs -input-charset UTF-8 -iso-level 3 -J -joliet-long -r -V <id>`.
  Every flag there was measured, and `-input-charset UTF-8` is not optional: under
  a C locale, without it, xorriso silently wrote an accented name into the
  Windows names as underscores.
- **Windows reads this disc through Joliet, which holds less than a folder
  does, and xorriso cuts or changes a name it cannot hold without saying so.**
  So `name_problems` checks every name on the staged disc before anything is
  written, and the build stops naming each file: over 103 characters, a
  character Windows forbids or cannot store (emoji included), a trailing dot or
  space, a Windows device name, or two names differing only in capitals. The
  measurements behind each rule are in `docs/xorriso-spike.md`. Files over 4 GiB
  are allowed: Windows 11 read them back whole.
- **xorriso's own reader is not a witness for what Windows sees.** Asked for the
  Joliet tree it reported a name whole that the record on the disc held cut to
  64 characters. `tools/joliet.py` reads the records themselves, and the tests
  use it.
- The whole disc has been built end to end and compared with the Windows one:
  see the end of `docs/xorriso-spike.md`. Ten of the twelve files byte for byte,
  the other two the same pictures, same label, same name in Explorer.
- `cli.py` is the tool: `discwright build`, with one option per step of the
  Windows window. An add-on belongs to the `--game` named before it, which is
  why both go through one argparse action that keeps the order they were typed
  in. Everything that cannot be built is refused before a file is copied, each
  message naming what to do. `--stage-only` stops after the disc folder.
- `project.py` reads and writes `discproject.json`, schema 8, the same file the
  Windows app writes: a build saves one beside its ISO, and
  `discwright build --project FILE` rebuilds from it. Every older schema opens,
  and a key a file does not carry reads back as what that disc behaved like
  before the key existed, not as today's default. The file is UTF-8 **with** a
  byte order mark and CRLF, because without the mark PowerShell 5.1 reads it in
  the machine's ANSI codepage and mangles any path with an accent. Checked both
  ways: the tests read a real Windows file of schema 8 and one of schema 5, and
  `tools/windows/Read-Project.ps1` had the real `Import-Project` read a file
  written here, with every accent arriving intact.
- **A Windows bug found while porting, fixed in Windows 0.7.6:** a rebuild
  over an existing disc sets the old disc folder aside and rewrites every
  setting that pointed inside it, then deletes that folder once the ISO is
  written. `Save-Project` runs in between, so the project it saves names files
  in a folder that no longer exists: reopening it loses the icon or background
  picked from the disc being rebuilt. `build.py` points those paths back at the
  new disc folder, where the same files are, before saving.
- **The window is two files on purpose.** `form.py` holds every rule the window
  follows, with no GTK in it: what can be used right now and the sentence saying
  why not, what stops a build and what only needs a yes. `window.py` only draws
  it and passes clicks back, so the rules are tested without a screen. Where the
  Windows app leaves BUILD clickable and refuses in a dialog, this greys it out
  and says what is missing. `Test-ComposedBg` is in the window: picking a
  760x480 background ticks "use as it is".
- **The window's tests** (`tests/test_window.py`) draw it for real and skip
  without GTK 4 or a display. One walks every clickable widget and demands it be
  greyed out during a build, rather than asking the form: a widget nobody tied
  to a rule is exactly the one a list of rules misses. It found three on its
  first run: Add file..., Add folder... and the extra-content list.
  Dialogs are recorded in tests, not shown. CI runs them under `xvfb-run`.
  `tools/window_screenshots.py` draws the window in several states to PNGs,
  under Xvfb, without touching the desktop.
- WSL shows the window on the Windows desktop through WSLg: `discwright window`.
  Installing PyGObject into the venv (`pip install -e ".[dev,gui]"`) needs
  `libgirepository-2.0-dev libcairo2-dev pkg-config python3-dev` from apt.
- Next: the real-Linux checks in `docs/testing-on-linux.md`, then a first
  release, only when the owner says. Nothing has been published for Linux yet:
  no package, no version number but `0.1.0.dev0`.
- **Working with the owner, beyond the rules above:** say plainly when a run
  will take over the desktop and when it is free again; ask before anything
  that needs their password (`sudo`), their GitHub login, or deletes files they
  did not create in this session. A safety hook protects folders directly under
  a drive root; do not work around it, leave such a delete to the owner.
- Not ported, and probably never: the target-disc sizing (`Get-MediaFit` and the
  media tiers), which is a window's live recommendation rather than anything the
  disc carries.
