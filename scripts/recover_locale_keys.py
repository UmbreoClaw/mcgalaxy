#!/usr/bin/env python3
"""
For each file that has been modified (vs git HEAD), extract what strings were
replaced with Locale.Get("key") calls by comparing the old and new versions.

Produces a key=value mapping of  locale_key → original_english_string.
"""
import os
import re
import subprocess
import sys

REPO = '/home/user/mcgalaxy'

def git_show_original(relpath):
    """Return the original content of a file from HEAD."""
    try:
        result = subprocess.run(
            ['git', 'show', f'HEAD:{relpath}'],
            capture_output=True, text=True, cwd=REPO
        )
        if result.returncode == 0:
            return result.stdout
    except Exception:
        pass
    return None

def get_modified_cs_files():
    result = subprocess.run(
        ['git', 'diff', '--name-only', 'HEAD'],
        capture_output=True, text=True, cwd=REPO
    )
    return [f for f in result.stdout.splitlines() if f.endswith('.cs')]

# Pattern matching Locale.Get("key"  in current file
LOCALE_GET_RE = re.compile(r'Locale\.Get\("([^"]+)"')

# Pattern matching p.Message("string" in original file (very simple)
# Just capture any string literal (we'll match by position context)
OLD_MSG_RE = re.compile(
    r'((?:p|who|pl|target|from|to|player)\s*\.\s*Message\()'
    r'"((?:[^"\\]|\\.)*?)"',
    re.DOTALL
)

def align_keys_to_strings(orig_src, new_src):
    """
    For each Locale.Get("key") in new_src, try to find the matching
    original string literal at the same logical location in orig_src.
    Returns dict: key -> original_string
    """
    # Get ordered list of Locale.Get keys in new file
    new_keys = [(m.start(), m.group(1)) for m in LOCALE_GET_RE.finditer(new_src)]
    # Get ordered list of old string literals
    old_strs = [(m.start(), m.group(2)) for m in OLD_MSG_RE.finditer(orig_src)]

    if not new_keys or not old_strs:
        return {}

    # Simple alignment: pair them in order (they should correspond)
    result = {}
    for i, (pos, key) in enumerate(new_keys):
        if i < len(old_strs):
            result[key] = old_strs[i][1]
    return result


def main():
    modified = get_modified_cs_files()
    print(f'Scanning {len(modified)} modified C# files ...')

    # Load missing keys
    with open('/tmp/all_referenced_keys.txt') as f:
        referenced = set(l.strip() for l in f)
    with open('/tmp/all_defined_keys.txt') as f:
        defined = set(l.strip() for l in f)
    missing = referenced - defined

    recovered = {}   # key -> english_string

    for relpath in modified:
        abspath = os.path.join(REPO, relpath)
        if not os.path.exists(abspath):
            continue

        with open(abspath, encoding='utf-8', errors='replace') as f:
            new_src = f.read()

        orig_src = git_show_original(relpath)
        if not orig_src:
            continue

        # Check if this file has any missing keys
        file_keys = set(LOCALE_GET_RE.findall(new_src))
        file_missing = file_keys & missing
        if not file_missing:
            continue

        mapping = align_keys_to_strings(orig_src, new_src)
        for key, val in mapping.items():
            if key in missing and key not in recovered:
                recovered[key] = val

    print(f'Recovered {len(recovered)} / {len(missing)} missing keys')

    # Write recovered entries to a file
    out_lines = ['# Recovered locale entries from git history\n']
    for key in sorted(recovered):
        val = recovered[key]
        out_lines.append(f'{key} = {val}')

    still_missing = missing - set(recovered)
    if still_missing:
        out_lines.append('\n# ── Keys still unresolved ──────────────────────────────────')
        for key in sorted(still_missing):
            out_lines.append(f'# MISSING: {key}')

    outfile = '/tmp/recovered_locale.txt'
    with open(outfile, 'w', encoding='utf-8') as f:
        f.write('\n'.join(out_lines))

    print(f'Written to {outfile}')
    print(f'Still unresolved: {len(still_missing)}')


if __name__ == '__main__':
    main()
