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


def judge(text, category):
    """Returns (passed, summary) for the result XML in text."""
    try:
        root = ET.fromstring(text)
    except ET.ParseError:
        return False, "unreadable result: XML syntax error"
    counts = {"Passed": 0, "Failed": 0, "Skipped": 0, "other": 0}
    outside = 0
    failed_suites = []

    def walk(element, inherited):
        nonlocal outside
        mine = inherited | _categories(element)
        if element.tag == "test-case":
            if category in mine:
                result = element.get("result", "")
                counts[result if result in counts else "other"] += 1
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
    return counts["Passed"] == ran, summary


def main(argv):
    if len(argv) != 3:
        print("usage: ios_player_results.py <result.xml> <category>", file=sys.stderr)
        return 2
    with open(argv[1], encoding="utf-8") as f:
        passed, summary = judge(f.read(), argv[2])
    print(summary)
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
