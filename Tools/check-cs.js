#!/usr/bin/env node
/*
 * Lightweight static sanity check for the C# sources (no compiler available on the dev machine):
 *  - brace / parenthesis balance per file
 *  - every `using VortexKarts.X;` refers to a namespace that exists
 *  - every `VortexKarts.X` type referenced through common patterns exists somewhere
 *  - reports public method/property names used as `Something.Member(` where the class is ours and
 *    the member is not declared anywhere in that class file (heuristic)
 */
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const files = [];
(function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const f = path.join(d, e.name);
    if (e.isDirectory()) walk(f);
    else if (e.name.endsWith('.cs')) files.push(f);
  }
})(path.join(root, 'Assets'));

let problems = 0;
const namespaces = new Set();
const classes = new Map(); // className -> { file, members:Set }
const sources = new Map();

for (const f of files) {
  const src = fs.readFileSync(f, 'utf8');
  sources.set(f, src);
  // Strip strings and comments for structural checks.
  const stripped = src
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '')
    .replace(/"(?:\\.|[^"\\])*"/g, '""')
    .replace(/'(?:\\.|[^'\\])'/g, "''");
  const counts = { '{': 0, '}': 0, '(': 0, ')': 0, '[': 0, ']': 0 };
  for (const ch of stripped) if (ch in counts) counts[ch]++;
  if (counts['{'] !== counts['}']) { console.log(`BRACES  ${path.relative(root, f)} { ${counts['{']} vs } ${counts['}']}`); problems++; }
  if (counts['('] !== counts[')']) { console.log(`PARENS  ${path.relative(root, f)} ( ${counts['(']} vs ) ${counts[')']}`); problems++; }
  if (counts['['] !== counts[']']) { console.log(`BRACKETS ${path.relative(root, f)}`); problems++; }

  for (const m of stripped.matchAll(/namespace\s+([\w.]+)/g)) namespaces.add(m[1]);
  for (const m of stripped.matchAll(/(?:class|struct|interface|enum)\s+(\w+)/g)) {
    if (!classes.has(m[1])) classes.set(m[1], { file: f, members: new Set() });
  }
}

// Collect declared members per file (heuristic: identifiers followed by ( or { or ; or => at declaration level).
for (const [name, info] of classes) {
  const src = sources.get(info.file);
  for (const m of src.matchAll(/(?:public|internal|protected|private|static)\s+[\w<>\[\],\s?.]+?\s+(\w+)\s*(?:\(|\{|=>|;|=)/g)) info.members.add(m[1]);
  for (const m of src.matchAll(/\b(\w+)\s*(?:\{\s*get|=>)/g)) info.members.add(m[1]);
  for (const m of src.matchAll(/event\s+[\w<>,\s]+\s+(\w+)\s*;/g)) info.members.add(m[1]);
  for (const m of src.matchAll(/(?:const|readonly)\s+\w+\s+(\w+)\s*=/g)) info.members.add(m[1]);
  // enum values
  const enumBlocks = src.matchAll(new RegExp(`enum\\s+${name}\\s*\\{([^}]*)\\}`, 'g'));
  for (const b of enumBlocks) for (const v of b[1].split(',')) { const id = v.trim().split(/[\s=]/)[0]; if (id) info.members.add(id); }
}

for (const f of files) {
  const src = sources.get(f);
  for (const m of src.matchAll(/using\s+(VortexKarts[\w.]*)\s*;/g)) {
    if (!namespaces.has(m[1])) { console.log(`USING   ${path.relative(root, f)} -> missing namespace ${m[1]}`); problems++; }
  }
  // Static/instance access on our own classes: ClassName.Member
  for (const m of src.matchAll(/\b([A-Z]\w+)\.([A-Z]\w+)\b/g)) {
    const cls = m[1], member = m[2];
    if (!classes.has(cls)) continue;
    if (['Instance', 'Create', 'EnsureExists'].includes(member) && classes.get(cls).members.has(member)) continue;
    const info = classes.get(cls);
    if (!info.members.has(member)) {
      // Could be a nested type or enum value; check any class named member exists.
      if (classes.has(member)) continue;
      console.log(`MEMBER? ${path.relative(root, f)} uses ${cls}.${member} (not found in ${path.basename(info.file)})`);
      problems++;
    }
  }
}

console.log(`\nFiles: ${files.length}, namespaces: ${namespaces.size}, types: ${classes.size}, issues flagged: ${problems}`);
