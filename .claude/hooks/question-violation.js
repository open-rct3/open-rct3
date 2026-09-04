#!/usr/bin/env node
const fs = require('fs');

function readStdin() {
  try {
    let chunks = [];
    const buf = Buffer.alloc(4096);
    let bytesRead;
    while ((bytesRead = fs.readSync(0, buf, 0, buf.length)) > 0) {
      chunks.push(buf.slice(0, bytesRead));
    }
    return Buffer.concat(chunks).toString('utf-8');
  } catch {
    return '';
  }
}

const input = readStdin();
let transcriptPath = '';

if (input) {
  try {
    const parsed = JSON.parse(input);
    transcriptPath = parsed.transcriptPath || '';
  } catch {
    const match = input.match(/"transcriptPath"\s*:\s*"([^"]+)"/);
    if (match) transcriptPath = match[1];
  }
}

if (!transcriptPath) {
  transcriptPath = process.env.TRANSCRIPT_PATH || process.env.CLAUDE_TRANSCRIPT_PATH || '';
}

if (transcriptPath && fs.existsSync(transcriptPath)) {
  const content = fs.readFileSync(transcriptPath, 'utf-8');
  const lines = content.trim().split('\n').filter(Boolean);
  const recent = lines.slice(-25);

  let lastAssistantLine = '';
  for (let i = recent.length - 1; i >= 0; i--) {
    if (recent[i].includes('"PLANNER_RESPONSE"') || recent[i].includes('"assistant"')) {
      lastAssistantLine = recent[i];
      break;
    }
  }

  if (lastAssistantLine) {
    const hasToolCall = lastAssistantLine.includes('"ask_question"') || lastAssistantLine.includes('"ask"');
    if (!hasToolCall) {
      const questionPattern = /(\?|options?:|which (one|option|approach))/i;
      if (questionPattern.test(lastAssistantLine)) {
        process.stdout.write(JSON.stringify({
          decision: 'continue',
          reason: 'QUESTION TOOL VIOLATION: You asked a question or presented options in plain text without using the ask_question tool. Per AGENTS.md, invoke ask_question now.'
        }) + '\n');
        process.exit(1);
      }
    }
  }
}

process.stdout.write(JSON.stringify({ decision: 'allow' }) + '\n');
process.exit(0);
