#!/usr/bin/env node
'use strict';

/**
 * UserPromptSubmit hook - "senin sıran".
 *
 * Handing a piece of the work back to the user needs judgement, and a shell
 * script has none: it cannot tell a good handover task from a terrible one.
 * So this script does only the part a script is actually good at - it keeps
 * count, and it never forgets. The judgement lives in
 * .claude/skills/hand-it-over/SKILL.md.
 *
 * The clock is commits, not messages. The incremental-commits skill already
 * splits work into pieces that each land as one commit, so "every N commits"
 * reads as "every N units of finished work" rather than "every N times the
 * user typed something".
 *
 * UserPromptSubmit is the only event where a clean split exists: the task has
 * arrived and no work has been done yet. A PreToolUse hook would deny a call
 * halfway through authoring a scene and leave it half built.
 *
 * This never throws and always exits 0. A broken hook must not cost a prompt.
 */

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const STATE = path.join(__dirname, 'your-turn.state.json');
const SKILL = '.claude/skills/hand-it-over/SKILL.md';

const DEFAULTS = {
  enabled: true,
  everyNCommits: 2,   // how many committed pieces go by between handovers
  maxReminders: 3,    // an ignored ticket nags this often, then lapses
  lastTicketAtCommit: null,
  prompts: 0,
  openTicket: null,   // { openedAtPrompt, openedAtCommit, reminders }
  handovers: 0,
  lapsed: 0
};

function load() {
  try {
    return Object.assign({}, DEFAULTS, JSON.parse(fs.readFileSync(STATE, 'utf8')));
  } catch (e) {
    return Object.assign({}, DEFAULTS);
  }
}

function save(state) {
  try {
    fs.writeFileSync(STATE, JSON.stringify(state, null, 2) + '\n', 'utf8');
  } catch (e) { /* a read-only disk is not worth losing the prompt over */ }
}

/** Commits are the clock. No repo yet means no clock, and no tickets. */
function commitCount(cwd) {
  try {
    const out = execFileSync('git', ['rev-list', '--count', 'HEAD'], {
      cwd: cwd || process.cwd(),
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore']
    });
    const n = parseInt(out.trim(), 10);
    return isNaN(n) ? null : n;
  } catch (e) {
    return null;
  }
}

// A follow-up inside a running task is the worst possible place to hand
// something over - the work is already half done. Only a prompt that reads
// like fresh work gets a ticket.
const FOLLOW_UP = /^\s*(onay|tamam|evet|hay[ıi]r|yok|olur|peki|ok\b|oke|devam|commit|at\b|bekle|dur\b|geri al|iptal|te[şs]ekk[üu]r|sa[ğg]\s?ol|g[üu]zel|harika|oldu\b|olmu[şs])/i;
const FORCE = /#benim-s[ıi]ram/i;
const SKIP = /#kendin-yap/i;

function looksLikeNewWork(prompt) {
  const p = (prompt || '').trim();
  if (p.length < 25) return false;        // "peki", "şunu da ekle" - too thin to split
  if (p.charAt(0) === '/') return false;  // slash command, not a task
  if (FOLLOW_UP.test(p)) return false;
  return true;
}

function ticket(state, commits, forced) {
  const since = state.lastTicketAtCommit === null ? commits : commits - state.lastTicketAtCommit;
  return [
    forced
      ? '[SENİN SIRAN] Kullanıcı bileti elle istedi (#benim-sıram).'
      : '[SENİN SIRAN] Son devirden bu yana ' + since + ' commit geçti.',
    'Bu isteğe başlamadan önce içinden kullanıcının Unity\'de kendi eliyle',
    'yapabileceği bir parça ayır ve ona devret. Nasıl devredileceği ' + SKILL + ' içinde.',
    'Uygun bir parça yoksa tek cümleyle söyle ve normal devam et.',
    'Devir bitince: node .claude/hooks/your-turn.js --close',
    ''
  ].join('\n');
}

function reminder(state) {
  const age = state.prompts - state.openTicket.openedAtPrompt;
  const left = state.maxReminders - state.openTicket.reminders;
  return [
    '[SENİN SIRAN - hâlâ açık] Bilet ' + age + ' istek önce açıldı, kapanmadı.',
    'Ya devri şimdi yap (' + SKILL + '), ya da neden yapmadığını tek cümleyle söyle.',
    'Kapatmak için: node .claude/hooks/your-turn.js --close' +
      (left <= 0 ? '  (son hatırlatma, sonra bilet düşer)' : ''),
    ''
  ].join('\n');
}

function runHook(input) {
  const state = load();
  if (!state.enabled) return;

  const prompt = input.prompt || '';
  state.prompts += 1;
  const commits = commitCount(input.cwd);

  if (SKIP.test(prompt)) {
    state.openTicket = null;
    if (commits !== null) state.lastTicketAtCommit = commits;
    save(state);
    return;
  }

  // An open ticket outranks everything. An ignored handover is exactly the
  // failure this hook exists to catch, so it gets louder instead of lapsing
  // quietly - but only so many times, or it turns into noise.
  if (state.openTicket) {
    state.openTicket.reminders += 1;
    if (state.openTicket.reminders > state.maxReminders) {
      state.openTicket = null;
      state.lapsed += 1;
      if (commits !== null) state.lastTicketAtCommit = commits;
      save(state);
      return;
    }
    save(state);
    process.stdout.write(reminder(state));
    return;
  }

  const forced = FORCE.test(prompt);
  if (!forced) {
    if (commits === null) { save(state); return; }
    if (state.lastTicketAtCommit === null) {
      // First run: start the clock here rather than firing on commit #1.
      state.lastTicketAtCommit = commits;
      save(state);
      return;
    }
    if (commits - state.lastTicketAtCommit < state.everyNCommits) { save(state); return; }
    if (!looksLikeNewWork(prompt)) { save(state); return; }
  }

  const text = ticket(state, commits === null ? 0 : commits, forced);
  state.openTicket = { openedAtPrompt: state.prompts, openedAtCommit: commits, reminders: 0 };
  save(state);
  process.stdout.write(text);
}

function runCli(argv) {
  const state = load();
  const cmd = argv[0];

  if (cmd === '--close') {
    const commits = commitCount(process.cwd());
    const had = !!state.openTicket;
    state.openTicket = null;
    if (had) state.handovers += 1;
    if (commits !== null) state.lastTicketAtCommit = commits;
    save(state);
    console.log(had
      ? 'Devir kapatıldı. Sıradaki bilet ' + state.everyNCommits + ' commit sonra.'
      : 'Açık bilet yoktu; sayaç yine de sıfırlandı.');
    return;
  }

  if (cmd === '--every') {
    const n = parseInt(argv[1], 10);
    if (!(n > 0)) { console.log('Kullanım: --every <commit sayısı>'); return; }
    state.everyNCommits = n;
    save(state);
    console.log('Her ' + n + ' committe bir devir.');
    return;
  }

  if (cmd === '--off' || cmd === '--on') {
    state.enabled = cmd === '--on';
    if (!state.enabled) state.openTicket = null;
    save(state);
    console.log(state.enabled ? 'Açık.' : 'Kapalı.');
    return;
  }

  const commits = commitCount(process.cwd());
  console.log([
    'senin sıran  ' + (state.enabled ? 'açık' : 'kapalı'),
    '  sıklık     her ' + state.everyNCommits + ' commit',
    '  commit     ' + (commits === null ? '?' : commits) +
      ' (son devir: ' + (state.lastTicketAtCommit === null ? '-' : state.lastTicketAtCommit) + ')',
    '  bilet      ' + (state.openTicket
      ? 'AÇIK, ' + state.openTicket.reminders + ' hatırlatma'
      : 'yok'),
    '  devredilen ' + state.handovers + ' (düşen: ' + state.lapsed + ')',
    '  görülen    ' + state.prompts + ' istem  <- artmıyorsa hook bağlı değil',
    '',
    'komutlar: --status | --close | --every <n> | --off | --on',
    'istem içinde: #benim-sıram (hemen bilet)  #kendin-yap (bu seferlik atla)'
  ].join('\n'));
}

function main() {
  // Claude Code pipes the event JSON in. A person typing the name at a prompt
  // has a terminal on stdin and wants the status line instead.
  if (process.argv.length > 2) { runCli(process.argv.slice(2)); return; }
  if (process.stdin.isTTY) { runCli(['--status']); return; }

  let raw = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', function (chunk) { raw += chunk; });
  process.stdin.on('end', function () {
    try { runHook(JSON.parse(raw || '{}')); } catch (e) { /* stay out of the way */ }
  });
}

try { main(); } catch (e) { /* stay out of the way */ }
