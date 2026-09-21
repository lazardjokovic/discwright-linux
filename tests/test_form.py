from pathlib import Path

import pytest

from discwright.form import BUTTONS, CONTROL_NAMES, Form
from discwright.games import GameInfo
from discwright.layout import remove_entry
from discwright.project import save_project

from test_stage import BACKGROUND, ICONS, MB, sparse, src  # noqa: F401  (src is a fixture)


def entry(name, kind="Game", parent=-1):
    return GameInfo(ok=True, game_name=name, kind=kind, parent_index=parent)


# ---- removing an entry: the Windows tests for Remove-GameEntry, ported ------------------

def test_shifts_a_parent_that_pointed_past_the_removed_entry():
    # Remove B and C becomes 1, so the add-on has to follow it, or it silently
    # attaches to A.
    out = remove_entry([entry("A"), entry("B"), entry("C"), entry("Mod", "AddOn", 2)], 1)
    assert len(out) == 3 and out[2].kind == "AddOn"
    assert out[out[2].parent_index].game_name == "C"


def test_leaves_a_parent_below_the_removed_entry_alone():
    out = remove_entry([entry("A"), entry("Mod", "AddOn", 0), entry("C")], 2)
    assert out[1].parent_index == 0


def test_turns_an_orphaned_add_on_back_into_a_game_rather_than_deleting_it():
    out = remove_entry([entry("A"), entry("Mod", "AddOn", 0)], 0)
    assert [(e.game_name, e.kind, e.parent_index) for e in out] == [("Mod", "Game", -1)]


def test_ignores_an_index_that_is_not_in_the_list():
    assert len(remove_entry([entry("A"), entry("B")], -1)) == 2
    assert len(remove_entry([entry("A"), entry("B")], 9)) == 2


def test_survives_a_removal_that_leaves_nothing():
    assert remove_entry([entry("Only")], 0) == []


def test_takes_only_that_games_add_ons_when_asked():
    two = [entry("Alan Wake"), entry("Hollow Knight"),
           entry("AW patch", "AddOn", 0), entry("HK patch", "AddOn", 1)]
    out = remove_entry(two, 0, with_add_ons=True)
    assert [e.game_name for e in out] == ["Hollow Knight", "HK patch"]


def test_repoints_the_survivors_after_several_rows_go_at_once():
    # With a game and its two add-ons going, "one less if the parent sat after
    # it" does not hold, and a stale parent reattaches a patch to whatever game
    # slid into the gap.
    mix = [entry("Alan Wake"), entry("AW 1", "AddOn", 0), entry("AW 2", "AddOn", 0),
           entry("Hollow Knight"), entry("HK patch", "AddOn", 3)]
    out = remove_entry(mix, 0, with_add_ons=True)
    assert [e.game_name for e in out] == ["Hollow Knight", "HK patch"]
    assert (out[1].parent_index, out[1].kind) == (0, "AddOn")


def test_promotes_every_orphan_when_not_asked_to_take_them():
    five = [entry("Hollow Knight")] + [entry(f"Update {c}", "AddOn", 0) for c in "ABCD"]
    out = remove_entry(five, 0)
    assert len(out) == 4 and all(e.kind == "Game" and e.parent_index == -1 for e in out)


# ---- a form that can build, for the rules below to take apart ---------------------------

@pytest.fixture
def ready(src, tmp_path):
    f = Form()
    assert f.add_game(src / "Alpha") is None
    f.icon = src / "art" / "alpha.ico"
    f.background = src / "art" / "alpha-bg.png"
    f.out_dir = tmp_path / "out"
    return f


def test_a_complete_form_can_build(ready):
    assert ready.missing() == []
    assert ready.controls()["build"].enabled


# ---- the label -----------------------------------------------------------------------

def test_names_the_disc_after_the_first_game(src):
    f = Form()
    f.add_game(src / "Alpha")
    assert f.label == "alpha" and f.label_is_seeded()


def test_leaves_a_label_the_user_typed_alone(src):
    f = Form(label="MY DISC")
    f.add_game(src / "Alpha")
    assert f.label == "MY DISC" and not f.label_is_seeded()


def test_clears_its_own_label_when_the_last_game_goes(src):
    f = Form()
    f.add_game(src / "Alpha")
    f.remove(0)
    assert f.label == ""


def test_keeps_a_typed_label_when_the_last_game_goes(src):
    f = Form(label="MY DISC")
    f.add_game(src / "Alpha")
    f.remove(0)
    assert f.label == "MY DISC"


def test_keeps_control_characters_out_of_the_label():
    f = Form()
    f.set_label("ALAN\r\nWAKE")
    assert f.label == "ALANWAKE"


# ---- nothing that cannot be done right now stays usable -------------------------------

def test_says_what_stops_a_build_in_step_order():
    f = Form(background=None)
    assert f.missing() == [
        "Add a game first (step 1).",
        "Give the disc a label (step 2).",
        "Choose a disc icon (step 3).",
        "The menu needs a background image (step 4), or switch the menu off.",
        "Choose an output folder (step 6).",
    ]
    c = f.controls()["build"]
    assert not c.enabled and c.why == "Add a game first (step 1)."


def test_needs_no_background_with_the_menu_off(ready):
    ready.background = None
    assert not ready.controls()["build"].enabled
    ready.menu = False
    assert ready.controls()["build"].enabled


def test_refuses_an_icon_that_is_not_a_picture(ready, tmp_path):
    bad = tmp_path / "icon.png"
    bad.write_text("not a picture")
    ready.icon = bad
    assert "icon cannot be used" in ready.missing()[0]


def test_refuses_a_menu_with_no_buttons(ready):
    ready.buttons = []
    assert "no buttons" in ready.missing()[0]


def test_refuses_music_switched_on_with_none_chosen(ready):
    ready.music_on = True
    assert "music" in ready.missing()[0]


def test_refuses_a_label_that_cannot_name_an_iso(ready):
    ready.label = "***"
    assert ready.missing() == []            # "___.iso", as Windows names it
    ready.label = "   "
    assert "label" in ready.missing()[0]


def test_an_add_on_needs_a_game_to_belong_to(src):
    f = Form()
    assert not f.controls()["add_on"].enabled
    assert "Add the game first" in f.controls()["add_on"].why
    f.add_game(src / "Alpha")
    assert f.controls()["add_on"].enabled


def test_refuses_an_add_on_filed_under_another_add_on(src):
    f = Form()
    f.add_game(src / "Alpha")
    patch = sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB)
    assert f.add_on(patch, 0) is None
    assert f.add_on(patch, 1) is not None       # entry 1 is the add-on itself
    assert f.parents() == [0]


def test_remove_needs_something_picked(ready):
    assert not ready.controls()["remove"].enabled
    assert ready.controls(selected=0)["remove"].enabled


def test_switching_the_menu_off_greys_every_menu_option(ready):
    ready.menu = False
    c = ready.controls()
    for name in ("background", "bg_as_is", "buttons", "music_on", "window_border", "button_style",
                 "panel_side", "divider", "show_title", "title_text", "music", "manual", "extras"):
        assert not c[name].enabled, name
        assert "menu is switched off" in c[name].why, name


def test_using_the_picture_as_it_is_greys_what_would_be_painted_into_it(ready):
    ready.bg_as_is = True
    c = ready.controls()
    for name in ("panel_side", "divider", "show_title", "title_text"):
        assert not c[name].enabled and "Painted into the picture" in c[name].why, name
    # Still a menu, so everything else stays.
    assert c["buttons"].enabled and c["background"].enabled


def test_the_title_text_needs_the_title_ticked(ready):
    assert not ready.controls()["title_text"].enabled
    ready.show_title = True
    assert ready.controls()["title_text"].enabled


def test_manual_and_extras_follow_their_buttons(ready):
    assert ready.controls()["manual"].enabled
    ready.buttons = ["Play", "Install", "Exit"]
    c = ready.controls()
    assert not c["manual"].enabled and "Manual button" in c["manual"].why
    assert not c["extras"].enabled


def test_locks_everything_while_a_build_runs(ready):
    ready.building = True
    c = ready.controls(selected=0)
    assert set(c) == set(CONTROL_NAMES)
    assert not any(x.enabled for x in c.values())


def test_every_control_is_answered_for(ready):
    assert set(ready.controls()) == set(CONTROL_NAMES)


def test_new_disc_needs_something_to_clear(src, tmp_path):
    f = Form(out_dir=tmp_path)
    assert not f.controls()["new_disc"].enabled
    f.add_game(src / "Alpha")
    assert f.controls()["new_disc"].enabled


# ---- what only needs a yes -------------------------------------------------------------

def test_warns_about_a_label_windows_cannot_show(ready):
    ready.label = "Żółw"          # a Polish z-dot has no Windows-1252 form
    warnings = ready.warnings()
    assert len(warnings) == 1 and "cannot show this disc label" in warnings[0]


def test_does_not_warn_about_a_label_windows_can_show(ready):
    ready.label = "Café Über Alles"
    assert ready.warnings() == []


def test_warns_about_a_download_with_parts_missing(ready):
    ready.games[0].missing_parts = [2]
    assert "-2.bin" in ready.warnings()[0]


# ---- to the build, and back from a project --------------------------------------------

def test_hands_the_build_what_it_shows(ready):
    ready.panel_side, ready.divider, ready.buttons = "Left", True, ["Exit", "Play"]
    s = ready.to_settings()
    assert s.label == "alpha" and s.icon_is_ico and s.panel_side == "Left" and s.divider
    assert s.buttons == ["Play", "Exit"]               # the menu's order, not the ticking order


def test_leaves_out_what_the_menu_would_not_use(ready, src):
    ready.music = src / "media" / "Alpha Manual.pdf"
    ready.manual = src / "media" / "Alpha Manual.pdf"
    ready.buttons = ["Play", "Exit"]
    s = ready.to_settings()
    assert s.music_file is None and s.manual_path is None
    ready.menu = False
    assert ready.to_settings().bg_path is None


def test_opens_a_project_and_builds_it_the_same(ready, tmp_path):
    ready.divider, ready.show_title, ready.title_text = True, True, "Alpha!"
    ready.out_dir.mkdir(parents=True)
    path = save_project(ready.to_settings(), ready.out_dir)
    back, problems = Form.from_project(path)
    assert problems == []
    assert (back.label, back.divider, back.show_title, back.title_text) == ("alpha", True, True, "Alpha!")
    assert [g.game_name for g in back.games] == ["alpha"]
    assert back.missing() == []


def test_says_why_a_project_will_not_open(tmp_path):
    bad = tmp_path / "x.json"
    bad.write_text("nope")
    f, problems = Form.from_project(bad)
    assert f is None and "not a disc project" in problems[0]


def test_summarises_the_disc(src):
    f = Form()
    assert f.summary() == "No games yet."
    f.add_game(src / "Alpha")
    f.add_on(sparse(src / "Alpha" / "patch_alpha_1.0_to_1.1.exe", MB), 0)
    assert f.summary().startswith("1 game, 1 add-on, ")


def test_offers_the_same_buttons_as_windows():
    assert BUTTONS == ["Play", "Install", "Manual", "Extras", "Exit"]


# ---- which game an add-on goes under, and renaming --------------------------------------

def two_games(src):
    sparse(src / "Beta" / "setup_beta_2.0.exe", MB)
    f = Form()
    f.add_game(src / "Alpha")
    f.add_game(src / "Beta")
    return f


def test_files_an_add_on_under_the_only_game_without_asking(src):
    f = Form()
    f.add_game(src / "Alpha")
    assert f.parent_for(None) == 0
    assert f.controls()["add_on"].enabled


def test_asks_which_game_when_there_are_several(src):
    f = two_games(src)
    assert f.parent_for(None) is None
    c = f.controls()["add_on"]
    assert not c.enabled and "Pick the game" in c.why
    assert f.parent_for(1) == 1 and f.controls(selected=1)["add_on"].enabled


def test_files_an_add_on_beside_a_picked_add_on(src):
    f = two_games(src)
    f.add_on(sparse(src / "Beta" / "patch_beta_2.0_to_2.1.exe", MB), 1)
    assert f.parent_for(2) == 1


def test_renames_the_menu_name_and_keeps_the_registry_one(src):
    f = Form()
    f.add_game(src / "Alpha")
    matched = f.games[0].match_name
    assert f.rename(0, "  Alpha: The Return\n") is None
    assert f.games[0].game_name == "Alpha: The Return"
    assert f.games[0].match_name == matched


def test_refuses_an_empty_name(src):
    f = Form()
    f.add_game(src / "Alpha")
    assert f.rename(0, "   ") is not None
    assert f.games[0].game_name == "alpha"


def test_rename_needs_something_picked(ready):
    assert not ready.controls()["rename"].enabled
    assert ready.controls(selected=0)["rename"].enabled
