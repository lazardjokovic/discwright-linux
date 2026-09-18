from pathlib import Path

import pytest

# Files Windows DiscWright wrote, copied off a real disc byte for byte. They are
# the reference: when a test here disagrees with one of them, the Python is wrong.
FIXTURES = Path(__file__).parent / "fixtures"


@pytest.fixture
def windows_072_alanwake() -> Path:
    return FIXTURES / "windows-0.7.2" / "alanwake"
