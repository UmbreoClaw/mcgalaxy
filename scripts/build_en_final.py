#!/usr/bin/env python3
"""
Produce the authoritative locale/en.lang by reconciling the current file with
the git-re-derived values (/tmp/en_rederived.lang).

Rules per key (values are unescaped: \" -> " because the .lang reader does not
process C# escapes):
  * not in re-derived            -> keep current (unescaped)
  * re-derived == current        -> keep
  * denylist (bad fragments)     -> keep current
  * pause.disabled               -> apply re-derived (current's {1} crashes; 1 arg)
  * undo.physics_undone          -> "Physics were undone" (code appends " &b" + time)
  * re-derived has >= placeholders -> apply re-derived (shift fix, no format loss)
  * re-derived has fewer placeholders:
        - non-format call context -> apply re-derived (spurious placeholders removed)
        - format call context     -> keep current (placeholders are needed)
"""
import re, glob

def load(p):
    d={}
    for line in open(p,encoding='utf-8'):
        line=line.rstrip('\n'); s=line.strip()
        if not s or s.startswith('#') or ' = ' not in line: continue
        k,v=line.split(' = ',1); d[k.strip()]=v
    return d

def un(v): return v.replace('\\"','"')
def ps(v):
    t=v.replace('{{','').replace('}}','')
    return frozenset(int(m.group(1)) for m in re.finditer(r'\{(\d+)(?::[^}]*)?\}',t))

cur=load('locale/en.lang'); der=load('/tmp/en_rederived.lang')
code={f:open(f,encoding='utf-8',errors='replace').read() for f in glob.glob('MCGalaxy/**/*.cs',recursive=True)}

def context(key):
    fmt=nonfmt=False
    pat=re.compile(r'Locale\.Get\(\s*"'+re.escape(key)+r'"\s*(?:,\s*\w+\s*)?\)')
    for txt in code.values():
        for m in pat.finditer(txt):
            after=txt[m.end():m.end()+3].lstrip()
            before=txt[max(0,m.start()-14):m.start()]
            if after.startswith(','): fmt=True
            elif after.startswith('+'): nonfmt=True
            elif 'string.Format' in before: fmt=True
            else: nonfmt=True
    if fmt: return 'fmt'
    return 'nonfmt'

DENY={'cmd.botai.msg1','server.use_update'}

final={}
applied=0
for k in cur:
    cv=un(cur[k])
    if k not in der:
        final[k]=cv; continue
    dv=un(der[k])
    if dv==cv:
        final[k]=cv; continue
    if k in DENY:
        final[k]=cv; continue
    if k=='undo.physics_undone':
        final[k]='Physics were undone'; applied+=1; continue
    if k=='pause.disabled':
        final[k]=dv; applied+=1; continue
    if len(ps(dv))>=len(ps(cv)):
        final[k]=dv; applied+=1; continue
    # fewer placeholders
    if context(k)=='nonfmt':
        final[k]=dv; applied+=1
    else:
        final[k]=cv
print(f"Applied {applied} re-derived corrections")

header=[
 '# MCGalaxy English locale file',
 '# Format: key = value',
 '# {0}, {1}, ... are positional placeholders',
 '# &X are colour codes. λNICK/λFULL/λSHORT are player name tokens.',
 '',
]
with open('locale/en.lang','w',encoding='utf-8') as f:
    f.write('\n'.join(header))
    for k in sorted(final):
        f.write(f'{k} = {final[k]}\n')
print(f"Wrote locale/en.lang ({len(final)} entries)")
# sanity: any remaining escaped quotes?
rem=sum(1 for v in final.values() if '\\"' in v)
print(f"Remaining escaped quotes: {rem}")
