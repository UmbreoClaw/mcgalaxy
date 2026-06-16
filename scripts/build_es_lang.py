#!/usr/bin/env python3
"""
Build the final locale/es.lang by merging all Spanish translation sources.
Priority order (later overrides earlier):
  1. locale/es.lang (existing hand-written translations)
  2. /tmp/es_part1.lang (A-M translations - first batch)
  3. /tmp/es_part2.lang (N-Z translations - second batch)
  4. /tmp/translation.txt (cmd.bot through cmd.zonecmds - agent batch 1)
  5. /tmp/translations.txt (cmdbind through import - agent batch 2)
  6. /tmp/es_batch3.lang (inbox through undoself - agent batch 3)
  7. /tmp/mcgalaxy_spanish_translation.txt (update through zombie - agent batch 4)
  8. /tmp/es_batch5.lang (cmd.ctfadsfd through place - agent batch 5)
  9. /tmp/es_batch6.lang (place.help3 through whonick - agent batch 6)
Then report what still needs translation.
"""
import os

REPO = '/home/user/mcgalaxy'
LOCALE_DIR = os.path.join(REPO, 'locale')

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

def main():
    # Load in priority order (later overrides earlier)
    sources = [
        os.path.join(LOCALE_DIR, 'es.lang'),
        '/tmp/es_part1.lang',
        '/tmp/es_part2.lang',
        '/tmp/translation.txt',
        '/tmp/translations.txt',
        '/tmp/es_batch3.lang',
        '/tmp/mcgalaxy_spanish_translation.txt',
        '/tmp/es_batch5.lang',
        '/tmp/es_batch6.lang',
    ]
    merged = {}
    for src in sources:
        entries = load_lang(src)
        merged.update(entries)
        print(f'  {os.path.basename(src)}: {len(entries)} entries')
    print(f'Total unique ES entries: {len(merged)}')

    # Find what's still untranslated
    en = load_lang(os.path.join(LOCALE_DIR, 'en.lang'))
    untranslated = {k: v for k, v in en.items() if k not in merged}
    print(f'Still untranslated: {len(untranslated)} / {len(en)} en.lang keys')

    # Write es.lang
    header = [
        '# MCGalaxy Archivo de idioma Español (es)',
        '# Formato: clave = valor',
        '# {0}, {1}, {2} son marcadores de posición — mantenlos en las traducciones.',
        '# &X son códigos de color. λNICK, λFULL, λSHORT son tokens de nombre de jugador — NO los traduzcas.',
        '',
    ]
    lines = header[:]
    for key in sorted(merged):
        lines.append(f'{key} = {merged[key]}')

    out = os.path.join(LOCALE_DIR, 'es.lang')
    with open(out, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'\nWrote {out} ({len(merged)} entries)')

    if untranslated:
        print('\nKeys without Spanish translation (first 30):')
        for k, v in list(untranslated.items())[:30]:
            print(f'  {k} = {v}')

if __name__ == '__main__':
    main()
