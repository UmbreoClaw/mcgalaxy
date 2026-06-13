#!/usr/bin/env python3
"""
Authoritatively re-derive locale/en.lang from git history.

For every command file changed in the locale commit, diff the ORIGINAL version
(before localisation) against the CURRENT version. Within each replaced region,
the Nth original message-string literal corresponds to the Nth Locale.Get key
that replaced it (the conversion preserved code order). This is robust to
Chat.MessageFromOps / Chat.MessageGlobal / string concatenation calls that the
earlier regex-only recovery mis-handled.

Outputs:
  /tmp/en_rederived.lang   - proposed authoritative key=value pairs
  prints low-confidence hunks (mismatched counts) for manual review
"""
import re, subprocess, difflib, sys

BASELINE = "389055c"   # commit immediately before localisation
HEAD     = "a7f1370"   # the localisation commit

LIT   = re.compile(r'"((?:[^"\\]|\\.)*)"')
KEY   = re.compile(r'Locale\.Get\(\s*"([^"]+)"')

def sh(*args):
    return subprocess.check_output(args, text=True, stderr=subprocess.DEVNULL)

def first_message_literal(line):
    """Return the message literal on an original-source line, or None."""
    m = LIT.search(line)
    if not m: return None
    s = m.group(1)
    if len(s) < 1: return None
    return s

def keys_on(line):
    return KEY.findall(line)

def main():
    changed = sh("git","diff","--name-only",BASELINE,HEAD).split()
    cs = [f for f in changed if f.endswith(".cs")]

    derived = {}            # key -> value
    conflicts = {}          # key -> set of differing values
    low_conf = []           # (file, orig_block, cur_block)

    for f in cs:
        try:
            old = sh("git","show",f"{BASELINE}:{f}").splitlines()
            new = sh("git","show",f"{HEAD}:{f}").splitlines()
        except Exception:
            continue

        sm = difflib.SequenceMatcher(a=old, b=new, autojunk=False)
        for tag, i1, i2, j1, j2 in sm.get_opcodes():
            if tag not in ("replace",):
                continue
            orig_lines = old[i1:i2]
            cur_lines  = new[j1:j2]
            # message literals from original (only lines that have one)
            msgs = []
            for ln in orig_lines:
                lit = first_message_literal(ln)
                if lit is not None and ("Locale.Get" not in ln):
                    msgs.append(lit)
            # keys from current
            ks = []
            for ln in cur_lines:
                ks.extend(keys_on(ln))
            if not ks:
                continue
            if len(ks) == len(msgs):
                for k, v in zip(ks, msgs):
                    if k in derived and derived[k] != v:
                        conflicts.setdefault(k, set()).update([derived[k], v])
                    derived[k] = v
            else:
                low_conf.append((f, msgs, ks))

    # Write proposed pairs
    with open("/tmp/en_rederived.lang","w",encoding="utf-8") as out:
        for k in sorted(derived):
            out.write(f"{k} = {derived[k]}\n")

    print(f"Derived {len(derived)} key/value pairs from {len(cs)} files")
    print(f"Low-confidence (count mismatch) hunks: {len(low_conf)}")
    print(f"Conflicting keys (same key, different literals across hunks): {len(conflicts)}")
    for k, vals in list(conflicts.items())[:20]:
        print(f"  CONFLICT {k}: {vals}")

    # Validation gate: compare to current en.lang for the report.* block
    cur = {}
    for line in open("locale/en.lang",encoding="utf-8"):
        line=line.rstrip("\n"); s=line.strip()
        if not s or s.startswith("#") or " = " not in line: continue
        kk,vv=line.split(" = ",1); cur[kk.strip()]=vv
    print("\n--- report.* : current vs re-derived ---")
    for k in sorted(cur):
        if not k.startswith("report."): continue
        d = derived.get(k, "(not derived)")
        flag = "" if d == cur[k] else "  CHANGED"
        print(f"  {k}{flag}")
        if flag:
            print(f"      cur: {cur[k]}")
            print(f"      new: {d}")

if __name__ == "__main__":
    main()
