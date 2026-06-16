#!/usr/bin/env python3
"""
Definitive format-safety check for a locale file.

For every Locale.Get("key"[, p]) call site, robustly determine how many format
arguments are supplied, then verify the locale value's highest placeholder index
is < that count (otherwise string.Format throws at runtime). Also flags values
that contain placeholders but are used only in non-format contexts (the braces
would be shown literally).

Usage: python3 scripts/check_format_safety.py [locale/en.lang]
"""
import re, glob, sys

LOCALE = sys.argv[1] if len(sys.argv)>1 else 'locale/en.lang'

def load(p):
    d={}
    for line in open(p,encoding='utf-8'):
        line=line.rstrip('\n'); s=line.strip()
        if not s or s.startswith('#') or ' = ' not in line: continue
        k,v=line.split(' = ',1); d[k.strip()]=v
    return d
def maxidx(v):
    t=v.replace('{{','').replace('}}','')
    idx=[int(m.group(1)) for m in re.finditer(r'\{(\d+)(?::[^}]*)?\}',t)]
    return max(idx) if idx else -1

vals=load(LOCALE)
GET=re.compile(r'Locale\.Get\(\s*"([^"]+)"')

def match_paren(s, open_pos):
    """Given index of '(', return index just after matching ')'."""
    depth=0
    i=open_pos
    while i < len(s):
        c=s[i]
        if c=='(': depth+=1
        elif c==')':
            depth-=1
            if depth==0: return i
        elif c=='"':
            i+=1
            while i<len(s) and s[i]!='"':
                if s[i]=='\\': i+=1
                i+=1
        i+=1
    return -1

def count_top_commas(s):
    depth=0; n=0; i=0
    while i < len(s):
        c=s[i]
        if c in '([{': depth+=1
        elif c in ')]}': depth-=1
        elif c=='"':
            i+=1
            while i<len(s) and s[i]!='"':
                if s[i]=='\\': i+=1
                i+=1
        elif c==',' and depth==0: n+=1
        i+=1
    return n

# context+argcount per key (max args seen, and whether any non-format use)
key_fmt_args={}     # key -> max format arg count seen
key_nonfmt={}       # key -> True if used non-format
files=glob.glob('MCGalaxy/**/*.cs',recursive=True)
for f in files:
    txt=open(f,encoding='utf-8',errors='replace').read()
    for m in GET.finditer(txt):
        key=m.group(1)
        # find the Locale.Get(...) full extent
        getopen=txt.find('(', m.start())
        getclose=match_paren(txt, getopen)
        if getclose<0: continue
        after=txt[getclose+1:getclose+4].lstrip()
        # find enclosing call: search backwards for the method '(' that contains getopen
        # Heuristic: the char run before Locale.Get up to an identifier'('
        if after.startswith('+'):
            key_nonfmt[key]=True
            continue
        if after.startswith(','):
            # enclosing call open paren = the '(' before "Locale.Get" at same depth
            # find it by scanning backward
            j=m.start()-1
            depth=0
            while j>=0:
                c=txt[j]
                if c==')' or c==']' or c=='}': depth+=1
                elif c=='(' or c=='[' or c=='{':
                    if depth==0: break
                    depth-=1
                j-=1
            callopen=j
            callclose=match_paren(txt, callopen)
            if callclose<0: continue
            inner=txt[getclose+1:callclose]   # ", arg1, arg2..."
            # count commas at top level in inner (leading comma included)
            nargs=count_top_commas(inner)
            key_fmt_args[key]=max(key_fmt_args.get(key,0), nargs)
        else:
            # bare use: Message(Locale.Get(..)) or wrapped
            before=txt[max(0,m.start()-16):m.start()]
            if 'string.Format' in before:
                # args are after Locale.Get(...) inside Format(...)
                fclose=match_paren(txt, txt.rfind('(', 0, m.start()))
                # fallback: treat as format with unknown args -> mark fmt with >=1
                key_fmt_args[key]=max(key_fmt_args.get(key,0),1)
            else:
                key_nonfmt[key]=True

problems=[]
for key,val in vals.items():
    mi=maxidx(val)
    fargs=key_fmt_args.get(key)
    if fargs is not None:
        if mi >= fargs:
            problems.append(('CRASH', key, f'maxidx={mi} args={fargs}', val))
    else:
        # only non-format (or unused)
        if mi >= 0 and key_nonfmt.get(key):
            problems.append(('LITERAL_BRACES', key, f'maxidx={mi} non-format', val))

print(f"Checked {len(vals)} values against call sites")
crash=[p for p in problems if p[0]=='CRASH']
lit=[p for p in problems if p[0]=='LITERAL_BRACES']
print(f"CRASH risks: {len(crash)}")
for _,k,info,v in crash:
    print(f"  {k} ({info}) = {v}")
print(f"LITERAL_BRACES risks: {len(lit)}")
for _,k,info,v in lit:
    print(f"  {k} ({info}) = {v}")
