#!/usr/bin/env bash
set -euo pipefail

# Verify the consistency of a hand-written manual under manual/<version>/.
#
# Usage:
#   ./scripts/verify_manual.sh <version> [--strict] [--quiet]
#
# Examples:
#   ./scripts/verify_manual.sh 1.9.0
#   ./scripts/verify_manual.sh 1.9.0 --strict
#
# Checks (blocking ones decide the exit code):
#   1. image references     BLOCKING  every images/... path resolves to a real file
#   2. sample conformance   warning   code examples use the sample scene's literals and real APIs
#   3. anchors              BLOCKING  every [](#anchor) resolves to a heading
#   4. version references   BLOCKING  every version the manual names is <version>
#   5. prose style          warning   ja is です・ます体, ko is 합니다体
#   6. language parity      warning   the three languages carry the same images/headings
#   7. docs sync            BLOCKING  docs/<version>/ matches manual/<version>/
#
# Exit codes:
#   0  no blocking failure (warnings may still be present)
#   1  at least one blocking failure
#   2  bad usage / missing input
#
# Known-intentional exceptions for check 2 live in:
#   agent-rules/workflows/verify-manual/SAMPLE_CONFORMANCE_IGNORE.txt

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/verify_manual.sh <version> [--strict] [--quiet]

  <version>   Manual version to verify (e.g. 1.9.0). Reads manual/<version>/.
  --strict    Treat warnings as blocking too.
  --quiet     Print the summary only; suppress per-finding detail.

Checks 1, 3, 4 and 7 are blocking; 2, 5 and 6 are warnings unless --strict is given.
USAGE
}

if [[ ${1:-} == "-h" || ${1:-} == "--help" ]]; then
  usage
  exit 0
fi

VERSION=${1:-}
STRICT=false
QUIET=false

if [[ -z $VERSION ]]; then
  echo "error: <version> is required" >&2
  usage >&2
  exit 2
fi
shift

while [[ $# -gt 0 ]]; do
  case $1 in
    --strict) STRICT=true ;;
    --quiet)  QUIET=true ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$REPO_ROOT"

if [[ ! -d "manual/$VERSION" ]]; then
  echo "error: manual/$VERSION not found" >&2
  exit 2
fi

VERSION="$VERSION" STRICT="$STRICT" QUIET="$QUIET" python3 - <<'PYTHON'
import filecmp
import os
import re
import sys
from pathlib import Path

VERSION = os.environ["VERSION"]
STRICT = os.environ["STRICT"] == "true"
QUIET = os.environ["QUIET"] == "true"

MANUAL = Path("manual") / VERSION
DOCS = Path("docs") / VERSION
PACKAGE = Path("Packages/com.jonghyunkim.nativetoolkit")
RUNTIME = PACKAGE / "Runtime"
SAMPLE_ROOT = RUNTIME / "UI"
IGNORE_FILE = Path("agent-rules/workflows/verify-manual/SAMPLE_CONFORMANCE_IGNORE.txt")

# Localized platform headings map to (sample directory, manager class prefix).
PLATFORMS = {
    "android": ("Android", "Android"),
    "ios": ("iOS", "Ios"),
    "macos": ("macOS", "Mac"),
    "mac": ("macOS", "Mac"),
    "windows": ("Windows", "Windows"),
}

# Types the manual may legitimately name that this package does not define.
EXTERNAL_TYPES = {
    "AndroidJavaClass", "AndroidJavaObject", "AndroidJavaProxy",
    "AndroidJNI", "AndroidJNIHelper",
}

IMAGE_REF = re.compile(r"images/[A-Za-z0-9_/.-]+\.(?:png|jpg|jpeg|gif|svg)")
NOISE_FILES = {".DS_Store"}


def feature_pages():
    """English feature pages, keyed by feature name. Translations reuse their code."""
    out = {}
    for p in sorted(MANUAL.glob("*.md")):
        stem = p.stem
        if stem == "index" or stem.endswith((".ja", ".ko")):
            continue
        out[stem] = p
    return out


def strip_code_blocks(text):
    """Yield (line, in_code) for every line, tracking ``` fences."""
    in_code = False
    for line in text.split("\n"):
        if line.startswith("```"):
            in_code = not in_code
            yield line, True
            continue
        yield line, in_code


def code_blocks(text):
    """Yield (language, body) for every fenced block."""
    lang, buf, in_code = None, [], False
    for line in text.split("\n"):
        if line.startswith("```"):
            if in_code:
                yield lang, "\n".join(buf)
                lang, buf, in_code = None, [], False
            else:
                lang, in_code = line[3:].strip() or None, True
            continue
        if in_code:
            buf.append(line)


def anchor_of(title, counts):
    """GitHub's heading-anchor algorithm, including the -1/-2 duplicate suffix."""
    a = re.sub(r"[^\w\s-]", "", title.strip().lower(), flags=re.UNICODE).replace(" ", "-")
    n = counts.get(a, 0)
    counts[a] = n + 1
    return a if n == 0 else f"{a}-{n}"


# --- 1. image references (BLOCKING) ------------------------------------------

def check_images():
    findings = []
    for p in sorted(MANUAL.glob("*.md")):
        text = p.read_text(encoding="utf-8")
        for ref in sorted(set(IMAGE_REF.findall(text))):
            if not (MANUAL / ref).is_file():
                findings.append(f"{p.name} -> {ref}")
    return findings


# --- 2. sample scene conformance (warning) -----------------------------------
#
# Two comparisons, both chosen because they cannot produce false positives on
# prose a manual is entitled to invent:
#
#   a. identifier-shaped literals  "public.png", "com.example.app.custom"
#      must appear in the platform's ExampleController, so a documented UTI or
#      pasteboard name cannot drift away from the screen that produces the
#      screenshot next to it.
#   b. package types and members   IosClipboardManager.GetSnapshot, ...
#      must exist under Runtime/, so a renamed or invented API is caught.
#
# Display strings ("Copied", "Hello") are deliberately not compared: a manual
# legitimately invents them, and comparing every literal buries the real
# findings. Only `csharp` fences are read, so the AndroidManifest xml snippets
# are not treated as sample code.

IDENTIFIER_LITERAL = re.compile(r'^"[A-Za-z0-9_][A-Za-z0-9_.-]*\.[A-Za-z0-9_.-]+"$')
PACKAGE_TYPE = re.compile(r"^(?:Ios|Android|Mac|Windows)[A-Z]")


def load_ignores():
    if not IGNORE_FILE.is_file():
        return set()
    out = set()
    for line in IGNORE_FILE.read_text(encoding="utf-8").split("\n"):
        line = line.split("#", 1)[0].strip()
        if line:
            out.add(line)
            out.add(line.strip('"'))
    return out


def runtime_symbols():
    """Public surface of the package, as (type names, member names)."""
    source = "\n".join(p.read_text(encoding="utf-8") for p in RUNTIME.rglob("*.cs"))
    types = set(re.findall(r"\b(?:class|struct|enum|interface)\s+([A-Za-z0-9_]+)", source))
    # Members: everything a `public`/`internal` declaration names, whether it is a
    # method, property, event, const or field.
    members = set(re.findall(
        r"\b(?:public|internal)\s+[^;{}()\n]*?\b([A-Za-z0-9_]+)\s*(?:\(|\{|=>|=|;)", source))
    # Enum members carry no access modifier; the last one has no trailing comma.
    enum_members = set(re.findall(r"^\s+([A-Z][A-Za-z0-9_]*)\s*,?\s*$", source, flags=re.M))
    return types, types | members | enum_members


def platform_sections(text):
    """Split a manual page into (platform key, body) by its `## ` headings."""
    sections, current, buf = [], None, []
    for line in text.split("\n"):
        m = re.match(r"^## (.+)$", line)
        if m:
            if current:
                sections.append((current, "\n".join(buf)))
            key = re.sub(r"[^a-z]", "", m.group(1).strip().lower())
            current, buf = PLATFORMS.get(key), []
            continue
        if current:
            buf.append(line)
    if current:
        sections.append((current, "\n".join(buf)))
    return [(p, b) for p, b in sections if p]


def check_sample_conformance():
    findings, notes = [], []
    ignores = load_ignores()
    types, known = runtime_symbols()
    for feature, page in sorted(feature_pages().items()):
        text = page.read_text(encoding="utf-8")
        for (directory, prefix), body in platform_sections(text):
            controller = (SAMPLE_ROOT / directory / feature.capitalize()
                          / f"{prefix}{feature.capitalize()}ManagerExampleController.cs")
            where = f"{page.name} [{directory}]"
            sample = controller.read_text(encoding="utf-8") if controller.is_file() else None
            if sample is None:
                notes.append(f"{where} no ExampleController found ({controller})")
            for lang, block in code_blocks(body):
                if lang != "csharp":
                    continue
                if sample is not None:
                    for lit in sorted({m for m in re.findall(r'"[^"\n]+"', block)
                                       if IDENTIFIER_LITERAL.match(m)}):
                        if lit in ignores or lit.strip('"') in ignores:
                            continue
                        if lit not in sample:
                            findings.append(f"{where} literal not in {controller.name}: {lit}")
                # String bodies are dropped so a path or a UTI inside a literal is
                # not mistaken for a type reference.
                code = re.sub(r'"[^"\n]*"', '""', block)
                for name in sorted(set(re.findall(r"\b([A-Za-z0-9_]+)\b", code))):
                    if not PACKAGE_TYPE.match(name):
                        continue
                    if name in known or name in EXTERNAL_TYPES or name in ignores:
                        continue
                    findings.append(f"{where} type not in Runtime/: {name}")
                for owner, member in sorted(set(re.findall(
                        r"\b((?:Ios|Android|Mac|Windows)[A-Za-z0-9_]*)\.(?:Instance\.)?"
                        r"([A-Za-z0-9_]+)", code))):
                    if owner not in types or member == "Instance":
                        continue
                    if member in known or f"{owner}.{member}" in ignores:
                        continue
                    findings.append(f"{where} member not in Runtime/: {owner}.{member}")
    return findings, notes


# --- 3. table-of-contents anchors (BLOCKING) ---------------------------------

def check_anchors():
    findings = []
    for p in sorted(MANUAL.glob("*.md")):
        text = p.read_text(encoding="utf-8")
        counts, anchors = {}, set()
        for line, in_code in strip_code_blocks(text):
            if in_code:
                continue
            m = re.match(r"^(#{1,6}) (.+)$", line)
            if m:
                anchors.add(anchor_of(m.group(2), counts))
        for link in re.findall(r"\]\((#[^)]+)\)", text):
            if link[1:] not in anchors:
                findings.append(f"{p.name} -> {link}")
    return findings


# --- 4. version references (BLOCKING) ----------------------------------------
#
# This repository ships no build artifacts, so what a manual can get wrong is the
# version it names: the package Git URL, its own docs/ links, and the version
# heading in index.md. A copied manual keeps the previous version in all three.

def check_version_references():
    findings = []
    patterns = [
        (re.compile(r"com\.jonghyunkim\.nativetoolkit#([0-9]+\.[0-9]+\.[0-9]+)"), "package git url"),
        (re.compile(r"docs/([0-9]+\.[0-9]+\.[0-9]+)/"), "docs link"),
        (re.compile(r"^## ([0-9]+\.[0-9]+\.[0-9]+)\s*$", re.M), "version heading"),
    ]
    for p in sorted(MANUAL.glob("*.md")):
        text = p.read_text(encoding="utf-8")
        for pattern, label in patterns:
            for found in sorted(set(pattern.findall(text))):
                if found != VERSION:
                    findings.append(f"{p.name} {label} names {found}, not {VERSION}")
    return findings


# --- 5. prose style (warning) ------------------------------------------------

JA_PLAIN = re.compile(r"(?:する|できる|なる|ある|ない|行う|返す|持つ|使う|扱う|示す)。")
JA_POLITE = re.compile(r"(?:ます|です|ません|でした|ましょう|ください|下さい)。")
KO_PLAIN = re.compile(r"(?:한다|된다|없다|있다|아니다|같다)\.")
KO_POLITE = re.compile(r"(?:니다|시오)\.")


def check_prose_style():
    findings = []
    for p in sorted(MANUAL.glob("*.ja.md")):
        for i, (line, in_code) in enumerate(strip_code_blocks(p.read_text(encoding="utf-8")), start=1):
            if in_code or line.lstrip().startswith(("//", "#", "|")):
                continue
            for seg in re.findall(r"[^。]{0,20}。", line):
                if JA_PLAIN.search(seg) and not JA_POLITE.search(seg):
                    findings.append(f"{p.name}:{i} plain form: {seg.strip()}")
    for p in sorted(MANUAL.glob("*.ko.md")):
        for i, (line, in_code) in enumerate(strip_code_blocks(p.read_text(encoding="utf-8")), start=1):
            if in_code or line.lstrip().startswith(("//", "#", "|")):
                continue
            for seg in re.findall(r"[^.]{0,20}\.", line):
                if KO_PLAIN.search(seg) and not KO_POLITE.search(seg):
                    findings.append(f"{p.name}:{i} plain form: {seg.strip()}")
    return findings


# --- 6. language parity (warning) --------------------------------------------

def check_language_parity():
    findings = []
    for feature in sorted(feature_pages()):
        variants = {}
        for suffix in ("", ".ja", ".ko"):
            p = MANUAL / f"{feature}{suffix}.md"
            if p.is_file():
                variants[suffix or "en"] = p
        if len(variants) < 2:
            continue
        stats = {}
        for lang, p in variants.items():
            text = p.read_text(encoding="utf-8")
            imgs = set(IMAGE_REF.findall(text))
            heads = sum(1 for line, in_code in strip_code_blocks(text)
                        if not in_code and re.match(r"^#{2,6} ", line))
            stats[lang] = (imgs, heads)
        base_lang, (base_imgs, base_heads) = next(iter(stats.items()))
        for lang, (imgs, heads) in stats.items():
            if lang == base_lang:
                continue
            for missing in sorted(base_imgs - imgs):
                findings.append(f"{feature}: {lang} is missing an image {base_lang} has: {missing}")
            for extra in sorted(imgs - base_imgs):
                findings.append(f"{feature}: {lang} has an image {base_lang} lacks: {extra}")
            if heads != base_heads:
                findings.append(f"{feature}: {lang} has {heads} headings, {base_lang} has {base_heads}")
    return findings


# --- 7. docs sync (BLOCKING) -------------------------------------------------
#
# docs/<version>/ is a copy of manual/<version>/ made by scripts/publish_docs.sh.
# Editing the manual without republishing is invisible in the manual itself, so
# the difference is reported here rather than discovered after a release.

def _dir_differences(cmp_result, base=""):
    out = []
    for name in cmp_result.left_only:
        if name not in NOISE_FILES:
            out.append(f"{base}{name} is in manual/ but not docs/")
    for name in cmp_result.right_only:
        if name not in NOISE_FILES:
            out.append(f"{base}{name} is in docs/ but not manual/")
    for name in cmp_result.diff_files:
        if name not in NOISE_FILES:
            out.append(f"{base}{name} differs")
    for name, sub in cmp_result.subdirs.items():
        out += _dir_differences(sub, f"{base}{name}/")
    return out


def check_docs_sync():
    if not DOCS.is_dir():
        return [f"docs/{VERSION} does not exist; run ./scripts/publish_docs.sh {VERSION}"]
    findings = _dir_differences(filecmp.dircmp(str(MANUAL), str(DOCS)))
    if findings:
        findings.append(f"run ./scripts/publish_docs.sh {VERSION} to republish")
    latest = Path("docs/latest/VERSION.txt")
    if latest.is_file():
        published = latest.read_text(encoding="utf-8").strip()
        versions = sorted(
            (p.name for p in Path("docs").glob("*") if re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", p.name)),
            key=lambda v: [int(x) for x in v.split(".")])
        if versions and published != versions[-1]:
            findings.append(f"docs/latest points at {published}, but docs/{versions[-1]} is newer")
    return findings


# --- runner -------------------------------------------------------------------

CHECKS = [
    ("image references", "BLOCKING", lambda: (check_images(), [])),
    ("sample conformance", "warning", check_sample_conformance),
    ("anchors", "BLOCKING", lambda: (check_anchors(), [])),
    ("version references", "BLOCKING", lambda: (check_version_references(), [])),
    ("prose style (ja/ko)", "warning", lambda: (check_prose_style(), [])),
    ("language parity", "warning", lambda: (check_language_parity(), [])),
    ("docs sync", "BLOCKING", lambda: (check_docs_sync(), [])),
]

print(f"Verifying manual/{VERSION}\n")

blocking, warned = [], []
for index, (name, level, fn) in enumerate(CHECKS, start=1):
    findings, notes = fn()
    findings = list(dict.fromkeys(findings))  # the same value can appear in several fences
    notes = list(dict.fromkeys(notes))
    is_blocking = level == "BLOCKING" or STRICT
    if findings:
        status = "FAIL" if is_blocking else "WARN"
        (blocking if is_blocking else warned).append(name)
    else:
        status = "OK"
    print(f"[{index}/{len(CHECKS)}] {name:<22} {status}"
          + (f" ({len(findings)})" if findings else ""))
    if not QUIET:
        for f in findings:
            print(f"          {f}")
        for n in notes:
            print(f"          note: {n}")

print()
print(f"Blocking failures: {len(blocking)}" + (f" ({', '.join(blocking)})" if blocking else ""))
print(f"Warnings:          {len(warned)}" + (f" ({', '.join(warned)})" if warned else ""))

sys.exit(1 if blocking else 0)
PYTHON
