import pytest

from discwright import __version__
from discwright.cli import main


def test_reports_its_version(capsys):
    with pytest.raises(SystemExit) as done:
        main(["--version"])
    assert done.value.code == 0
    assert __version__ in capsys.readouterr().out
