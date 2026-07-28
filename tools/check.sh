#!/usr/bin/env bash
# Sunny Stop - the full gate. Runs everything CI runs, locally, with no Unity.
#
#   tools/check.sh
#
# Needs: python3 (3.11+), and mono-mcs for the C# half. Without mono the C#
# steps are skipped with a warning rather than silently passing.

set -uo pipefail
cd "$(dirname "$0")/.."

BUILD="${TMPDIR:-/tmp}/sunnystop-build"
mkdir -p "$BUILD"
FAILED=0

step() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }
fail() { printf '\033[31mFAIL\033[0m %s\n' "$1"; FAILED=1; }
ok()   { printf '\033[32mok\033[0m   %s\n' "$1"; }

step "Rules, level and postcard tests (Python)"
if (cd tools/leveltool && python3 -m unittest discover -p 'test_*.py' 2>&1 | tail -4); then
  ok "python tests"
else
  fail "python tests"
fi

step "Level content gate - re-solve every shipped level"
if (cd tools/leveltool && python3 main.py verify 2>&1 | tail -3); then
  ok "all levels solvable and on curve"
else
  fail "level verification"
fi

if ! command -v mcs >/dev/null 2>&1; then
  printf '\n\033[33mmono (mcs) not installed - skipping the C# half.\033[0m\n'
  printf 'Install with: apt-get install -y mono-mcs\n'
  exit $FAILED
fi

step "Compile Core (engine-free, must build with no Unity at all)"
if mcs -langversion:latest -target:library \
       -out:"$BUILD/SunnyStop.Core.dll" Assets/Scripts/Core/*.cs; then
  ok "Core compiles"
else
  fail "Core does not compile"
fi

step "Compile the Unity layer against the API stub"
# Catches typos, wrong member names and bad signatures without the editor.
if mcs -langversion:latest -target:library -r:"$BUILD/SunnyStop.Core.dll" \
       -out:"$BUILD/SunnyStop.Game.dll" \
       tools/unitystub/UnityStub.cs Assets/Scripts/Game/*.cs; then
  ok "Unity layer compiles"
else
  fail "Unity layer does not compile"
fi

step "Compile the Unity test suite against the stubs"
# Nothing in the repo should be uncompiled, including the editor tests.
if mcs -langversion:latest -target:library \
       -r:"$BUILD/SunnyStop.Core.dll" -r:"$BUILD/SunnyStop.Game.dll" \
       -out:"$BUILD/SunnyStop.Tests.dll" \
       tools/unitystub/NUnitStub.cs Assets/Tests/*.cs; then
  ok "test suite compiles"
else
  fail "test suite does not compile"
fi

step "Cross-language check - C# engine replays every Python-verified level"
if mcs -langversion:latest -r:"$BUILD/SunnyStop.Core.dll" \
       -out:"$BUILD/crosscheck.exe" tools/crosscheck/CrossCheck.cs; then
  if mono "$BUILD/crosscheck.exe" Assets/Resources/Levels Assets/Resources/Messages; then
    ok "C# and Python agree"
  else
    fail "cross-language check"
  fi
else
  fail "cross-check harness does not compile"
fi

printf '\n'
if [ "$FAILED" -eq 0 ]; then
  printf '\033[32mAll checks passed.\033[0m\n'
else
  printf '\033[31mSomething failed - see above.\033[0m\n'
fi
exit $FAILED
