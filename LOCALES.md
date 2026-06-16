# MCGalaxy Locale (Translation) Guide

MCGalaxy's player-facing messages are stored in plain UTF-8 text files in the
`locale/` folder. Dropping a new file there is all it takes to add a language —
no recompile, no restart required.

---

## Quick-start: translate MCGalaxy in 5 steps

1. **Copy the English template**

   ```
   cp locale/en.lang locale/fr.lang
   ```

   Use the [BCP 47](https://en.wikipedia.org/wiki/IETF_language_tag) language
   code as the filename (`fr`, `de`, `pt`, `ja`, …).

2. **Translate the values**

   Every line looks like:

   ```
   key = English text here
   ```

   Edit only the right-hand side. Leave the key, the `=`, and any `{0}`/`{1}`
   placeholders exactly as they are.

3. **Hot-reload while the server is running**

   ```
   /Language reload
   ```

   Requires Operator rank. No restart needed.

4. **Test your locale**

   ```
   /Language fr          ← set your own client to French
   /Language list        ← confirm it appears in the list
   /Language             ← shows which locale is active
   ```

5. **Set it as the server default** (Operator+)

   ```
   /Language server fr
   ```

   Or edit `properties/server.properties` → `language = fr`.

---

## File format reference

```
# Lines starting with # are comments and are ignored.

# Plain string
kick.banned = You are banned from this server.

# Positional placeholders — {0}, {1}, … are filled in by the server.
# Do NOT reorder or remove them unless you are certain the code passes
# that argument (check en.lang to see how many exist for that key).
ban.banned_by = Banned by {0}: {1}

# Colour codes — &X where X is 0-9 or a-f.
# &S resets to the default chat colour.
rank.now_ranked = You have been ranked to {0}&S!

# Player name tokens (substituted by the chat formatter):
#   λNICK  — the player's display/nick name
#   λFULL  — rank colour + prefix + name (e.g. &aAdmin Umbre)
chat.global = λFULL&S: {0}
```

### Rules of thumb

| Do | Don't |
|----|-------|
| Translate the value (right of `=`) | Change the key (left of `=`) |
| Keep `{0}`, `{1}`, … in the same positions | Add or remove placeholders |
| Use `&S` to reset colour at the end of coloured phrases | Use colour codes not in the range `&0`–`&f` |
| Leave a line out if you haven't translated it yet (falls back to English) | Delete keys you don't want — the fallback handles it |

---

## Fallback chain

When a key is looked up for a player:

```
player's language → server default language → English (en) → key name
```

This means a **partial translation is safe**: any untranslated key
automatically shows in English.

---

## In-game commands

| Command | What it does |
|---------|-------------|
| `/Language` | Show your current language setting |
| `/Language list` | List all loaded locale codes |
| `/Language [code]` | Set your personal language |
| `/Language server [code]` | Set the server-wide default (Op+) |
| `/Language reload` | Reload all `.lang` files from disk (Op+) |

---

## Key categories

`en.lang` contains ~2034 keys grouped by prefix:

| Prefix | Covers |
|--------|--------|
| `access.*` | Visit/build permission messages |
| `ban.*` / `kick.*` | Ban and kick messages |
| `chat.*` | Chat formatting |
| `goto.*` | Map-change broadcasts |
| `gui.*` | GUI window labels (desktop app) |
| `locale.*` | `/Language` command output |
| `modaction.*` | Rank/mute/freeze broadcasts |
| `paginator.*` | List pagination footers |
| `pass.*` | Password verification flow |
| `rank.*` | Rank-change messages |
| `whois.*` | `/WhoIs` and `/Info` output |
| `[cmd].*` | Help text and output for each command |

---

## Contributing a translation

1. Fork the repository and create a branch.
2. Add your `locale/[code].lang` file.
3. Open a pull request — the CI will build automatically to confirm nothing is broken.

There is no required completeness threshold: even a 20 % translated file is
useful because English fills the gaps automatically.
