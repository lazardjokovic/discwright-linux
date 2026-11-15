#!/usr/bin/env python3
"""Run the shared suite on Linux, and hold the waiting list to shrinking.

Two runs. Everything not on waiting-on-linux.txt has to pass. Everything on it
has to still fail, because a test that passes while the list says it does not
is a fix nobody wrote down, and the next person reads the list and believes it.

A run that never happened looks exactly like a green one, so both runs have to
leave a results file with tests in it, and the first has to hold more than a
thousand: the day the glob quietly compiles nothing, this says so.
"""
import pathlib
import re
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
PROJECT = HERE / "DiscWright.LinuxTests.csproj"
RESULTS = HERE.parent.parent / "artifacts" / "linux-test-results"
FEWEST = 1000


def waiting():
    lines = (HERE / "waiting-on-linux.txt").read_text(encoding="utf-8").splitlines()
    return [line.strip() for line in lines if line.strip() and not line.startswith("#")]


def run(name, clause):
    trx = RESULTS / (name + ".trx")
    if trx.exists():
        trx.unlink()
    command = [
        "dotnet", "test", str(PROJECT), "-c", "Release", "--no-build", "--nologo",
        "--filter", clause,
        "--logger", "trx;LogFileName=" + name + ".trx",
        "--results-directory", str(RESULTS),
    ]
    finished = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if not trx.exists():
        print(finished.stdout)
        sys.exit("The " + name + " run left no results, so it did not happen.")
    found = re.findall(
        r'<UnitTestResult [^>]*testName="([^"]+)"[^>]*outcome="([^"]+)"',
        trx.read_text(encoding="utf-8-sig"))
    return [(test.split("(")[0], outcome) for test, outcome in found], finished.stdout


def main():
    names = waiting()
    doubled = sorted({name for name in names if names.count(name) > 1})
    if doubled:
        sys.exit("On the waiting list twice: " + ", ".join(doubled))

    everything_else = "&".join("FullyQualifiedName!=" + name for name in names)
    cases, said = run("the-suite", everything_else)
    failed = sorted({test for test, outcome in cases if outcome != "Passed"})
    print("%d cases off the waiting list, %d of them failing" % (len(cases), len(failed)))
    if failed:
        print(said)
        sys.exit("These fail on Linux and are not on the waiting list:\n  " + "\n  ".join(failed))
    if len(cases) < FEWEST:
        sys.exit("Only %d cases ran, and there should be over %d." % (len(cases), FEWEST))

    only_those = "|".join("FullyQualifiedName=" + name for name in names)
    cases, said = run("the-waiting-list", only_those)
    ran = {test for test, _ in cases}
    still_failing = {test for test, outcome in cases if outcome != "Passed"}
    print("%d tests on the waiting list, %d still failing" % (len(names), len(still_failing)))

    gone = sorted(set(names) - ran)
    if gone:
        sys.exit("On the waiting list and not in the suite any more:\n  " + "\n  ".join(gone))
    mended = sorted(ran - still_failing)
    if mended:
        sys.exit("These pass on Linux now. Take them off waiting-on-linux.txt:\n  " + "\n  ".join(mended))


if __name__ == "__main__":
    main()
