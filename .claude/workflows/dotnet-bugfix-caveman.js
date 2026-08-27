export const meta = {
  name: 'dotnet-bugfix-caveman',
  description: '.NET bugfix helper: investigate via wiki+ast-index, apply confirmed fix, verify build, document insights in wiki',
  whenToUse: 'Bug report (current vs expected behavior) on this .NET codebase. Stages called separately, each only after the matching human gate: investigate -> (human confirms plan) -> fix -> verify -> (human confirms fix works) -> document.',
  phases: [
    { title: 'Investigate' },
    { title: 'Fix' },
    { title: 'Verify' },
    { title: 'Document' },
  ],
}

// args shape per stage:
//   investigate: { stage:'investigate', currentBehavior, expectedBehavior, hints? }
//   fix:         { stage:'fix', plan, files }              // files: string[] (paths), max ~2, plan = confirmed fix plan text
//   verify:      { stage:'verify', projectPaths? }         // projectPaths: string[] of .csproj/.sln to build, default: whole backend
//   document:    { stage:'document', summary, insight, filesChanged, wikiHints? }

if (!args || !args.stage) {
  throw new Error('args.stage required: investigate | fix | verify | document')
}

const CAVEMAN = 'Use caveman mode (full): drop articles, fragments OK, short synonyms, no decorative tables/emoji.'

if (args.stage === 'investigate') {
  phase('Investigate')

  const currentBehavior = args.currentBehavior || ''
  const expectedBehavior = args.expectedBehavior || ''
  const hints = args.hints || ''

  const results = await parallel([
    () => agent(
      `${CAVEMAN}
Use wiki-read to search project info. Entry point /docs/wiki/index.md.
Bug report:
Current behavior: ${currentBehavior}
Expected behavior: ${expectedBehavior}
${hints ? `Extra hints: ${hints}` : ''}
Task: find wiki pages, sections, described algorithms/flows relevant to this bug. Report wiki file paths + section names + short excerpt of relevant algorithm/flow description. Factual only, no speculation.`,
      { label: 'wiki-search', agentType: 'caveman:cavecrew-investigator' }
    ),
    () => agent(
      `${CAVEMAN}
Use ast-index for code search: ast-index search <query>, ast-index file <pattern>, ast-index symbol, ast-index callers. Run via Bash.
Project is .NET (backend/nsitransfer/*.csproj).
Bug report:
Current behavior: ${currentBehavior}
Expected behavior: ${expectedBehavior}
${hints ? `Extra hints: ${hints}` : ''}
Task: locate code responsible for current (wrong) behavior and code path that should implement expected behavior. Find relevant functions, call sites (callers), related state/cache/scope relevant for a correct fix.
Report exact file:line locations, short snippets only where necessary. Factual only, no fix suggestions.`,
      { label: 'ast-search', agentType: 'caveman:cavecrew-investigator' }
    ),
  ])

  return { wiki: results[0], ast: results[1] }
}

if (args.stage === 'fix') {
  phase('Fix')

  const plan = args.plan || ''
  const files = args.files || []

  if (files.length === 0) {
    throw new Error('args.files required (paths this fix touches)')
  }

  const result = await agent(
    `${CAVEMAN}
Confirmed fix plan (user already approved this — apply exactly, no scope creep):
${plan}

Files in scope:
${files.map(f => `- ${f}`).join('\n')}

Apply the plan. Keep style consistent with surrounding code, no comments unless explaining non-obvious why. Report exact edits made (file:line, before/after summary).`,
    { label: 'apply-fix', agentType: 'caveman:cavecrew-builder' }
  )

  return { fix: result }
}

if (args.stage === 'verify') {
  phase('Verify')

  const projectPaths = args.projectPaths && args.projectPaths.length > 0
    ? args.projectPaths
    : ['backend']

  const result = await agent(
    `${CAVEMAN}
Run: cd ${projectPaths[0]} && dotnet build (repeat per path if multiple: ${JSON.stringify(projectPaths)}).
Report: pass or fail. If fail, quote only the first error line(s) verbatim (not full warning noise). If pass, just say build ok, N warnings (preexisting, no need to list).`,
    { label: 'build-verify' }
  )

  return { verify: result }
}

if (args.stage === 'document') {
  phase('Document')

  const summary = args.summary || ''
  const insight = args.insight || ''
  const filesChanged = args.filesChanged || []
  const wikiHints = args.wikiHints || ''

  const filesList = filesChanged
    .map(f => `- ${f.path}${f.lines ? `:${f.lines}` : ''} — ${f.whatChanged || ''}`)
    .join('\n')

  const result = await agent(
    `${CAVEMAN}
Use wiki-read to locate wiki page(s) already covering this area. Entry point /docs/wiki/index.md.
${wikiHints ? `Hints on likely relevant pages: ${wikiHints}` : ''}

Bug fix just confirmed working by user. Update wiki with key result + insight for future reuse.

Bug/fix summary: ${summary}
Key insight (non-obvious root cause / algorithm change): ${insight}
Files changed:
${filesList}

Task:
1. Find most relevant existing wiki page(s) (via wiki-read).
2. If found — edit page(s): add/extend a "Баги и исправления" (or matching existing convention) section, short, factual, matching page style. Include file:line refs.
3. If no relevant page exists — create minimal new page under docs/wiki, link from /docs/wiki/index.md.
4. Update index.md "last modified" note if that convention exists.
Report which wiki files touched and what section/content added. Short, factual.`,
    { label: 'wiki-update' }
  )

  return { wikiUpdate: result }
}

throw new Error(`unknown args.stage: ${args.stage}`)
