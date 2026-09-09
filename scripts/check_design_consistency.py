#!/usr/bin/env python3
"""Check internal consistency of a unity-native-plugin design document.

Design documents restate the same facts across a dozen sections: file counts,
result-type counts, seam tables, per-call slots, verification IDs, section
cross-references, pseudo-code identifiers and citations into the C# sources.
Six review rounds on the macOS clipboard design were dominated by drift
between those copies rather than by design decisions, so the invariants are
checked here instead of by hand.

Every check reports one of three states. A check that cannot find its subject
reports SKIP and never OK: a silent vacuous pass hands out false confidence,
which is worse than having no check at all.

Usage:
    python3 scripts/check_design_consistency.py <design.md> [...]

On Windows `python3` may resolve to a Microsoft Store app execution alias, which
runs nothing and exits 49 - the checks then look like they passed when none of
them ran. Where `python3 -c "print(1)"` prints nothing, use `python` instead.

Exit status is 1 when any check fails.
"""

import re
import sys
from pathlib import Path

# The findings quote the design documents back, and those are Japanese. A Japanese
# Windows defaults stdout to cp932, which mangles them, so a finding would arrive
# unreadable - and any character outside cp932 would end the run in a traceback.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

REPO = Path(__file__).resolve().parent.parent

# The change log records what earlier revisions did, and the out-of-scope /
# rejected-alternatives sections name things this design deliberately does not
# build. Both legitimately mention identifiers and counts that no longer hold.
# Two forms are in use - "## vN からの主な変更" and "### vN からの変更点" - and
# matching only the first left a whole change log inside the checked text, where a
# sentence saying an ID was deleted reads exactly like a citation of it.
HISTORY_HEADING = re.compile(r"^#{2,3} v\d+ からの(?:主な)?変更")
EXEMPT_HEADINGS = re.compile(r"^## \d+\. (採用しなかった案|出力範囲の明記)")
SECTION_HEADING = re.compile(r"^#{2,4} (?:\d+\.|v\d+ )")

# Lines that state something is absent describe the absence rather than rely on it.
ABSENCE_MARKERS = ("対象外", "定義しない", "持たない", "使わない", "採用しない", "到達しない",
                   "存在しない", "削除", "旧", "実装しない", "踏襲しない", "死にコード",
                   "前例が無い", "書いてはならない", "してはならない", "含まない")

# Platform and BCL vocabulary. A design names the framework types it uses; only
# project symbols say anything about drift.
FOREIGN = re.compile(
    r"^(NS|UI|UT|CF|CG|CA|IL)[A-Z]"
    r"|^(Action|Func|Task|List|Dictionary|HashSet|IReadOnly\w*|Exception|Convert|Enumerable"
    r"|String|Debug|Application|GameObject|MonoBehaviour|Thread|Marshal|DllImport|UnmanagedType"
    r"|CallingConvention|UnmanagedFunctionPointer|MonoPInvokeCallback|RuntimePlatform"
    r"|DateTimeOffset|CultureInfo|ArgumentException|ArgumentNullException|OutOfMemoryException"
    r"|NullReferenceException|OverflowException|InvalidOperationException|TimeSpan|Awaitable"
    r"|AwaitableCompletionSource|CancellationToken|RuntimeInitializeOnLoadMethod"
    r"|RuntimeInitializeLoadType|SubsystemRegistration|TearDown|SetUp|Sum|Length|Count|Value"
    r"|Items|Keys|Values|Instance|Invoke|Add|Remove|Clear|Contains|ToString"
    r"|Log|LogError|LogWarning|Enqueue|Update|Awake|OnDestroy|TryParse|nameof"
    r"|Array|Path|File|Directory|FileInfo|EditorUtility|StringComparison|Encoding"
    r"|TryGetValue|WriteAllText|ReadAllText|Combine|GetStatic|CallStatic|IntPtr"
    r"|Volatile|Interlocked|StringBuilder|IDisposable|Dispose|Equals|GetHashCode)$"
)

ID_PREFIXES = ("V", "D", "OP")


class Report:
    def __init__(self, path):
        self.path = path
        self.notes = []
        self.failures = []

    def ok(self, name):
        self.notes.append(f"  OK   {name}")

    def skip(self, name, why):
        self.notes.append(f"  SKIP {name}: {why}")

    def check(self, condition, name, detail=""):
        if condition:
            self.ok(name)
        else:
            self.failures.append(f"  FAIL {name}: {detail}")

    def dump(self):
        print(f"== {self.path}")
        for line in self.notes + self.failures:
            print(line)
        return not self.failures


def section(text, start_pattern, stop_pattern=r"^#{2,4} "):
    """Body after a heading, up to the next heading. None when absent."""
    m = re.search(start_pattern, text, re.M)
    if not m:
        return None
    rest = text[m.end():]
    stop = re.search(stop_pattern, rest, re.M)
    return rest[:stop.start()] if stop else rest


def table_rows(body):
    """Data rows of the first markdown table in `body` (header and rule dropped)."""
    rows = [ln for ln in body.splitlines() if ln.startswith("|")]
    return [r for r in rows if not re.match(r"^\|[\s:|-]+\|$", r)][1:]


def live_lines(text):
    """(lineno, line) for the sections that state the current contract."""
    in_history = in_exempt = False
    for lineno, line in enumerate(text.splitlines(), 1):
        if HISTORY_HEADING.match(line):
            in_history, in_exempt = True, False
        elif EXEMPT_HEADINGS.match(line):
            in_history, in_exempt = False, True
        elif SECTION_HEADING.match(line):
            in_history = in_exempt = False
        if in_history or in_exempt:
            continue
        yield lineno, line


def fenced(text):
    """(lineno, line, in_fence) over every line, tracking ``` fences."""
    in_fence = False
    for lineno, line in enumerate(text.splitlines(), 1):
        if line.lstrip().startswith("```"):
            in_fence = not in_fence
            yield lineno, line, True
            continue
        yield lineno, line, in_fence


# ── checks ──────────────────────────────────────────────────────────────────

def check_heading_order(text, rep):
    """Section numbers ascend and have no gaps or duplicates.

    A version cut by search-and-replace renumbers one table and forgets the
    heading, which every cross-reference then points past.
    """
    # The depth of a number is part of it: "### 5.2.1" is not another "### 5.2",
    # and reading it as one reported a duplicate rather than the wrong heading level
    # that actually caused it.
    depth = [(len(m.group(1)) - 1, m.group(2), lineno)
             for lineno, line in enumerate(text.splitlines(), 1)
             for m in [re.match(r"^(#{2,4}) (\d+(?:\.\d+)*)[ .]", line)] if m]
    mislevelled = [(number, lineno) for level, number, lineno in depth
                   if level != number.count(".") + 1]
    rep.check(not mislevelled, "heading level matches the depth of its number",
              f"wrong level={mislevelled[:5]}")

    for label, pattern in (("top-level", r"^## (\d+)\.(?![\d.])"),
                           ("sub", r"^### (\d+)\.(\d+)(?![\d.])"),
                           ("subsub", r"^#### (\d+)\.(\d+)\.(\d+)(?![\d.])")):
        found = re.findall(pattern, text, re.M)
        if len(found) < 2:
            rep.skip(f"{label} heading order", f"{len(found)} heading(s)")
            continue
        nums = [tuple(int(x) for x in (m if isinstance(m, tuple) else (m,))) for m in found]
        drops = [(a, b) for a, b in zip(nums, nums[1:]) if b < a]
        rep.check(not drops, f"{label} heading order ascending", f"drops={drops[:5]}")

        dupes = sorted({n for n in nums if nums.count(n) > 1})
        rep.check(not dupes, f"{label} headings unique", f"duplicates={dupes[:5]}")


def check_section_refs(text, rep):
    """Every section number the prose cites must exist as a heading."""
    defined = set()
    for m in re.finditer(r"^#{2,4} (\d+(?:\.\d+)*)[ .]", text, re.M):
        defined.add(m.group(1))
    if not defined:
        rep.skip("cited sections exist", "no numbered headings")
        return

    # Headings are not the only thing the prose cites. A chapter whose body is an
    # ordered list is cited by position: "要検証 9.2" is the second item under
    # "## 9. 要検証事項", which is a heading the document never writes. Counting the
    # items keeps that a real check - 9.2 resolves, 9.9 does not - instead of either
    # a false failure or a blanket exclusion.
    chapters = {}
    for m in re.finditer(r"^## (\d+)[ .]", text, re.M):
        rest = text[m.end():]
        stop = re.search(r"^#{2,4} ", rest, re.M)
        body = rest[:stop.start()] if stop else rest
        items = [int(x) for x in re.findall(r"^(\d+)\. ", body, re.M)]
        chapters[m.group(1)] = max(items) if items else 0

    # A bare "1.3.0" is a package version and "0.5" is a number of seconds, so only
    # the two forms that unambiguously cite a section count: inside a parenthetical,
    # or followed by 節 / 参照.
    cite = re.compile(r"（[^）]*?(?<![\d.\w])(\d+\.\d+(?:\.\d+)?)(?![\d.])[^）]*?）"
                      r"|(?<![\d.\w])(\d+\.\d+(?:\.\d+)?)(?![\d.])\s*(?:節|を参照|参照)")
    # An OS version reads exactly like a section number, and both appear inside
    # parentheticals: "（macOS 15.4 未満）".
    os_version = re.compile(r"(?:macOS|iOS|Android|Unity)\s*\d+\.\d+")
    # So does a package version, and the word it hangs off varies too much to match
    # on ("現行同梱 AAR（1.3.0）", "第 0 章の XCFramework 更新（1.2.0）", "（例: 1.11.0）").
    # What does not vary is that the line names the artifact being versioned, so the
    # line decides rather than the few characters in front of the number.
    ships_an_artifact = re.compile(r"AAR|XCFramework|\.aar|\.dll|\bdist\b|パッケージ")
    # Inline code spans hold literals, not references: `"3.0"` is a JSON number.
    code_span = re.compile(r"`[^`]*`")
    # A citation into another document reads exactly like a self-reference:
    # "（設計 2.9）" names section 2.9 of the feature design, not of this file, and
    # cannot be resolved from here. The naming word is the tell, and it is only ever
    # written about another document - no design document names itself this way.
    # Counted rather than dropped: an unresolvable citation is still a citation, and
    # a check that quietly ignores part of its subject is the vacuous pass this file
    # exists to avoid.
    other_document = re.compile(
        r"(?:設計|計画書|計画|実装結果|実機確認結果|実機確認|機能実装の?|マニュアル)"
        r"\s*(?:v\d+)?\s*(?:要検証(?:事項)?)?\s*$")
    fences = {ln for ln, _, inside in fenced(text) if inside}
    missing = {}
    elsewhere = 0
    for lineno, line in live_lines(text):
        if line.startswith("#") or lineno in fences:
            continue
        if ships_an_artifact.search(line):
            continue
        os_spans = [m.span() for m in os_version.finditer(line)]
        os_spans += [m.span() for m in code_span.finditer(line)]
        for m in cite.finditer(line):
            ref = m.group(1) or m.group(2)
            at = m.start(1) if m.group(1) else m.start(2)
            if any(a <= at < b for a, b in os_spans):
                continue
            if other_document.search(line[:at]):
                elsewhere += 1
                continue
            if ref in defined:
                continue
            head, _, tail = ref.partition(".")
            if head not in chapters:
                # Not this document's numbering at all: another document, or a value
                # that happens to read like one ("progress 0.5 固定").
                elsewhere += 1
                continue
            if tail.isdigit() and 1 <= int(tail) <= chapters[head]:
                continue
            missing.setdefault(ref, lineno)
    name = "cited sections exist"
    if elsewhere:
        name += f" ({elsewhere} unresolvable here: other documents or values)"
    rep.check(not missing, name, f"missing={sorted(missing.items())[:8]}")


def check_id_sequences(text, rep):
    """V- and D- IDs are contiguous, and every cited ID is defined in its table."""
    for prefix, label in (("V", "verification"), ("D", "decision")):
        defined = set()
        for row in re.findall(rf"^\|\s*\*{{0,2}}{prefix}-(\d+)\*{{0,2}}\s*\|", text, re.M):
            defined.add(int(row))
        if not defined:
            rep.skip(f"{label} IDs contiguous", f"no {prefix}- rows")
            continue
        gaps = sorted(set(range(1, max(defined) + 1)) - defined)
        rep.check(not gaps, f"{label} IDs contiguous", f"gaps={gaps}")

        cited = {}
        for lineno, line in live_lines(text):
            if re.match(rf"^\|\s*\*{{0,2}}{prefix}-\d+", line):
                continue
            # "v3 の V-7 は ... 削除した" names the ID to say it is gone. Reading that
            # as a citation makes the document fail for explaining itself.
            if any(marker in line for marker in ABSENCE_MARKERS):
                continue
            for a, b in re.findall(rf"{prefix}-(\d+)\s*〜\s*(?:{prefix}-)?(\d+)", line):
                for n in range(int(a), int(b) + 1):
                    cited.setdefault(n, lineno)
            for n in re.findall(rf"(?<![\w-]){prefix}-(\d+)(?![\d〜])", line):
                cited.setdefault(int(n), lineno)
        missing = sorted((f"{prefix}-{n}", ln) for n, ln in cited.items() if n not in defined)
        rep.check(not missing, f"cited {label} IDs are defined", f"missing={missing[:8]}")


def check_declared_counts(text, rep):
    """A count stated in prose must match the table that defines the thing.

    Every round of review found one of these stale after a table grew by a row.
    """
    checks = []

    body = section(text, r"^### 4\.1 .*?— (\d+) ファイル")
    m = re.search(r"^### 4\.1 .*?— (\d+) ファイル", text, re.M)
    if m and body is not None:
        rows = [r for r in table_rows(body) if re.match(r"^\|\s*\d+\s*\|", r)]
        checks.append(("4.1 runtime file count", int(m.group(1)), len(rows)))

    # OS-agnostic form: "### <n>.<m> 新規作成（Runtime）" plus a prose line that
    # states how many files that inventory holds. The macOS documents declare the
    # count in the heading (handled above); the Windows one states it in the
    # platform-independence self-check table, and splitting one bundle file into
    # eight left that number stale with nothing to catch it.
    for kind in ("Runtime", "テスト"):
        m = re.search(rf"^### \d+\.\d+ 新規作成（{kind}）", text, re.M)
        if not m:
            continue
        body = section(text, rf"^### \d+\.\d+ 新規作成（{kind}）")
        if body is None:
            continue
        names = set(re.findall(OS_PREFIXED_CS, body))
        if not names:
            continue
        declared = re.search(rf"{kind}\s*(\d+)\s*ファイル", text)
        if declared:
            checks.append((f"新規作成（{kind}）file count", int(declared.group(1)), len(names)))

    body = section(text, r"^#### 5\.6\.12 per-call スロット")
    m = re.search(r"計 (\d+) 本（操作 \d+", text)
    if body is not None and m:
        slots = set(re.findall(r"`(s_[A-Za-z]\w*)`", body))
        checks.append(("5.6.12 per-call slot count", int(m.group(1)), len(slots)))

    body = section(text, r"^### 7\.5 手動確認")
    m = re.search(r"手動確認 (\d+) 項目", text)
    if body is not None and m:
        rows = [r for r in table_rows(body) if re.match(r"^\|\s*\d+[a-z]?\s*\|", r)]
        checks.append(("7.5 manual check count", int(m.group(1)), len(rows)))

    if not checks:
        rep.skip("declared counts match their tables", "no count declarations found")
        return
    bad = [(name, d, a) for name, d, a in checks if d != a]
    rep.check(not bad, "declared counts match their tables",
              "; ".join(f"{n}: declared {d}, table has {a}" for n, d, a in bad))


def check_repeated_counts(text, rep):
    """The same quantity quoted in several sections must agree.

    Keyed on the phrase, so a figure restated in a test section or a stage
    table cannot drift away from the section that owns it.
    """
    patterns = {
        "result types": r"結果型 (\d+) 種",
        "parser methods": r"パーサ.{0,4}?(\d+) メソッド|(\d+) 種の出力（出力専用",
        "public methods": r"公開メソッド (\d+)",
        "public events": r"公開イベント (\d+)",
        "guard stages": r"ガードチェーン (\d+) 段",
    }
    hits = {}
    for lineno, line in live_lines(text):
        if any(marker in line for marker in ABSENCE_MARKERS):
            continue
        for label, pat in patterns.items():
            values = {int(next(g for g in m.groups() if g)) for m in re.finditer(pat, line)}
            # A line that states two figures for the same label is comparing a subset
            # with a total ("4 本 … 13 本のうち残り 8 本"), not contradicting itself.
            if len(values) != 1:
                continue
            hits.setdefault(label, {}).setdefault(values.pop(), lineno)
    conflicting = {label: sorted(v.items()) for label, v in hits.items() if len(v) > 1}
    if not hits:
        rep.skip("repeated counts agree", "no repeated counts found")
        return
    rep.check(not conflicting, "repeated counts agree",
              "; ".join(f"{k}: {v}" for k, v in conflicting.items()))


_SHIPPED = None


def shipped_names():
    """Every name the package's own sources already define.

    A design document has to introduce the helpers it invents, not the API it
    calls: "FinishDrain" is a method that exists, and asking prose to declare it
    again is noise. Reading the sources answers that without a hand-kept list,
    which is what the previous allow-list was and could never finish being.
    """
    global _SHIPPED
    if _SHIPPED is None:
        names = set()
        package = REPO / "Packages" / "com.jonghyunkim.nativetoolkit"
        # Named rather than walked from the package root: Plugins/ carries the macOS
        # xcframework, whose Versions/Current symlink is dead on a Windows checkout
        # and ends the walk with a FileNotFoundError.
        sources = [package / name for name in ("Runtime", "Editor", "Tests")]
        for path in [p for root in sources if root.is_dir() for p in root.rglob("*.cs")]:
            source = path.read_text(encoding="utf-8", errors="replace")
            for line in source.splitlines():
                # Comments and doc prose are read out first, or every English word
                # in the package would count as a name it defines.
                if line.lstrip().startswith("///"):
                    continue
                names.update(re.findall(r"\b[A-Za-z_]\w*\b", readable_code(line)))
        for path in [p for root in sources if root.is_dir() for p in root.rglob("*.uxml")]:
            source = path.read_text(encoding="utf-8", errors="replace")
            names.update(re.findall(r'name="([^"]+)"', source))
        _SHIPPED = names
    return _SHIPPED


def readable_code(line):
    """A code line with the parts that name nothing removed.

    Three of them, and each produced its own class of false report before being
    taken out: a string literal ("Hello from Unity Native Toolkit") holds prose, a
    using directive holds a namespace path rather than anything the document has to
    declare, and a comment holds whatever the author wanted to say.
    """
    if re.match(r"\s*(?:using|namespace)\s+[\w.]+\s*;?\s*$", line):
        return ""
    line = re.sub(r"//.*$", "", line)
    for quote in ('"', "'"):
        halves = line.split(quote)
        line = quote.join(h if i % 2 == 0 else '' for i, h in enumerate(halves))
    return line


def check_code_identifiers(text, rep):
    """Every project identifier used in pseudo-code must be defined in prose too.

    This is what catches a skeleton that calls a helper the document never
    declares. Wording scans cannot: the helper reads like every other call.
    """
    # A declaration may itself live in a code fence, so "defined" cannot mean
    # "mentioned outside a fence". What a missing declaration looks like is an
    # identifier the document names exactly once, in a sample that calls it.
    # A declaration introduces a name without referring to anything, so a const or
    # event listed once is complete as it stands. Only a name that is *called* while
    # never being declared is drift.
    DECLARATION = re.compile(r"\b(public|private|internal|protected|const|static|event"
                             r"|class|struct|enum|delegate|extern)\b")
    occurrences, declared = {}, set()
    for lineno, line in enumerate(text.splitlines(), 1):
        stripped = readable_code(line)
        names = re.findall(r"\b([A-Za-z_]\w*)\b", stripped)
        for name in names:
            occurrences.setdefault(name, []).append(lineno)
        if DECLARATION.search(stripped):
            declared.update(names)

    used = {}
    for lineno, line, in_f in fenced(text):
        if not in_f or line.lstrip().startswith("```"):
            continue
        stripped = readable_code(line)
        for m in re.finditer(r"\b([A-Za-z_]\w*)\b", stripped):
            # A member of a type this document did not invent is not this document's
            # to declare: "RuntimePlatform.IPhonePlayer" is one name, not two.
            owner = re.search(r"\b([A-Za-z_]\w*)\.$", stripped[:m.start()])
            if owner and FOREIGN.match(owner.group(1)):
                continue
            used.setdefault(m.group(1), lineno)

    interesting = {}
    for name, lineno in used.items():
        if FOREIGN.match(name) or len(name) < 5:
            continue
        if name in shipped_names():
            continue
        # SCREAMING_SNAKE is a compile symbol or a native constant, never a helper
        # this document was supposed to declare: UNITY_EDITOR, CLIPBOARD_EMPTY_CONTENT.
        if name.upper() == name:
            continue
        if name[0].islower() and not name.startswith("s_"):
            continue  # locals and parameters live only in the sample
        if len(occurrences.get(name, [])) > 1 or name in declared:
            continue
        interesting.setdefault(name, lineno)

    if not used:
        rep.skip("pseudo-code identifiers are declared in prose", "no code fences")
        return
    rep.check(not interesting, "pseudo-code identifiers are declared in prose",
              f"only in code: {sorted(interesting.items())[:8]}")


def walk_cs(base):
    """Yield every .cs file under base, tolerating unreadable directories.

    Native plugin bundles (.xcframework / .framework) carry symlinks such as
    `Versions/Current` that do not resolve on a Windows checkout, and rglob
    aborts the whole walk with FileNotFoundError when it meets one. Those
    bundles hold no C# anyway, so they are skipped outright and any other
    unreadable directory is stepped over rather than allowed to kill the run.
    """
    SKIP_SUFFIXES = (".framework", ".xcframework", ".bundle", ".app")
    stack = [base]
    while stack:
        current = stack.pop()
        try:
            entries = list(current.iterdir())
        except (FileNotFoundError, NotADirectoryError, PermissionError, OSError):
            continue
        for entry in entries:
            try:
                if entry.is_dir():
                    if entry.name.endswith(SKIP_SUFFIXES):
                        continue
                    stack.append(entry)
                elif entry.suffix == ".cs":
                    yield entry
            except OSError:
                continue


def check_source_citations(text, rep):
    """`Foo.cs:120-140` must point at a file that exists and is long enough."""
    cited, missing, overrun = 0, [], []
    index = {}
    for root in ("Packages", "Assets"):
        base = REPO / root
        if base.is_dir():
            for p in walk_cs(base):
                index.setdefault(p.name, p)

    for lineno, line in live_lines(text):
        for name, start, end in re.findall(r"`?(\w+\.cs):(\d+)(?:-(\d+))?", line):
            cited += 1
            path = index.get(name)
            if path is None:
                missing.append((name, lineno))
                continue
            total = len(path.read_text(encoding="utf-8").splitlines())
            last = int(end or start)
            if last > total:
                overrun.append((f"{name}:{last}", f"file has {total}", lineno))

    if not cited:
        rep.skip("C# citations resolve", "no .cs citations")
        return
    rep.check(not missing, "cited C# files exist", f"missing={missing[:6]}")
    rep.check(not overrun, "cited C# line numbers are in range", f"out of range={overrun[:6]}")


# A design numbers its own chapters: the macOS documents list new files in 4.1 and
# describe them in chapter 5, the Windows one uses 6.1 and chapter 7. Locking the
# check to one pair silently skipped every document that numbers them differently,
# which is the failure mode this whole script exists to prevent.
NEW_FILES_HEADING = re.compile(r"^### (\d+)\.1 新規作成", re.M)
DETAIL_HEADING = re.compile(r"^## (\d+)\. 実装詳細", re.M)
OS_PREFIXED_CS = re.compile(r"`((?:Android|Ios|Mac|Windows)\w+)\.cs`")


def check_file_list_matches_sections(text, rep):
    """Every runtime file in the "新規作成" list must be described in 実装詳細."""
    m = NEW_FILES_HEADING.search(text)
    if m is None:
        rep.skip("new-file list is described in 実装詳細", "no 新規作成 section")
        return
    inventory_no = m.group(1)
    body = section(text, rf"^### {inventory_no}\.1 新規作成")
    if body is None:
        rep.skip("new-file list is described in 実装詳細", f"no {inventory_no}.1 section")
        return

    # Only OS-prefixed names are project files; agent-rules requires the prefix
    # (common.md "命名: OS 接頭辞と、共通ファイルを作らない方針").
    files = re.findall(OS_PREFIXED_CS, body)
    if not files:
        rep.skip("new-file types are described in 実装詳細", f"no file names in {inventory_no}.1")
        return

    d = DETAIL_HEADING.search(text)
    if d is None:
        rep.skip("new-file types are described in 実装詳細", "no 実装詳細 chapter")
        return
    detail_no = int(d.group(1))
    detail = section(text, rf"^## {detail_no}\. 実装詳細", rf"^## {detail_no + 1}\. ")
    if detail is None:
        rep.skip("new-file types are described in 実装詳細", "no 実装詳細 chapter")
        return

    # 実装詳細 documents types, not file names: a result type declared in
    # WindowsClipboardResult.cs is described as WindowsClipboardResult.
    undocumented = sorted({f for f in files if f not in detail})
    rep.check(not undocumented, "new-file types are described in 実装詳細",
              f"not in 実装詳細: {undocumented}")


def run(path):
    text = Path(path).read_text(encoding="utf-8")
    rep = Report(path)
    check_heading_order(text, rep)
    check_section_refs(text, rep)
    check_id_sequences(text, rep)
    check_declared_counts(text, rep)
    check_repeated_counts(text, rep)
    check_code_identifiers(text, rep)
    check_source_citations(text, rep)
    check_file_list_matches_sections(text, rep)
    return rep.dump()


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    return 0 if all([run(p) for p in argv[1:]]) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
