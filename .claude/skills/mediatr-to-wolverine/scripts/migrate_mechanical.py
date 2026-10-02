#!/usr/bin/env python3
"""
Mechanical part of the MediatR -> Wolverine migration (style A, conservative).

Does ONLY the edits that are purely syntactic, so every run produces the same result:

  1. Removes MediatR marker interfaces from messages and handlers:
       ": IRequest<T>" / ", IRequest<T>" / ": IRequest", ": INotification",
       ": IRequestHandler<..>" / ": INotificationHandler<..>"   (single-line declarations)
  2. Removes `using MediatR;`.
  3. Replaces IMediator / ISender / IPublisher with IMessageBus (field type, ctor parameter,
     `_mediator` -> `_bus`, `mediator` -> `bus`) and adds `using Wolverine;`.
  4. Rewrites `.Publish(<notification>, ...)` into `.InvokeAsync(...)` (default option 1, flagged CHECK) and
     `.Send(<message>, ...)` into `.InvokeAsync<TResponse>(<message>, ...)`
     (non-generic `.InvokeAsync(...)` for plain IRequest). TResponse comes from the ORIGINAL
     IRequest<T> declaration of the message; `new X(...)` and variables declared as
     `var v = new X(...)` are resolved. Anything else is reported as MANUAL.
  5. Adds the `using` directives the explicit TResponse needs (Result, DTOs, ...).

It does NOT touch: DI registration (AddMediatR, behaviors), behaviors/processors, notification
semantics beyond the default Publish -> InvokeAsync, tests, packages. Those are decisions (see SKILL.md steps 3, 4, 7, 8).

Usage:
    python migrate_mechanical.py <solution-root>            # dry run: prints what would change
    python migrate_mechanical.py <solution-root> --apply    # writes the files

Skips bin/obj/.git and Internal/Generated. Preserves BOM and line endings. Regex-based on
purpose (no Roslyn): review the diff and run `inventory.py --check` afterwards.
"""
from __future__ import annotations

import argparse
import os
import re
import sys

SKIP_DIRS = {"bin", "obj", ".git", ".vs", ".idea", "node_modules", "packages", "artifacts", "TestResults", "Generated"}

DECL_RE = re.compile(r'\b(?:record\s+struct|record\s+class|record|class|struct)\s+(\w+)')
NS_RE = re.compile(r'^\s*namespace\s+([\w.]+)\s*[;{]', re.M)
IREQUEST_RE = re.compile(r'\bIRequest\b')
SEND_RE = re.compile(r'\b(_?\w+)\.Send\(\s*(new\s+)?(\w+)')
MEDIATOR_TYPES = ("IMediator", "ISender", "IPublisher")


def iter_cs(root):
    for dp, dn, fn in os.walk(root):
        dn[:] = [d for d in dn if d not in SKIP_DIRS]
        for f in fn:
            if f.endswith(".cs") and not f.endswith(".g.cs"):
                yield os.path.join(dp, f)


def read(path):
    b = open(path, "rb").read()
    bom = b.startswith(b"\xef\xbb\xbf")
    return b.decode("utf-8-sig"), bom


def write(path, text, bom):
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def angle_arg(text, start):
    """text[start] == '<'. Returns the balanced generic argument and the index after '>'."""
    depth = 0
    for i in range(start, len(text)):
        if text[i] == "<":
            depth += 1
        elif text[i] == ">":
            depth -= 1
            if depth == 0:
                return text[start + 1:i].strip(), i + 1
    return None, start


def strip_line_comments(text):
    return "\n".join("" if ln.lstrip().startswith("//") else ln for ln in text.split("\n"))


def collect(root):
    """Pass 1 over ORIGINAL sources: message -> response type, type -> namespace."""
    responses, type_ns = {}, {}
    for path in iter_cs(root):
        text, _ = read(path)
        code = strip_line_comments(text)
        m = NS_RE.search(code)
        ns = m.group(1) if m else ""
        for d in DECL_RE.finditer(code):
            type_ns.setdefault(d.group(1), ns)
        for m in IREQUEST_RE.finditer(code):
            decls = list(DECL_RE.finditer(code, 0, m.start()))
            if not decls:
                continue
            name = decls[-1].group(1)
            end = m.end()
            if end < len(code) and code[end] == "<":
                arg, _ = angle_arg(code, end)
                if arg:
                    responses[name] = re.sub(r"\s+", " ", arg)
            else:
                responses.setdefault(name, "")  # plain IRequest: no response
    return responses, type_ns


def transform(path, text, responses, type_ns, manual):
    new = text
    changed_bus = False

    # 1/2: interfaces and using
    out = []
    for line in new.split("\n"):
        if not line.lstrip().startswith("//"):
            line = re.sub(r",\s*IRequest(?:<.*>)?\s*;", ";", line)
            line = re.sub(r"\s*:\s*IRequest(?:<.*>)?\s*;", ";", line)
            line = re.sub(r",\s*IRequestHandler<.*>(\r?)$", r"\1", line)
            line = re.sub(r"\s*:\s*IRequestHandler<.*>(\r?)$", r"\1", line)
            line = re.sub(r"\s*:\s*INotification\s*;", ";", line)
            line = re.sub(r"\s*:\s*INotificationHandler<.*>(\r?)$", r"\1", line)
        out.append(line)
    new = "\n".join(out)
    new = re.sub(r"^using MediatR;\r?\n", "", new, flags=re.M)

    # 3: mediator types -> IMessageBus
    if any(t in new for t in MEDIATOR_TYPES):
        for t in MEDIATOR_TYPES:
            new = re.sub(r"\b%s\s+_?(?:mediator|sender|publisher)\b" % t,
                         lambda m: "IMessageBus " + ("_bus" if "_" in m.group(0) else "bus"), new)
        new = re.sub(r"\b_(?:mediator|sender|publisher)\b", "_bus", new)
        new = re.sub(r"\b(?:mediator|sender|publisher)\b(?=\s*[;,)])", "bus", new)
        changed_bus = True

    # 4: Send -> InvokeAsync<T>
    def repl(m):
        target, is_new, ident = m.group(1), m.group(2), m.group(3)
        msg = ident
        if not is_new:
            decl = re.search(r"\b(?:var|\w+)\s+%s\s*=\s*new\s+(\w+)" % re.escape(ident), new[:m.start()])
            if not decl:
                manual.append((path, "Send(%s, ...): cannot infer the message type" % ident))
                return m.group(0)
            msg = decl.group(1)
        if msg not in responses:
            manual.append((path, "Send(%s ...): no IRequest declaration found for '%s'" % (ident, msg)))
            return m.group(0)
        resp = responses[msg]
        call = "%s.InvokeAsync%s(" % (target, "<%s>" % resp if resp else "")
        return call + m.group(0)[len(target) + len(".Send("):]

    if changed_bus or ".Send(" in new:
        new = SEND_RE.sub(repl, new)

    # 4b: Publish -> InvokeAsync (default notification option 1: inline, awaited, exceptions propagate)
    if changed_bus:
        def pub(m):
            manual.append((path, "CHECK Publish -> InvokeAsync: make sure the notification has at least one handler "
                                 "(zero handlers throws in Wolverine); see references/notifications.md"))
            return m.group(1) + ".InvokeAsync("
        new = re.sub(r"\b(_?bus)\.Publish\(", pub, new)

    # 5: usings for explicit response types + Wolverine
    if changed_bus or "InvokeAsync" in new:
        needed = set()
        if "using Wolverine;" not in new:
            needed.add("Wolverine")
        file_ns = (NS_RE.search(new).group(1) if NS_RE.search(new) else "")
        for m in re.finditer(r"InvokeAsync<(.*?)>\(", new):
            for tok in re.findall(r"\w+", m.group(1)):
                ns = type_ns.get(tok)
                if ns and ns != file_ns and not file_ns.startswith(ns + "."):
                    if ("using %s;" % ns) not in new:
                        needed.add(ns)
        if needed:
            nl = "\r\n" if "\r\n" in new else "\n"
            add = "".join("using %s;%s" % (n, nl) for n in sorted(needed))
            u = re.search(r"^using .*;\r?\n", new, re.M)
            new = (new[:u.start()] + add + new[u.start():]) if u else (add + nl + new)

    return new


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("root")
    ap.add_argument("--apply", action="store_true", help="write changes (default is a dry run)")
    args = ap.parse_args()
    if not os.path.isdir(args.root):
        print("Not a directory: %s" % args.root, file=sys.stderr)
        return 2
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")

    responses, type_ns = collect(args.root)
    manual, changed = [], []
    for path in iter_cs(args.root):
        text, bom = read(path)
        new = transform(path, text, responses, type_ns, manual)
        if new != text:
            changed.append(path)
            if args.apply:
                write(path, new, bom)

    print("%s: %d file(s) %s" % ("APPLIED" if args.apply else "DRY RUN", len(changed), "changed" if args.apply else "would change"))
    for p in changed:
        print("  " + os.path.relpath(p, args.root))
    print("Messages with a known response type: %d" % len(responses))
    if manual:
        print("MANUAL (%d) - finish these by hand:" % len(manual))
        for p, why in manual:
            print("  %s: %s" % (os.path.relpath(p, args.root), why))
    print("Not touched (decisions): AddMediatR/behavior registration, behaviors, notification semantics (Publish becomes InvokeAsync, flagged CHECK), tests, packages.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
