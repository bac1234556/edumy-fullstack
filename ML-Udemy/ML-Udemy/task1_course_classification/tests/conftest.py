"""pytest conftest.py - setup sys.path for tests."""
import sys
from pathlib import Path

# Ensure src is on path for all tests
_ROOT = Path(__file__).parent.parent
sys.path.insert(0, str(_ROOT / "src"))
