from discwright.autorun import autorun_inf


def test_matches_the_file_windows_wrote_byte_for_byte(windows_072_alanwake):
    expected = (windows_072_alanwake / "autorun.inf").read_bytes()
    assert autorun_inf("ALAN WAKE", "ALANWAKE.ico", menu=True) == expected


def test_is_crlf_with_a_trailing_line_break_and_no_bom():
    data = autorun_inf("X", "X.ico", menu=True)
    assert data.endswith(b"\r\n")
    assert not data.startswith(b"\xef\xbb\xbf")
    assert b"\n" not in data.replace(b"\r\n", b"")


def test_leaves_the_menu_lines_out_when_there_is_no_menu():
    text = autorun_inf("X", "X.ico", menu=False).decode("cp1252")
    assert "shellexecute=" not in text
    assert "action=" not in text
    assert "icon=X.ico" in text and "label=X" in text


def test_a_label_cannot_write_a_directive_of_its_own():
    text = autorun_inf("My Game\r\nopen=payload.exe", "X.ico", menu=True).decode("cp1252")
    assert "\r\nopen=" not in text
