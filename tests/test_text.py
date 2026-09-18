from discwright.text import encode_ansi, remove_control_chars, volume_label


# Ported one for one from the Get-VolumeLabel block of the Windows suite
# (tests/DiscWright.Tests.ps1), so both tools agree on the same inputs.

def test_keeps_a_label_that_already_fits():
    assert volume_label("Alan Wake") == "Alan_Wake"


def test_folds_anything_not_alphanumeric_to_an_underscore():
    assert volume_label("Broken Sword: Shadow") == "Broken_Sword__Sh"


def test_caps_the_volume_id_at_16_characters():
    assert len(volume_label("THE WITCHER ENHANCED EDITION")) == 16


def test_no_trailing_underscore_when_the_cut_lands_on_a_word_boundary():
    assert volume_label("THE WITCHER ENH EDITION") == "THE_WITCHER_ENH"
    assert not volume_label("A B C D E F G H I").endswith("_")


def test_never_reserves_room_for_a_disc_number():
    assert not volume_label("THE WITCHER ENHANCED EDITION")[-2:].startswith("_D")


def test_falls_back_to_disc_when_nothing_survives():
    assert volume_label("!!!") == "DISC"
    assert volume_label("") == "DISC"
    assert volume_label(None) == "DISC"


def test_folds_accented_letters_rather_than_keeping_them():
    # The volume id is plain ASCII. The accented name survives where it is
    # actually shown, in autorun.inf's label line.
    assert volume_label("Über Alles") == "ber_Alles"


def test_strips_every_control_character():
    assert remove_control_chars("My Game\r\nopen=payload.exe") == "My Gameopen=payload.exe"
    assert remove_control_chars("tab\there\x7f") == "tabhere"
    assert remove_control_chars("") == ""
    assert remove_control_chars(None) is None


def test_ansi_keeps_what_windows_1252_can_hold():
    assert encode_ansi("Über Alles") == b"\xdcber Alles"
    assert encode_ansi("Star Wars™") == b"Star Wars\x99"


def test_ansi_drops_an_accent_1252_has_no_letter_for():
    # z-acute is not in Windows-1252; Windows' best fit writes a plain z.
    assert encode_ansi("ź") == b"z"


def test_ansi_writes_a_question_mark_only_as_a_last_resort():
    assert encode_ansi("中") == b"?"
