#!/usr/bin/env python3
"""Mechanical migration for RobustToolbox 289+ removed PrototypeId serializers.

This script changes data-field types rather than deleting validation attributes
without replacing their behavior. It preserves YAML field names and values.
Unrecognized constructs are left intact and reported for manual follow-up.
Run ONLY on a migration branch; review the diff and compile afterwards.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SERIALIZER = re.compile(
    r"customTypeSerializer\s*:\s*typeof\s*\(\s*"
    r"(?P<class>PrototypeId(?:ValueDictionary|Dictionary|HashSet|List|Array)?Serializer)"
    r"\s*<\s*(?P<types>[^()<>]+)\s*>\s*\)",
    re.S,
)
VALIDATE = re.compile(r"\[\s*ValidatePrototypeId(?:Attribute)?<\s*(?P<type>[\w.]+)\s*>\s*\]")
DECL = re.compile(
    r"(?m)^[ \t]*(?:public|private|protected|internal)\s+"
    r"(?:(?:readonly|static|new|virtual|sealed|override|required)\s+)*"
    r"(?P<type>[\w<>,?\[\]. \t]+?)\s+(?P<name>\w+)\s*(?=[;={])"
)
OBSOLETE_USING = re.compile(
    r"(?m)^using Robust\.Shared\.Serialization\.TypeSerializers\.Implementations\.Custom\.Prototype(?:\.[\w.]+)?;\r?\n"
)

def proto_name(kind):
    kind = kind.strip()
    return "EntProtoId" if kind == "EntityPrototype" or kind.endswith(".EntityPrototype") else "ProtoId<" + kind + ">"

def convert_type(old, serializer, type_args):
    args = [x.strip() for x in type_args.split(",")]
    target = proto_name(args[-1])
    if serializer == "PrototypeIdSerializer":
        if old in ("string", "string?"):
            return target + ("?" if old.endswith("?") else "")
        return None
    if serializer == "PrototypeIdArraySerializer":
        return old.replace("string[]", target + "[]", 1) if "string[]" in old else None
    if serializer in ("PrototypeIdListSerializer", "PrototypeIdHashSetSerializer"):
        return re.sub(r"\bstring\b", target, old, count=1) if "string" in old else None
    if serializer == "PrototypeIdDictionarySerializer":
        return re.sub(r"(?<=<)\s*string\s*(?=,)", target, old, count=1) if "string" in old else None
    if serializer == "PrototypeIdValueDictionarySerializer":
        return re.sub(r",\s*string\s*(?=>)", ", " + target, old, count=1) if "string" in old else None
    return None

def next_declaration(tail):
    return DECL.search(tail[:900])

def migrate(content, relpath):
    changed = 0
    unhandled = []
    # Process from the end of the file backward so offsets to the left are stable.
    matches = list(SERIALIZER.finditer(content))
    for match in reversed(matches):
        begin = content.rfind("[", 0, match.start())
        end = content.find("]", match.end())
        if begin < 0 or end < 0 or "DataField" not in content[begin:match.start()]:
            unhandled.append((relpath, "attribute:" + match.group("class")))
            continue
        tail = content[end+1:]
        decl = next_declaration(tail)
        if not decl:
            unhandled.append((relpath, "field:" + match.group("class")))
            continue
        oldtype = decl.group("type").strip()
        newtype = convert_type(oldtype, match.group("class"), match.group("types"))
        if not newtype:
            unhandled.append((relpath, "type:" + match.group("class") + ":" + oldtype))
            continue
        # Modify the field declaration after the attribute, then remove old serializer argument.
        start, stop = decl.span("type")
        tail = tail[:start] + newtype + tail[stop:]
        attr = content[begin:end+1]
        a = match.start() - begin
        b = match.end() - begin
        attr = attr[:a] + attr[b:]
        attr = re.sub(r",\s*,", ", ", attr)
        attr = re.sub(r"\(\s*,", "(", attr)
        attr = re.sub(r",\s*\)", ")", attr)
        content = content[:begin] + attr + tail
        changed += 1

    # ValidatePrototypeId attributes were also removed from the new engine.
    for match in reversed(list(VALIDATE.finditer(content))):
        tail = content[match.end():]
        decl = next_declaration(tail)
        if not decl or decl.group("type").strip() not in ("string", "string?"):
            unhandled.append((relpath, "validation:" + match.group("type")))
            continue
        oldtype = decl.group("type").strip()
        newtype = proto_name(match.group("type")) + ("?" if oldtype.endswith("?") else "")
        start, stop = decl.span("type")
        tail = tail[:start] + newtype + tail[stop:]
        content = content[:match.start()] + tail
        changed += 1

    if changed:
        content = re.sub(r"(?m)^[ \t]+$", "", content)
        content = OBSOLETE_USING.sub("", content)
        if "using Robust.Shared.Prototypes;" not in content:
            content = "using Robust.Shared.Prototypes;\n" + content
    return content, changed, unhandled

def main():
    modified = []
    unhandled = []
    total = 0
    for folder in ("Content.Shared", "Content.Server", "Content.Client"):
        for file in (ROOT / folder).rglob("*.cs"):
            old = file.read_text(encoding="utf-8-sig")
            new, count, errors = migrate(old, str(file.relative_to(ROOT)))
            if count:
                file.write_text(new, encoding="utf-8")
                modified.append(str(file.relative_to(ROOT)))
                total += count
            unhandled.extend(errors)
    print("Migrated", total, "prototype serializer/validator sites in", len(modified), "files.")
    for line in unhandled[:250]:
        print("UNHANDLED", *line)
    print("Unhandled count:", len(unhandled))
    if not modified:
        raise SystemExit("No applicable prototype-ID migrations found")
    for file in modified:
        print("CHANGED", file)

if __name__ == "__main__":
    main()
