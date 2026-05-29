#!/usr/bin/env python3
"""
MCGalaxy locale key extractor and C# modifier.

For each command .cs file, finds every p.Message("literal") call and:
  1. Assigns a locale key (cmd.<name>.msg_<n>  or cmd.<name>.help_<n>)
  2. Appends the entry to locale/en_auto.lang
  3. Replaces the literal string with  Locale.Get("key", p)  in the source

Concatenation patterns (p.Message("a" + var + "b")) are left untouched —
those need manual attention.  They are reported to stdout.

Run from the repo root:
  python3 scripts/translate_commands.py [--dry-run]
"""

import os
import re
import sys
import json

DRY_RUN = '--dry-run' in sys.argv

REPO_ROOT   = '/home/user/mcgalaxy'
CMD_DIR     = os.path.join(REPO_ROOT, 'MCGalaxy', 'Commands')
EN_AUTO     = os.path.join(REPO_ROOT, 'locale', 'en_auto.lang')
SKIPPED_FILES = {
    'CmdHelp.cs', 'CmdBan.cs', 'CmdKick.cs', 'CmdMute.cs',
    'CmdFreeze.cs', 'CmdLanguage.cs',
    # helpers / base classes – no direct user messages to translate
    'Command.cs', 'Command.Helpers.cs', 'CommandParser.cs',
    'SubCommand.cs', 'EntityPropertyCmd.cs', 'DrawCmd.cs',
    'ModActionCmd.cs', 'ItemPermsCmd.cs', 'MoneyCmd.cs',
    'RoundsGameCmd.cs', 'RateMapCmds.cs', 'ReplaceCmds.cs',
    'PermissionCmds.cs',
}

# ── regex to match: p.Message("literal string")  ─────────────────────────
# Captures: method_start("  string_body  ")
# Does NOT match: p.Message(variable), p.Message("a" + b)
_SIMPLE_STR = r'"((?:[^"\\]|\\.)*?)"'
MSG_RE = re.compile(
    r'((?:p|who|pl|target|target2|source|other|sender|granter|banner|'
    r'kicker|from|to|tp|master|player|q|r|plr|you|caller|viewer|oper|'
    r'op|src|dst|admin|mod|staff|owner|guest|'
    r'Player\.Console)\s*\.\s*Message\()'
    + _SIMPLE_STR,
    re.DOTALL
)

# Also catch the console/server player variant: Player.Console.Message(...)
# And the static: Chat.MessageFrom(... "literal")  -- skipping for now

def cmd_key_name(path):
    base = os.path.basename(path).replace('.cs', '')
    if base.lower().startswith('cmd'):
        base = base[3:]
    return re.sub(r'[^a-z0-9]', '', base.lower())


def is_followed_by_concat(source, end_of_match):
    """Return True if the string literal is immediately followed by  +  ."""
    rest = source[end_of_match:]
    stripped = rest.lstrip()
    return stripped.startswith('+')


def is_help_line(s):
    """Heuristic: help text starts with &T or &H."""
    return s.startswith('&T') or s.startswith('&H')


def process_file(path):
    """
    Returns (new_source, entries, skipped_concat)
    entries = list of (key, value)
    """
    with open(path, encoding='utf-8', errors='replace') as f:
        source = f.read()

    name = cmd_key_name(path)
    entries   = []   # (key, value)
    skipped   = []   # strings we couldn't auto-handle

    help_counter = 0
    msg_counter  = 0
    replacements = []  # (start, end, new_text)

    search_from = 0
    while True:
        m = MSG_RE.search(source, search_from)
        if not m:
            break

        # Check for concatenation
        if is_followed_by_concat(source, m.end()):
            skipped.append(m.group(2))
            search_from = m.end()
            continue

        literal = m.group(2)

        # Skip very short or purely symbolic strings
        stripped = re.sub(r'&[0-9a-fA-FrRgGbByYwWsSoO]', '', literal).strip()
        if len(stripped) < 3:
            search_from = m.end()
            continue
        # Skip strings that look like identifiers/paths (no spaces, short)
        if len(stripped) < 20 and not re.search(r'\s', stripped) and not re.search(r'[{?!.,:;()\[\]]', stripped):
            search_from = m.end()
            continue

        # Assign a key
        if is_help_line(literal):
            help_counter += 1
            key = f'cmd.{name}.help{help_counter}'
        else:
            msg_counter += 1
            key = f'cmd.{name}.msg{msg_counter}'

        entries.append((key, literal))

        # Build replacement: keep the method_call portion, swap the string
        method_call = m.group(1)   # e.g. "p.Message("
        repl = f'{method_call}Locale.Get("{key}", p)'
        replacements.append((m.start(), m.end(), repl))

        search_from = m.end()

    if not replacements:
        return source, [], skipped

    # Apply replacements in reverse order to preserve offsets
    chars = list(source)
    for start, end, repl in sorted(replacements, reverse=True, key=lambda x: x[0]):
        chars[start:end] = list(repl)
    new_source = ''.join(chars)

    return new_source, entries, skipped


def main():
    cs_files = []
    for root, _dirs, files in os.walk(CMD_DIR):
        for fname in sorted(files):
            if fname.endswith('.cs') and fname not in SKIPPED_FILES:
                cs_files.append(os.path.join(root, fname))

    all_entries = []  # (filename, key, value)
    concat_report = []
    modified = 0

    for path in cs_files:
        new_src, entries, skipped = process_file(path)

        fname = os.path.basename(path)
        if entries:
            all_entries.append((fname, entries))
            if not DRY_RUN:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(new_src)
            modified += 1
            print(f'  {fname}: {len(entries)} keys')

        for s in skipped:
            concat_report.append(f'  {fname}: "{s}"')

    # Write en_auto.lang
    lines = ['# Auto-generated locale entries — do not edit by hand',
             '# Merge into locale/en.lang when satisfied\n']
    for fname, entries in all_entries:
        lines.append(f'\n# ── {fname} ' + '─' * max(0, 60 - len(fname)))
        for key, val in entries:
            lines.append(f'{key} = {val}')

    if not DRY_RUN:
        with open(EN_AUTO, 'w', encoding='utf-8') as f:
            f.write('\n'.join(lines))
        print(f'\nWrote {EN_AUTO}')
    else:
        print('\n[DRY RUN] Would write en_auto.lang with entries:')
        for fname, entries in all_entries[:3]:
            print(f'  {fname}:')
            for k, v in entries[:3]:
                print(f'    {k} = {v}')

    print(f'\nModified {modified} files.')
    if concat_report:
        print(f'\n{len(concat_report)} concatenated strings skipped (need manual attention):')
        for line in concat_report[:20]:
            print(line)
        if len(concat_report) > 20:
            print(f'  ... and {len(concat_report) - 20} more')


if __name__ == '__main__':
    main()
