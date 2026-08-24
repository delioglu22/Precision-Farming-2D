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
report "$(grep -oh '`[^`]*`' CLAUDE.md docs/*.md 2>/dev/null | tr -d '`' | sort -u | while IFS= read -r t; do
  case "$t" in *' '*|http*|/*|.[a-z]*) continue ;; esac
  case "$t" in
    */*)
      [ -e "$t" ] && continue
      git check-ignore -q "$t" 2>/dev/null && continue
      echo "MISSING PATH: $t" ;;
    *.cs|*.unity|*.prefab|*.md|*.asset|*.js|*.anim|*.controller)
      [ -n "$({ find Assets docs .claude ProjectSettings -name "$t" 2>/dev/null; find . -maxdepth 1 -name "$t" 2>/dev/null; } | head -1)" ] && continue
      echo "MISSING FILE: $t" ;;
  esac
done)"

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
