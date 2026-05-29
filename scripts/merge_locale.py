#!/usr/bin/env python3
"""
Merge all locale sources into a single clean locale/en.lang.

Priority (highest → lowest, later sources can override earlier):
  1. locale/en.lang              — hand-written baseline
  2. staging_chat_fun_other_en   — agent-generated Chat/Fun/other
  3. staging_info_maint_en       — agent-generated Information/Maintenance
  4. en_auto.lang                — Python-script-generated
  5. /tmp/recovered_locale.txt   — git-diff recovered (partially wrong)

Then applies manual overrides for known bad entries.
"""
import os, re

REPO = '/home/user/mcgalaxy'
LOCALE_DIR = os.path.join(REPO, 'locale')

SOURCES = [
    os.path.join(LOCALE_DIR, 'en.lang'),
    os.path.join(LOCALE_DIR, 'staging_chat_fun_other_en.lang'),
    os.path.join(LOCALE_DIR, 'staging_info_maint_en.lang'),
    os.path.join(LOCALE_DIR, 'en_auto.lang'),
    '/tmp/recovered_locale.txt',
]

def load_lang(path):
    entries = {}
    if not os.path.exists(path):
        return entries
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            idx = line.index(' = ') if ' = ' in line else -1
            if idx == -1:
                continue
            key = line[:idx].strip()
            val = line[idx+3:]
            entries[key] = val
    return entries

# Manual overrides — these are either missing or wrong in the automated sources
MANUAL = {
    # ── CmdCopy (alignment was completely off in recovered) ──────────────────
    'copy.no_such_copy':      'No such copy exists.',
    'copy.deleted_copy':      'Deleted copy {0}',
    'copy.no_saved_copies':   'You have no saved copies',
    'copy.set_offset':        'Set offset of where to paste from.',
    'copy.too_many_blocks':   'You tried to copy {0} blocks. You cannot copy more than {1} blocks.',
    'copy.copied_blocks':     'Copied &a{0} &Sblocks, origin at ({1}, {2}, {3}) corner',
    'copy.copy_air_hint':     'To also copy air blocks, use &T/Copy Air',
    'copy.place_offset_block':'Place a block to determine where to paste from',
    'copy.max_copies':        'You can only save a maxmium of 15 copies. /copy delete some.',
    'copy.not_copied_yet':    'You haven\'t copied anything yet',
    'copy.saved_copy':        'Saved copy as {0}',
    'copy.loaded_copy':       'Loaded copy from {0}',
    'copy.help1': '&T/Copy &H- Copies the blocks in an area.',
    'copy.help2': '&T/Copy save [name] &H- Saves what you have copied.',
    'copy.help3': '&T/Copy load [name] &H- Loads what you have saved.',
    'copy.help4': '&T/Copy delete [name] &H- Deletes the specified copy.',
    'copy.help5': '&T/Copy list &H- Lists all saved copies you have',
    'copy.help6': '&T/Copy cut &H- Copies the blocks in an area, then removes them.',
    'copy.help7': '&T/Copy air &H- Copies the blocks in an area, including air.',
    'copy.help8': '/Copy @ - @ toggle for all the above, gives you a third click after copying that determines where to paste from',
    # ── CmdLockdown (alignment off — Chat.MessageGlobal not in OLD_MSG_RE) ──
    'lockdown.unlocked':     'Map {0} was unlocked',
    'lockdown.unlocked_ops': 'Map {0} unlocked by: λNICK',
    'lockdown.locked':       'Map {0} was locked',
    'lockdown.locked_ops':   'Map {0} locked by: λNICK',
    'lockdown.help1':        '&T/Lockdown [level]',
    'lockdown.help2':        '&HPrevents new players from joining that level.',
    'lockdown.help3':        '&HUsing /lockdown again will unlock that level',
    # ── CmdCopyLVL ──────────────────────────────────────────────────────────
    'copylvl.help3': '&HNote: The level\'s BlockDB is not copied.',
    # ── CmdDeleteLvl ────────────────────────────────────────────────────────
    'deletelvl.help5': '&H-Permanently- deletes [backup] of [level].',
    # ── CmdHide ─────────────────────────────────────────────────────────────
    'hide.help2': '&T/Hide silent &H- Hides without showing join/leave message',
    'hide.help3': '&HUse &T/OHide &Hto hide other players.',
    # ── CmdLine ─────────────────────────────────────────────────────────────
    'line.help5': '&HLength optionally specifies max number of blocks in the line',
    # ── CmdPaste ────────────────────────────────────────────────────────────
    'paste.help4': '&4BEWARE: &SThe blocks will always be pasted in a set direction',
    # ── CmdPause ────────────────────────────────────────────────────────────
    'pause.help2': '&HPauses physics on the given level for the given number of seconds.',
    'pause.help3': '&H  If [level] is not given, pauses physics on the current level.',
    'pause.help4': '&H  If [seconds] is not given, pauses physics for 30 seconds.',
    # ── CmdPhysics ──────────────────────────────────────────────────────────
    'physics.help5': '&T/Physics kill &H- Sets physics to 0 on all loaded levels.',
    # ── CmdRenameLvl ────────────────────────────────────────────────────────
    'renamelvl.help2': '&HRenames [level] to [new name]',
    # ── CmdReport ───────────────────────────────────────────────────────────
    'report.help4': '&T/Report delete [player] &H- Deletes reports for that player.',
    'report.help5': '&T/Report clear &H- Clears &call&H reports.',
    # ── CmdRestartPhysics ───────────────────────────────────────────────────
    'restartphysics.help4': '/rp revert takes block names',
    # ── CmdSave ─────────────────────────────────────────────────────────────
    'save.help4': '&T/Save [level] [name] &H- Backups the level with a given restore name',
    # ── CmdUndoPlayer ───────────────────────────────────────────────────────
    'undo.help4': '&HOnly undoes block changes in the specified region.',
    # ── CmdUnflood ──────────────────────────────────────────────────────────
    'unflood.help3': '&H  If [liquid] is \\"all\\", unfloods the map of all liquids.',
    # ── Keys that need {0} format args added (Chat.MessageGlobal calls) ─────
    'copylvl.copied':    '{0} &Shas been copied to {1}',
    'deletelvl.deleted': 'Level {0} &Shas been deleted.',
    'renamelvl.renamed': 'Level {0} &Swas renamed to {1}',
    'save.all_saved':    'All levels have been saved.',
    'pause.reenabled':   'Physics on {0} &Shas been re-enabled.',
    'pause.disabled':    'Physics on {0} &Shas been disabled for {1} seconds.',
    'vote.started':      '&2Vote started - {0}',
    'vote.results':      '&2Vote results - yes: {0}, no: {1}',
}

def main():
    merged = {}
    for src in SOURCES:
        entries = load_lang(src)
        merged.update(entries)
        print(f'  {os.path.basename(src)}: {len(entries)} entries')

    # Apply manual overrides
    merged.update(MANUAL)
    print(f'  manual overrides: {len(MANUAL)} entries')
    print(f'Total unique entries: {len(merged)}')

    # Report still-missing keys
    with open('/tmp/all_cmd_keys.txt') as f:
        all_keys = set(l.strip() for l in f if l.strip())
    missing = all_keys - set(merged)
    print(f'Still missing: {len(missing)} / {len(all_keys)} keys')
    if missing:
        for k in sorted(missing)[:20]:
            print(f'  MISSING: {k}')

    # Write merged en.lang
    out = os.path.join(LOCALE_DIR, 'en.lang')
    lines = [
        '# MCGalaxy English locale file',
        '# Format: key = value',
        '# {0}, {1}, ... are positional placeholders',
        '# &X are colour codes. λNICK/λFULL/λSHORT are player name tokens.',
        '',
    ]
    for key in sorted(merged):
        lines.append(f'{key} = {merged[key]}')

    with open(out, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'\nWrote {out} ({len(merged)} entries)')

if __name__ == '__main__':
    main()
