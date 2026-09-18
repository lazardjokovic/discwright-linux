from discwright.xdg import xdg_volume_info


def test_matches_the_file_windows_wrote_byte_for_byte(windows_072_alanwake):
    expected = (windows_072_alanwake / ".xdg-volume-info").read_bytes()
    assert xdg_volume_info("ALAN WAKE", "ALANWAKE.png") == expected


def test_is_utf8_with_no_bom_and_lf_only():
    data = xdg_volume_info("Wiedźmin", "W.png")
    assert not data.startswith(b"\xef\xbb\xbf")
    assert b"\r" not in data
    assert "Wiedźmin".encode("utf-8") in data


def test_doubles_a_backslash_so_it_does_not_eat_the_next_character():
    # Two backslashes in the file, not four. The Windows version was written the
    # four-backslash way once, by a regex replacement that treated the backslash
    # as literal.
    assert b"Name=A\\\\B\n" in xdg_volume_info("A\\B", "x.png")


def test_a_label_cannot_add_keys_of_its_own():
    data = xdg_volume_info("Game\nIcon=evil", "x.png").decode("utf-8")
    assert "\nIcon=" not in data
