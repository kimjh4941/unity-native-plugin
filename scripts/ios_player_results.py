#!/usr/bin/env python3
r"""Judges the NUnit result an iOS player test run wrote into its app container.

Used by scripts/verify_unity_ios.sh (artifact/topics/ios-c-abi-2, UI automation design v4, 3.1).
The player writes the test-suite NUnit gives for the whole run (IosPlayerResultWriter), not the
test-run element the Editor writes, so the counts are taken from the test cases themselves.

A run passes only when:
- at least one test case of the category ran, all of them passed (a skipped player test means the
  check it stands for did not happen),
- every test case of the run belongs to the category (the run filters by it, so a case outside it, or
  a count that differs from the root's testcasecount, means the filter or the build went wrong), and
- no test-suite failed (NUnit records a OneTimeSetUp/OneTimeTearDown failure on the suite, while its
  cases can still read Passed).

    >>> sample = '''<test-suite type="TestSuite" name="Unity NativeToolkit" testcasecount="1" result="Passed">
    ...   <test-suite type="TestFixture" name="IosClipboardPlayerTests">
    ...     <properties><property name="Category" value="IosPlayer" /></properties>
    ...     <test-case name="NamedPasteboardRoundTripsPlainText" result="Passed" />
    ...   </test-suite>
    ... </test-suite>'''
    >>> judge(sample, "IosPlayer")
    (True, 'IosPlayer: 1 ran, 1 passed, 0 failed, 0 skipped')

    >>> judge(sample.replace('result="Passed" />', 'result="Failed" />'), "IosPlayer")
    (False, 'IosPlayer: 1 ran, 0 passed, 1 failed, 0 skipped')

    The failed cases are named with the first line of their message (the test's own text, cut at 200
    characters), so a device run, whose result file is deleted, still tells which case failed:

    >>> failed = sample.replace('result="Passed" />', 'result="Failed"><failure><message>  Expected: 1\n  But was: 2</message></failure></test-case>')
    >>> failures(failed)
    ['NamedPasteboardRoundTripsPlainText: Expected: 1']

    >>> judge(sample.replace('result="Passed" />', 'result="Skipped" />'), "IosPlayer")
    (False, 'IosPlayer: 1 ran, 0 passed, 0 failed, 1 skipped')

    >>> judge(sample, "Missing")
    (False, 'Missing: no test case of the category ran')

    A failed suite fails the run even when its cases passed:

    >>> judge(sample.replace('name="IosClipboardPlayerTests"', 'name="IosClipboardPlayerTests" result="Failed"'), "IosPlayer")
    (False, 'IosPlayer: 1 ran, 1 passed, 0 failed, 0 skipped; failed suite(s): IosClipboardPlayerTests')

    A case outside the category, or a count that differs from the root's, fails the run:

    >>> judge(sample.replace('</test-suite>\n</test-suite>', '</test-suite><test-case name="Stray" result="Passed" /></test-suite>'), "IosPlayer")
    (False, 'IosPlayer: 1 ran, 1 passed, 0 failed, 0 skipped; 1 case(s) outside the category; the root counts 1 case(s)')

    A category on a test case (not only on its fixture) counts too:

    >>> judge('''<test-suite testcasecount="1"><test-case name="a" result="Passed">
    ...   <properties><property name="Category" value="IosPlayer" /></properties>
    ... </test-case></test-suite>''', "IosPlayer")
    (True, 'IosPlayer: 1 ran, 1 passed, 0 failed, 0 skipped')

    >>> judge("not xml", "IosPlayer")
    (False, 'unreadable result: XML syntax error')
"""

import sys
import xml.etree.ElementTree as ET


def _categories(element):
    return {p.get("value") for p in element.findall("./properties/property[@name='Category']")}


def judge(text, category, allow_skip=frozenset()):
    """Returns (passed, summary) for the result XML in text.

    allow_skip names categories whose skipped cases do not fail the run: the sample-screen tests on the
    Test iOS Profile, whose player has no sample scene (UI driver design v3, chapter 6). They are counted
    apart and still need a category of the run.

    >>> two = '''<test-suite type="TestSuite" name="r" testcasecount="2" result="Passed">
    ...   <test-suite type="TestFixture" name="F"><properties><property name="Category" value="IosPlayer" /></properties>
    ...     <test-case name="a" result="Passed" />
    ...     <test-case name="b" result="Skipped"><properties><property name="Category" value="IosSampleScreen" /></properties></test-case>
    ...   </test-suite>
    ... </test-suite>'''
    >>> judge(two, "IosPlayer")
    (False, 'IosPlayer: 2 ran, 1 passed, 0 failed, 1 skipped')
    >>> judge(two, "IosPlayer", {"IosSampleScreen"})
    (True, 'IosPlayer: 2 ran, 1 passed, 0 failed, 1 skipped (1 allowed: IosSampleScreen)')
    >>> judge(two.replace('result="Passed" />', 'result="Skipped" />'), "IosPlayer", {"IosSampleScreen"})
    (False, 'IosPlayer: 2 ran, 0 passed, 0 failed, 2 skipped (1 allowed: IosSampleScreen)')
    """
    try:
        root = ET.fromstring(text)
    except ET.ParseError:
        return False, "unreadable result: XML syntax error"
    counts = {"Passed": 0, "Failed": 0, "Skipped": 0, "other": 0}
    allowed = 0
    outside = 0
    failed_suites = []

    def walk(element, inherited):
        nonlocal outside, allowed
        mine = inherited | _categories(element)
        if element.tag == "test-case":
            if category in mine:
                result = element.get("result", "")
                counts[result if result in counts else "other"] += 1
                if result == "Skipped" and mine & set(allow_skip):
                    allowed += 1
            else:
                outside += 1
        elif element.get("result") == "Failed":
            failed_suites.append(element.get("name", "?"))
        for child in element:
            if child.tag in ("test-suite", "test-case"):
                walk(child, mine)

    walk(root, set())
    ran = sum(counts.values())
    if ran == 0:
        return False, f"{category}: no test case of the category ran"
    summary = (f"{category}: {ran} ran, {counts['Passed']} passed, {counts['Failed'] + counts['other']} failed, "
               f"{counts['Skipped']} skipped")
    if allow_skip:
        summary += f" ({allowed} allowed: {', '.join(sorted(allow_skip))})"
    problems = []
    if failed_suites:
        problems.append("failed suite(s): " + ", ".join(failed_suites))
    if outside:
        problems.append(f"{outside} case(s) outside the category")
    declared = root.get("testcasecount")
    if declared != str(ran + outside):
        problems.append(f"the root counts {declared or 'no'} case(s)")
    if problems:
        return False, summary + "; " + "; ".join(problems)
    return counts["Passed"] + allowed == ran, summary


def judge_required(text, required, allow_skip=frozenset()):
    """Returns (passed, summary): at least one case of the required category ran outside allow_skip, and
    none of its cases was skipped outside allow_skip (UI driver design v3, chapter 6: a UI test that did
    not reach the driver must not pass as skipped).

    >>> run = '''<test-suite type="TestSuite" name="r" testcasecount="2" result="Passed">
    ...   <test-suite type="TestFixture" name="F"><properties><property name="Category" value="IosPlayer" /><property name="Category" value="IosUiDriver" /></properties>
    ...     <test-case name="connect" result="Passed" />
    ...     <test-case name="sample" result="Skipped"><properties><property name="Category" value="IosSampleScreen" /></properties></test-case>
    ...   </test-suite>
    ... </test-suite>'''
    >>> judge_required(run, "IosUiDriver")
    (False, 'IosUiDriver: 2 case(s), 2 counted, 1 skipped')
    >>> judge_required(run, "IosUiDriver", {"IosSampleScreen"})
    (True, 'IosUiDriver: 2 case(s), 1 counted, 0 skipped')
    >>> judge_required(run.replace('name="connect" result="Passed" /', 'name="connect" result="Skipped" /'), "IosUiDriver", {"IosSampleScreen"})
    (False, 'IosUiDriver: 2 case(s), 1 counted, 1 skipped')
    >>> judge_required(run.replace('value="IosUiDriver"', 'value="Other"'), "IosUiDriver")
    (False, 'IosUiDriver: no test case of the category')
    """
    try:
        root = ET.fromstring(text)
    except ET.ParseError:
        return False, "unreadable result: XML syntax error"
    total = counted = skipped = 0

    def walk(element, inherited):
        nonlocal total, counted, skipped
        mine = inherited | _categories(element)
        if element.tag == "test-case" and required in mine:
            total += 1
            if not mine & set(allow_skip):
                counted += 1
                if element.get("result") == "Skipped":
                    skipped += 1
        for child in element:
            if child.tag in ("test-suite", "test-case"):
                walk(child, mine)

    walk(root, set())
    if total == 0:
        return False, f"{required}: no test case of the category"
    summary = f"{required}: {total} case(s), {counted} counted, {skipped} skipped"
    return counted > 0 and skipped == 0, summary


def failures(text):
    """The failed test cases, each with the first line of its failure message."""
    try:
        root = ET.fromstring(text)
    except ET.ParseError:
        return []
    lines = []
    for case in root.iter("test-case"):
        if case.get("result") in ("Passed", "Skipped"):
            continue
        message = (case.findtext("./failure/message") or "").strip().splitlines()
        first = message[0].strip()[:200] if message else "(no message)"
        lines.append(f"{case.get('name', '?')}: {first}")
    return lines


def main(argv):
    args = argv[1:]
    allow_skip, required = set(), []
    while len(args) > 2 and args[-2] in ("--allow-skip", "--require"):
        (allow_skip.add if args[-2] == "--allow-skip" else required.append)(args[-1])
        args = args[:-2]
    if len(args) != 2:
        print("usage: ios_player_results.py <result.xml> <category> [--require <category>]... [--allow-skip <category>]...",
              file=sys.stderr)
        return 2
    with open(args[0], encoding="utf-8") as f:
        text = f.read()
    passed, summary = judge(text, args[1], allow_skip)
    print(summary)
    for category in required:
        ok, line = judge_required(text, category, allow_skip)
        print("    " + line)
        passed = passed and ok
    for line in failures(text):
        print("    failed: " + line)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
