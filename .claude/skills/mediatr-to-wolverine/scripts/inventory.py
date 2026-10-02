#!/usr/bin/env python3
"""
MediatR -> Wolverine migration inventory.

Scans a .NET solution directory (C# sources + project files) and reports every
MediatR touch-point, plus Wolverine-specific risks (non-public handlers, open
generic handlers, accidental Wolverine handler candidates).

Usage:
    python3 inventory.py <solution-root> [--format markdown|json] [--check]

--check   Exit 1 if any MediatR reference remains (use as the final gate).
          Prints only the remaining references.

Regex-based on purpose (no Roslyn / dotnet SDK needed). It errs on the side of
reporting too much; review the output rather than trusting counts blindly.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, asdict, field

SKIP_DIRS = {"bin", "obj", ".git", ".vs", ".idea", "node_modules", "packages", "artifacts", "TestResults"}

# ---------------------------------------------------------------- patterns
PKG_RE = re.compile(r'<Package(?:Reference|Version)\s+Include="(MediatR[^"]*)"', re.I)

TYPE_DECL = r'(?P<mods>(?:\b(?:public|internal|private|protected|sealed|abstract|static|partial|file|readonly)\s+)*)' \
            r'(?P<kind>class|record(?:\s+(?:class|struct))?|struct)\s+(?P<name>\w+)(?P<generic><[^>{(]*>)?' \
            r'(?P<ctor>\s*\([^)]*\))?\s*:\s*(?P<bases>[^{;]+)'
TYPE_DECL_RE = re.compile(TYPE_DECL, re.S)

BASE_PATTERNS = {
    "request":               re.compile(r'\bIRequest\b(?!\w)(?!Handler|PreProcessor|PostProcessor|ExceptionHandler|ExceptionAction)(<[^>]*>)?'),
    "stream_request":        re.compile(r'\bIStreamRequest\s*<'),
    "notification":          re.compile(r'\bINotification\b(?!Handler|Publisher)'),
    "request_handler":       re.compile(r'\bIRequestHandler\s*<'),
    "stream_handler":        re.compile(r'\bIStreamRequestHandler\s*<'),
    "notification_handler":  re.compile(r'\bINotificationHandler\s*<'),
    "pipeline_behavior":     re.compile(r'\bIPipelineBehavior\s*<'),
    "stream_behavior":       re.compile(r'\bIStreamPipelineBehavior\s*<'),
    "pre_processor":         re.compile(r'\bIRequestPreProcessor\s*<'),
    "post_processor":        re.compile(r'\bIRequestPostProcessor\s*<'),
    "exception_handler":     re.compile(r'\bIRequestExceptionHandler\s*<'),
    "exception_action":      re.compile(r'\bIRequestExceptionAction\s*<'),
    "notification_publisher":re.compile(r'\bINotificationPublisher\b'),
}

CALL_PATTERNS = {
    "send_call":     re.compile(r'\.\s*Send\s*(?:<[^>]*>)?\s*\('),
    "publish_call":  re.compile(r'\.\s*Publish\s*(?:<[^>]*>)?\s*\('),
    "stream_call":   re.compile(r'\.\s*CreateStream\s*(?:<[^>]*>)?\s*\('),
}
MEDIATOR_INJECT_RE = re.compile(r'\b(IMediator|ISender|IPublisher)\b\s+(\w+)')
REGISTRATION_RE = re.compile(r'\bAddMediatR\s*\(|\bRegisterServicesFrom\w*\s*\(|\bAdd(?:Open)?Behavior\s*\(')
TEST_DOUBLE_RE = re.compile(r'(Mock<\s*(IMediator|ISender|IPublisher)\s*>|Substitute\.For<\s*(IMediator|ISender|IPublisher)\s*>|A\.Fake<\s*(IMediator|ISender|IPublisher)\s*>)')
USING_RE = re.compile(r'^\s*(?:global\s+)?using\s+MediatR(?:\.[\w.]+)?\s*;', re.M)
SHIM_RE = re.compile(r'^\s*(?:global\s+)?using\s+Wolverine\.Shims\.MediatR\s*;', re.M)
WRAPPER_RE = re.compile(r'\binterface\s+(IMediator|ISender|IPublisher)\b')
WOLVERINE_PKG_RE = re.compile(r'<Package(?:Reference|Version)\s+Include="(WolverineFx[^"]*)"', re.I)
UNIT_RE = re.compile(r'\bUnit\.Value\b|\bTask<Unit>')

# Wolverine accidental-handler heuristic: public class ...Handler/Consumer with public Handle*/Consume* method
WOLV_METHOD_RE = re.compile(r'\bpublic\s+(?:static\s+)?(?:async\s+)?[\w<>\[\],\s?.()]+?\s+(Handle|HandleAsync|Handles|HandlesAsync|Consume|ConsumeAsync|Consumes|ConsumesAsync)\s*\(\s*(?P<first>[\w<>\[\].?]+)')

TUPLE_RETURN_RE = re.compile(r'\bpublic\s+(?:static\s+)?(?:async\s+)?(?:Task<|ValueTask<)?\(\s*\w[^)]*,[^)]*\)>?\s+Handle')


@dataclass
class Finding:
    category: str
    file: str
    line: int
    name: str = ""
    detail: str = ""
    flags: list[str] = field(default_factory=list)


def line_of(text: str, idx: int) -> int:
    return text.count("\n", 0, idx) + 1


def strip_comments(src: str) -> str:
    # keep string lengths/newlines so line numbers remain valid
    def blank(m):
        return re.sub(r'[^\n]', ' ', m.group(0))
    src = re.sub(r'/\*.*?\*/', blank, src, flags=re.S)
    src = re.sub(r'//[^\n]*', blank, src)
    return src


def iter_files(root: str):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for f in filenames:
            yield os.path.join(dirpath, f)


def class_body(text: str, start: int) -> str:
    """Return the text of the {...} block starting at/after `start` (naive brace matching)."""
    i = text.find("{", start)
    if i < 0:
        return ""
    depth = 0
    for j in range(i, len(text)):
        c = text[j]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return text[i:j + 1]
    return text[i:]


def owning_project(root: str, path: str) -> str:
    """Nearest ancestor directory containing a *.csproj, relative to root."""
    d = os.path.dirname(path)
    root_abs = os.path.abspath(root)
    while True:
        try:
            if any(f.endswith(".csproj") for f in os.listdir(d)):
                for f in os.listdir(d):
                    if f.endswith(".csproj"):
                        return os.path.relpath(os.path.join(d, f), root)
        except OSError:
            pass
        if os.path.abspath(d) == root_abs or os.path.dirname(d) == d:
            return os.path.relpath(os.path.dirname(path), root) + " (no .csproj found)"
        d = os.path.dirname(d)


def scan(root: str) -> dict:
    findings: list[Finding] = []
    declared_types: set[str] = set()
    accidental: list[tuple[Finding, str]] = []
    projects_with_pkg: dict[str, list[str]] = defaultdict(list)
    cs_files = 0

    for path in iter_files(root):
        rel = os.path.relpath(path, root)
        lower = path.lower()

        if lower.endswith((".csproj", ".fsproj", ".vbproj", ".props", ".targets")):
            try:
                txt = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            for m in PKG_RE.finditer(txt):
                projects_with_pkg[rel].append(m.group(1))
                findings.append(Finding("package_reference", rel, line_of(txt, m.start()), m.group(1)))
            if lower.endswith(".csproj") and re.search(r'domain', os.path.basename(path), re.I):
                for m in WOLVERINE_PKG_RE.finditer(txt):
                    findings.append(Finding("domain_wolverine_reference", rel, line_of(txt, m.start()), m.group(1),
                                            "Domain project must not depend on Wolverine"))
            continue

        if not lower.endswith(".cs"):
            continue
        cs_files += 1
        try:
            raw = open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        txt = strip_comments(raw)
        is_test = bool(re.search(r'(^|[\\/._])(tests?|specs?)([\\/._]|$)', rel, re.I))
        declared_types.update(re.findall(r'\b(?:class|record|struct|interface|enum)\s+(\w+)', txt))
        uses_mediatr = bool(USING_RE.search(txt)) or "MediatR." in txt

        for m in USING_RE.finditer(txt):
            findings.append(Finding("using_directive", rel, line_of(txt, m.start()), m.group(0).strip()))
        for m in SHIM_RE.finditer(txt):
            findings.append(Finding("shim_usage", rel, line_of(txt, m.start()), m.group(0).strip(),
                                    "Shims are not allowed: the migration must end in native Wolverine handlers"))
        for m in WRAPPER_RE.finditer(txt):
            findings.append(Finding("mediatr_wrapper", rel, line_of(txt, m.start()), m.group(1),
                                    "Home-made MediatR-style abstraction; call IMessageBus directly"))

        # --- type declarations implementing MediatR interfaces
        for m in TYPE_DECL_RE.finditer(txt):
            bases = m.group("bases")
            name = m.group("name") + (m.group("generic") or "")
            mods = m.group("mods") or ""
            ln = line_of(txt, m.start())
            for cat, pat in BASE_PATTERNS.items():
                if not pat.search(bases):
                    continue
                flags = []
                is_public = "public" in mods
                if not is_public:
                    flags.append("NOT_PUBLIC: Wolverine requires public handlers/messages")
                if cat.endswith("handler") or cat in ("pipeline_behavior", "pre_processor", "post_processor"):
                    if m.group("generic") and cat in ("request_handler", "notification_handler", "stream_handler"):
                        flags.append("OPEN_GENERIC: Wolverine does not support open generic handlers")
                if cat in ("request_handler", "notification_handler") and not re.search(r'(Handler|Consumer)$', m.group("name")):
                    flags.append("NAME: class name must end with Handler/Consumer or use [WolverineHandler]")
                if cat in ("request_handler", "notification_handler"):
                    body = class_body(txt, m.end())
                    if TUPLE_RETURN_RE.search(body):
                        flags.append("TUPLE_RETURN: Wolverine treats tuple elements as separate return values")
                detail = re.sub(r'\s+', ' ', bases.strip())[:160]
                f = Finding(cat, rel, ln, name, detail, flags)
                if cat in ("request_handler", "notification_handler", "stream_handler"):
                    f.detail = detail + "  | project: " + owning_project(root, path)
                findings.append(f)

        # --- injection of IMediator/ISender/IPublisher
        for m in MEDIATOR_INJECT_RE.finditer(txt):
            if not uses_mediatr and m.group(1) == "IPublisher":
                continue  # IPublisher is a common name in other libs
            findings.append(Finding("mediator_injection", rel, line_of(txt, m.start()), m.group(1), m.group(0)))

        # --- call sites (only in files that use MediatR, to limit noise)
        if uses_mediatr or MEDIATOR_INJECT_RE.search(txt):
            for cat, pat in CALL_PATTERNS.items():
                for m in pat.finditer(txt):
                    line_txt = raw.splitlines()[line_of(txt, m.start()) - 1].strip()
                    flags = []
                    if cat == "send_call" and re.search(r'^\s*await\s+[\w.]+\s*\.\s*Send', line_txt):
                        flags.append("RESULT_DISCARDED: check handler return type before using non-generic InvokeAsync")
                    findings.append(Finding(cat, rel, line_of(txt, m.start()), "", line_txt[:200], flags))

        seen_reg_lines = set()
        for m in REGISTRATION_RE.finditer(txt):
            ln = line_of(txt, m.start())
            if ln in seen_reg_lines:
                continue
            seen_reg_lines.add(ln)
            line_txt = raw.splitlines()[ln - 1].strip()
            findings.append(Finding("registration", rel, ln, "", line_txt[:200]))

        for m in TEST_DOUBLE_RE.finditer(txt):
            findings.append(Finding("test_double", rel, line_of(txt, m.start()), m.group(0)))

        for m in UNIT_RE.finditer(txt):
            if uses_mediatr:
                findings.append(Finding("unit_usage", rel, line_of(txt, m.start()), m.group(0)))

        # --- Wolverine accidental-handler candidates (classes that are NOT MediatR handlers)
        if not is_test:
            for cm in re.finditer(r'\bpublic\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+(\w+(?:Handler|Consumer))\b(?P<rest>[^{]*)', txt):
                rest = cm.group("rest")
                if any(p.search(rest) for p in BASE_PATTERNS.values()):
                    continue
                if "abstract" in txt[cm.start():cm.start() + 60]:
                    continue
                body = class_body(txt, cm.end())
                mm = WOLV_METHOD_RE.search(body)
                if mm:
                    first = mm.group('first')
                    accidental.append((Finding(
                        "wolverine_accidental_handler", rel, line_of(txt, cm.start()), cm.group(1),
                        f"{mm.group(1)}({first} ...) would be discovered as a Wolverine handler for '{first}'",
                        ["Add [WolverineIgnore] unless this is meant to be a message handler"]), first))

    # Only flag candidates whose "message" type is not declared in this solution
    # (framework/library types such as AuthorizationHandlerContext or Exception).
    for f, first in accidental:
        base = re.sub(r'<.*', '', first).rstrip('?[]').split('.')[-1]
        if base not in declared_types:
            findings.append(f)

    return {"root": os.path.abspath(root), "cs_files_scanned": cs_files,
            "projects_with_mediatr_packages": dict(projects_with_pkg),
            "findings": [asdict(f) for f in findings]}


ORDER = [
    ("package_reference", "Package references"),
    ("registration", "DI registration (AddMediatR / behaviors)"),
    ("request", "Request types (IRequest)"),
    ("stream_request", "Stream requests (IStreamRequest) — no direct equivalent"),
    ("notification", "Notification types (INotification)"),
    ("request_handler", "Request handlers"),
    ("notification_handler", "Notification handlers"),
    ("stream_handler", "Stream handlers — no direct equivalent"),
    ("pipeline_behavior", "Pipeline behaviors → middleware"),
    ("stream_behavior", "Stream pipeline behaviors"),
    ("pre_processor", "Pre-processors → Before middleware"),
    ("post_processor", "Post-processors → After middleware"),
    ("exception_handler", "Exception handlers — no direct equivalent"),
    ("exception_action", "Exception actions"),
    ("notification_publisher", "Custom notification publishers"),
    ("mediator_injection", "IMediator / ISender / IPublisher injections"),
    ("send_call", "Send(...) call sites"),
    ("publish_call", "Publish(...) call sites — read notifications.md"),
    ("stream_call", "CreateStream(...) call sites"),
    ("unit_usage", "Unit usages"),
    ("test_double", "Test doubles of IMediator/ISender/IPublisher"),
    ("using_directive", "using MediatR directives"),
    ("shim_usage", "Wolverine.Shims.MediatR usages (not allowed)"),
    ("mediatr_wrapper", "Home-made IMediator/ISender/IPublisher abstractions"),
    ("domain_wolverine_reference", "Domain projects referencing WolverineFx"),
    ("wolverine_accidental_handler", "⚠ Classes Wolverine would discover as handlers by accident"),
]

# Anything in these categories means MediatR is still present
REMAINING = {k for k, _ in ORDER} - {"wolverine_accidental_handler", "send_call", "publish_call", "stream_call", "unit_usage"}


def to_markdown(report: dict) -> str:
    by_cat = defaultdict(list)
    for f in report["findings"]:
        by_cat[f["category"]].append(f)
    out = [f"# MediatR inventory\n", f"Root: `{report['root']}`  ", f"C# files scanned: {report['cs_files_scanned']}\n",
           "## Summary\n", "| Category | Count | Flagged |", "|---|---:|---:|"]
    for key, title in ORDER:
        items = by_cat.get(key, [])
        if items:
            flagged = sum(1 for i in items if i["flags"])
            out.append(f"| {title} | {len(items)} | {flagged or ''} |")
    if report["projects_with_mediatr_packages"]:
        out.append("\n## Projects referencing MediatR packages\n")
        for p, pk in sorted(report["projects_with_mediatr_packages"].items()):
            out.append(f"- `{p}`: {', '.join(sorted(set(pk)))}")
    # handler assemblies hint
    handler_dirs = sorted({f["detail"].split("| project: ")[-1] for f in report["findings"]
                           if f["category"] in ("request_handler", "notification_handler", "stream_handler")})
    if handler_dirs:
        out.append("\n## Projects containing handlers\n")
        out.append("Each project here needs `opts.Discovery.IncludeAssembly(...)` (or `[assembly: WolverineModule]`) unless it is the host.\n")
        out += [f"- `{d}`" for d in handler_dirs]
    for key, title in ORDER:
        items = by_cat.get(key, [])
        if not items:
            continue
        out.append(f"\n## {title} ({len(items)})\n")
        for i in sorted(items, key=lambda x: (x["file"], x["line"])):
            name = f" **{i['name']}**" if i["name"] else ""
            detail = f" — `{i['detail']}`" if i["detail"] else ""
            out.append(f"- `{i['file']}:{i['line']}`{name}{detail}")
            for fl in i["flags"]:
                out.append(f"  - ⚠ {fl}")
    return "\n".join(out) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("root")
    ap.add_argument("--format", choices=["markdown", "json"], default="markdown")
    ap.add_argument("--check", action="store_true", help="exit 1 if MediatR references remain")
    args = ap.parse_args()

    if not os.path.isdir(args.root):
        print(f"Not a directory: {args.root}", file=sys.stderr)
        return 2
    report = scan(args.root)

    if args.check:
        remaining = [f for f in report["findings"] if f["category"] in REMAINING]
        accidental = [f for f in report["findings"] if f["category"] == "wolverine_accidental_handler"]
        if remaining:
            print(f"FAIL: {len(remaining)} item(s) block a complete migration:")
            for f in sorted(remaining, key=lambda x: (x["file"], x["line"])):
                print(f"  {f['file']}:{f['line']}  [{f['category']}] {f['name'] or f['detail']}")
        else:
            print("OK: no MediatR references, shims, MediatR-style wrappers or Domain->Wolverine references found.")
            print("    Also run: dotnet list package --include-transitive | grep -i mediatr")
        if accidental:
            print(f"WARN: {len(accidental)} class(es) may be discovered as Wolverine handlers by accident:")
            for f in accidental:
                print(f"  {f['file']}:{f['line']}  {f['name']} — {f['detail']}")
        return 1 if remaining else 0

    if args.format == "json":
        print(json.dumps(report, indent=2))
    else:
        print(to_markdown(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
