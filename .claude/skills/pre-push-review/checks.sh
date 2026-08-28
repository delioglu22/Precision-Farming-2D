#!/usr/bin/env bash
# Pre-push quality pass - everything that can be checked from the shell.
# Run from anywhere inside the repo:  bash .claude/skills/pre-push-review/checks.sh
# It only ever reports. It never deletes, moves or rewrites anything.

cd "$(git rev-parse --show-toplevel)" || exit 1

section() { printf '\n== %s ==\n' "$1"; }
report() { if [ -n "$1" ]; then printf '%s\n' "$1" | sed 's/^/   /'; else echo "   none"; fi; }

section "Missing script references"
report "$(grep -rn 'm_Script: {fileID: 0}' --include=*.unity --include=*.prefab Assets/ 2>/dev/null)"

section "Assets with no .meta"
report "$(find Assets -type f ! -name '*.meta' 2>/dev/null | while IFS= read -r f; do [ -f "$f.meta" ] || echo "$f"; done)"

section ".meta with no asset"
report "$(find Assets -name '*.meta' 2>/dev/null | while IFS= read -r m; do [ -e "${m%.meta}" ] || echo "$m"; done)"

section "Assets nothing references (GUID appears nowhere else)"
# Folders are skipped: a folder's GUID is legitimately referenced by nothing.
# A scene can also look unreferenced - it is reached through the build settings.
report "$(find Assets -name '*.meta' 2>/dev/null | while IFS= read -r m; do
  a="${m%.meta}"
  [ -d "$a" ] && continue
  g=$(grep -m1 '^guid: ' "$m" 2>/dev/null | cut -d' ' -f2 | tr -d '\r')
  [ -z "$g" ] && continue
  refs=$(grep -rl --binary-files=without-match "$g" Assets ProjectSettings 2>/dev/null | grep -v "^$m$" | wc -l)
  [ "$refs" -eq 0 ] && printf '%s  (%sK)\n' "$a" "$(du -k "$a" 2>/dev/null | cut -f1)"
done)"

section "Tracked files that should be ignored"
report "$(git ls-files | grep -E '^(Library|Temp|Obj|Build|Logs|UserSettings|Captures|Screenshots)/' 2>/dev/null)"

section "Debug leftovers in scripts"
report "$(grep -rn 'Debug\.Log\|TODO\|FIXME\|HACK' --include=*.cs Assets/ 2>/dev/null)"

section "Placeholder text in the scenes"
report "$(grep -rn 'goes here\|Lorem\|placeholder\|PLACEHOLDER\|coming soon' --include=*.unity --include=*.prefab Assets/ 2>/dev/null)"

section "Docs naming a file that is not there"
# CLAUDE.md and docs/ name files in backticks, usually by basename. A renamed or
# deleted script leaves the sentence behind, and nothing else ever notices.
# Computed into a variable first, not nested straight inside report "$(...)" - that
# double nesting of quoted command substitutions is what silently corrupts $t below.
# if/[[ ]], not case: macOS ships bash 3.2 (frozen since 2007, GPLv3), whose parser
# cannot reliably close a "case...esac" when it sits inside a $(...) command
# substitution - it misreads the pattern-closing ")" against the substitution's own
# closing ")" and dies on the first ";;" with "syntax error near unexpected token".
# Confirmed with a minimal repro: identical case/esac, works standalone, breaks the
# instant it's wrapped in $(...), on this bash and no other. if/[[ ]] has no such bug.
docs_missing=$(grep -oh '`[^`]*`' CLAUDE.md docs/*.md 2>/dev/null | tr -d '`' | sort -u | while IFS= read -r t; do
  if [[ "$t" == *' '* || "$t" == http* || "$t" == /* || "$t" == .[a-z]* ]]; then
    continue
  fi
  if [[ "$t" == */* ]]; then
    [ -e "$t" ] && continue
    git check-ignore -q "$t" 2>/dev/null && continue
    echo "MISSING PATH: $t"
  elif [[ "$t" == *.cs || "$t" == *.unity || "$t" == *.prefab || "$t" == *.md || "$t" == *.asset || "$t" == *.js || "$t" == *.anim || "$t" == *.controller ]]; then
    found=$( { find Assets docs .claude ProjectSettings -name "$t" 2>/dev/null; find . -maxdepth 1 -name "$t" 2>/dev/null; } | head -1)
    [ -n "$found" ] && continue
    echo "MISSING FILE: $t"
  fi
done)
report "$docs_missing"

section "Tracked .claude entries CLAUDE.md never mentions"
report "$(for d in .claude/skills/*/; do
  git ls-files --error-unmatch "$d" >/dev/null 2>&1 || continue
  n=$(basename "$d"); grep -q "$n" CLAUDE.md || echo "skill: $n"
done
for f in .claude/hooks/*; do
  git ls-files --error-unmatch "$f" >/dev/null 2>&1 || continue
  n=$(basename "$f"); grep -q "$n" CLAUDE.md || echo "hook: $n"
done)"

section "Largest tracked assets"
git ls-files -z Assets | xargs -0 du -k 2>/dev/null | sort -rn | head -5 | sed 's/^/   /'

printf '\n-- shell checks done. Now run the two blocks in unity-checks.md. --\n'
