#!/usr/bin/env bash
# Pre-push quality pass - everything that can be checked from the shell.
# Run from the project root: bash .claude/skills/pre-push-review/checks.sh
# It only ever reports. It never deletes, moves or rewrites anything.

cd "$(git rev-parse --show-toplevel)" || exit 1
command -v rg >/dev/null 2>&1 || { echo "ripgrep (rg) is required for these checks" >&2; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "python3 is required for the GUID index" >&2; exit 1; }

section() { printf '\n== %s ==\n' "$1"; }
report() { if [ -n "$1" ]; then printf '%s\n' "$1" | sed 's/^/   /'; else echo "   none"; fi; }

section "Missing script references"
report "$(grep -rn 'm_Script: {fileID: 0}' --include=*.unity --include=*.prefab Assets/ 2>/dev/null)"

section "Assets with no .meta"
report "$(find Assets -type f ! -name '*.meta' ! -name '.DS_Store' ! -name 'Thumbs.db' 2>/dev/null | while IFS= read -r f; do [ -f "$f.meta" ] || echo "$f"; done)"

section ".meta with no asset"
report "$(find Assets -name '*.meta' 2>/dev/null | while IFS= read -r m; do [ -e "${m%.meta}" ] || echo "$m"; done)"

section "Assets nothing references (GUID appears nowhere else)"
# Index all GUID references in one scan, including references in other .meta files.
# A GUID's own .meta does not count. Folder assets and binary files are excluded.
python3 -B - <<'PY'
from pathlib import Path
import base64, json, os, re, subprocess, sys

assets = []
for meta in sorted(Path('Assets').rglob('*.meta')):
    asset = meta.with_suffix('')
    if asset.is_dir():
        continue
    match = re.search(r'^guid: ([0-9a-fA-F]{32})\s*$', meta.read_text(errors='replace'), re.M)
    if match:
        assets.append((asset, str(meta), match.group(1)))
references, binary_files = {}, set()
if assets:
    result = subprocess.run(['rg', '--json', '--hidden', '--no-ignore', '-F', '-f', '-',
                             'Assets', 'ProjectSettings'],
                            input='\n'.join(sorted({guid for _, _, guid in assets})),
                            capture_output=True, text=True)
    if result.returncode not in (0, 1):
        sys.exit(result.stderr or 'GUID reference scan failed')
    for line in result.stdout.splitlines():
        event = json.loads(line)
        data = event['data']
        if event['type'] not in ('match', 'end'):
            continue
        path = data['path'].get('text')
        if path is None:
            path = os.fsdecode(base64.b64decode(data['path']['bytes']))
        if event['type'] == 'end' and data.get('binary_offset') is not None:
            binary_files.add(path)
        if event['type'] == 'match':
            for match in data['submatches']:
                references.setdefault(match['match']['text'], set()).add(path)
unused = [(asset, meta) for asset, meta, guid in assets
          if not (references.get(guid, set()) - binary_files - {meta})]
for asset, _ in unused:
    size = str((asset.stat().st_blocks + 1) // 2) if asset.exists() else '?'
    print('   {}  ({}K)'.format(asset, size))
if not unused:
    print('   none')
PY
[ "$?" -eq 0 ] || exit 1

section "Tracked files that should be ignored"
report "$(git ls-files | grep -E '^(Library|Temp|Obj|Build|Logs|UserSettings|Captures|Screenshots)/' 2>/dev/null)"

section "Debug leftovers in scripts"
report "$(grep -rn 'Debug\.Log\|TODO\|FIXME\|HACK' --include=*.cs Assets/ 2>/dev/null)"

section "Placeholder text in the scenes"
report "$(grep -rn 'goes here\|Lorem\|placeholder\|PLACEHOLDER\|coming soon' --include=*.unity --include=*.prefab Assets/ 2>/dev/null)"

section "Docs naming a file that is not there"
# AGENTS.md, the Claude entrypoint, docs and shared skills name files in backticks.
# A renamed or deleted script can leave an otherwise plausible sentence behind.
# Computed into a variable first, not nested straight inside report "$(...)" - that
# double nesting of quoted command substitutions is what silently corrupts $t below.
# if/[[ ]], not case: macOS ships bash 3.2 (frozen since 2007, GPLv3), whose parser
# cannot reliably close a "case...esac" when it sits inside a $(...) command
# substitution - it misreads the pattern-closing ")" against the substitution's own
# closing ")" and dies on the first ";;" with "syntax error near unexpected token".
# Confirmed with a minimal repro: identical case/esac, works standalone, breaks the
# instant it's wrapped in $(...), on this bash and no other. if/[[ ]] has no such bug.
docs_missing=$(rg --no-filename -o '`[^`]*`' AGENTS.md CLAUDE.md docs .claude/skills --glob '*.md' | tr -d '`' | sort -u | while IFS= read -r t; do
  if [[ "$t" == *' '* || "$t" == *://* || "$t" == /* || "$t" == *'*'* || "$t" == *'('* || "$t" == *')'* || "$t" == *'$'* || "$t" =~ ^\.[[:alnum:]]+$ ]]; then
    continue
  fi
  file_token=false
  if [[ "$t" == *.cs || "$t" == *.unity || "$t" == *.prefab || "$t" == *.md || "$t" == *.asset || "$t" == *.js || "$t" == *.anim || "$t" == *.controller ]]; then
    file_token=true
  fi
  if [[ "$t" == */* ]]; then
    # Ignore hierarchy paths and refs such as Content/Body or origin/main.
    if [[ "$file_token" == false && "$t" != Assets/* && "$t" != ProjectSettings/* && "$t" != Packages/* && "$t" != docs/* && "$t" != .agents/* && "$t" != .claude/* && "$t" != .codex/* && ! -d "${t%%/*}" ]]; then
      continue
    fi
    [ -e "$t" ] && continue
    git check-ignore -q "$t" 2>/dev/null && continue
    echo "MISSING PATH: $t"
  elif [[ "$file_token" == true ]]; then
    found=$( { find Assets docs .claude ProjectSettings -name "$t" 2>/dev/null; find . -maxdepth 1 -name "$t" 2>/dev/null; } | head -1)
    [ -n "$found" ] && continue
    echo "MISSING FILE: $t"
  fi
done)
report "$docs_missing"

section "Tracked shared skills and hooks AGENTS.md never mentions"
report "$(for d in .claude/skills/*/; do
  git ls-files --error-unmatch "$d" >/dev/null 2>&1 || continue
  n=$(basename "$d"); grep -Fq "$n" AGENTS.md || echo "skill: $n"
done
for f in .claude/hooks/*; do
  git ls-files --error-unmatch "$f" >/dev/null 2>&1 || continue
  n=$(basename "$f"); grep -Fq "$n" AGENTS.md || echo "hook: $n"
done)"

section "Largest tracked assets"
git ls-files -z Assets | xargs -0 du -k 2>/dev/null | sort -rn | head -5 | sed 's/^/   /'

printf '\n-- shell checks done. Run applicable Unity checks when the review includes Unity content. --\n'
