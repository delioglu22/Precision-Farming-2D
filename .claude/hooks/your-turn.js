#!/usr/bin/env node
'use strict';

/**
 * UserPromptSubmit hook - "your turn".
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
const FOLLOW_UP = /^\s*(ok(ay)?\b|yes\b|no\b|yeah\b|yep\b|sure\b|fine\b|go ahead\b|do it\b|commit\b|push\b|continue\b|carry on\b|wait\b|stop\b|undo\b|cancel\b|thanks\b|thank you\b|great\b|nice\b|good\b|done\b|perfect\b|right\b|leave it\b)/i;
const FORCE = /#my-turn/i;
const SKIP = /#you-do-it/i;

function looksLikeNewWork(prompt) {
  const p = (prompt || '').trim();
  if (p.length < 25) return false;        // "ok", "add that too" - too thin to split
  if (p.charAt(0) === '/') return false;  // slash command, not a task
  if (FOLLOW_UP.test(p)) return false;
  return true;
}

function ticket(state, commits, forced) {
  const since = state.lastTicketAtCommit === null ? commits : commits - state.lastTicketAtCommit;
  return [
    forced
      ? '[YOUR TURN] The user asked for a ticket by hand (#my-turn).'
      : '[YOUR TURN] ' + since + ' commits have gone by since the last handover.',
    'Before starting this request, pick one piece of it the user can do by hand',
    'in Unity and hand that piece over. How to do it is in ' + SKILL + '.',
    'If there is no suitable piece, say so in one sentence and carry on as normal.',
    'When the handover is done: node .claude/hooks/your-turn.js --close',
    ''
  ].join('\n');
}

function reminder(state) {
  const age = state.prompts - state.openTicket.openedAtPrompt;
  const left = state.maxReminders - state.openTicket.reminders;
  return [
    '[YOUR TURN - still open] Ticket opened ' + age + ' requests ago, never closed.',
    'Either hand something over now (' + SKILL + '), or say in one sentence why not.',
    'To close it: node .claude/hooks/your-turn.js --close' +
      (left <= 0 ? '  (last reminder, then the ticket lapses)' : ''),
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
      ? 'Handover closed. Next ticket in ' + state.everyNCommits + ' commits.'
      : 'No ticket was open; the counter was reset anyway.');
    return;
  }

  if (cmd === '--every') {
    const n = parseInt(argv[1], 10);
    if (!(n > 0)) { console.log('Usage: --every <number of commits>'); return; }
    state.everyNCommits = n;
    save(state);
    console.log('One handover every ' + n + ' commits.');
    return;
  }

  if (cmd === '--off' || cmd === '--on') {
    state.enabled = cmd === '--on';
    if (!state.enabled) state.openTicket = null;
    save(state);
    console.log(state.enabled ? 'On.' : 'Off.');
    return;
  }

  const commits = commitCount(process.cwd());
  console.log([
    'your turn   ' + (state.enabled ? 'on' : 'off'),
    '  every      ' + state.everyNCommits + ' commits',
    '  commits    ' + (commits === null ? '?' : commits) +
      ' (last handover: ' + (state.lastTicketAtCommit === null ? '-' : state.lastTicketAtCommit) + ')',
    '  ticket     ' + (state.openTicket
      ? 'OPEN, ' + state.openTicket.reminders + ' reminder(s)'
      : 'none'),
    '  handovers  ' + state.handovers + ' (lapsed: ' + state.lapsed + ')',
    '  seen       ' + state.prompts + ' prompts  <- if this stops rising the hook is not wired',
    '',
    'commands: --status | --close | --every <n> | --off | --on',
    'in a prompt: #my-turn (ticket now)  #you-do-it (skip this one)'
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
