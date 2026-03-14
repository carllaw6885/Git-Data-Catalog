using System.Text.Json;
using GitCatalog.Core;

namespace GitCatalog.Generation;

public static class StaticSiteGenerator
{
    public static IReadOnlyList<GeneratedDocument> GenerateSiteAssets(IEnumerable<TableDefinition> tables)
    {
        var tableIds = tables
            .Select(t => t.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var manifest = JsonSerializer.Serialize(
            new SiteManifest("../generated", "../generated/er.mmd", tableIds),
          new JsonSerializerOptions
          {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
          });

        return
        [
            new GeneratedDocument("index.html", IndexHtml),
            new GeneratedDocument("app.css", AppCss),
            new GeneratedDocument("app.js", AppJs),
            new GeneratedDocument("manifest.json", manifest)
        ];
    }

    private const string IndexHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>GitCatalog Site</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Manrope:wght@400;600;700;800&family=IBM+Plex+Mono:wght@400;500&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="app.css" />
  <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
  <script type="module">
    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
    window.__gitcatalog_mermaid = mermaid;
  </script>
</head>
<body>
  <div class="bg-glow bg-glow-a"></div>
  <div class="bg-glow bg-glow-b"></div>
  <header class="topbar">
    <div class="brand">GitCatalog</div>
    <div class="subtitle">Interactive Architecture Site</div>
  </header>

  <main class="layout">
    <aside class="sidebar">
      <label class="label" for="table-search">Find table</label>
      <input id="table-search" type="search" placeholder="sales.order" />

      <button id="show-overview" class="nav-btn nav-btn-primary" type="button">Overview</button>
      <button id="show-er" class="nav-btn" type="button">ER Diagram</button>

      <div class="tables-header">Tables</div>
      <nav id="tables-nav" class="tables-nav"></nav>
    </aside>

    <section class="content-wrap">
      <article id="content" class="content"></article>
    </section>
  </main>

  <script src="app.js"></script>
</body>
</html>
""";

    private const string AppCss = """
:root {
  --bg: #0f1423;
  --bg-soft: #1a2238;
  --panel: rgba(20, 28, 48, 0.82);
  --panel-border: rgba(148, 163, 184, 0.28);
  --text: #f8fafc;
  --text-dim: #cbd5e1;
  --accent: #34d399;
  --accent-2: #38bdf8;
}

* { box-sizing: border-box; }

body {
  margin: 0;
  font-family: 'Manrope', sans-serif;
  color: var(--text);
  background: radial-gradient(circle at 15% 20%, #172554 0%, transparent 40%),
              radial-gradient(circle at 85% 15%, #14532d 0%, transparent 35%),
              linear-gradient(160deg, #0b1020 0%, #0f1423 45%, #111827 100%);
  min-height: 100vh;
}

.bg-glow {
  position: fixed;
  filter: blur(90px);
  opacity: 0.36;
  z-index: -1;
}

.bg-glow-a {
  width: 280px;
  height: 280px;
  left: -80px;
  top: 220px;
  background: #22d3ee;
}

.bg-glow-b {
  width: 260px;
  height: 260px;
  right: -70px;
  top: 70px;
  background: #4ade80;
}

.topbar {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  padding: 20px 26px;
  border-bottom: 1px solid rgba(148, 163, 184, 0.18);
}

.brand {
  font-size: 1.4rem;
  font-weight: 800;
  letter-spacing: 0.02em;
}

.subtitle {
  color: var(--text-dim);
  font-size: 0.95rem;
}

.layout {
  display: grid;
  grid-template-columns: minmax(240px, 320px) 1fr;
  gap: 16px;
  padding: 18px;
}

.sidebar,
.content-wrap {
  background: var(--panel);
  border: 1px solid var(--panel-border);
  border-radius: 16px;
  backdrop-filter: blur(6px);
}

.sidebar {
  padding: 14px;
}

.label,
.tables-header {
  color: var(--text-dim);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.72rem;
}

#table-search {
  width: 100%;
  margin: 8px 0 10px;
  padding: 10px;
  border-radius: 10px;
  border: 1px solid rgba(148, 163, 184, 0.34);
  background: rgba(15, 23, 42, 0.75);
  color: var(--text);
}

.nav-btn {
  width: 100%;
  margin-bottom: 8px;
  border: 1px solid rgba(148, 163, 184, 0.35);
  background: rgba(30, 41, 59, 0.75);
  color: var(--text);
  border-radius: 10px;
  padding: 9px 11px;
  text-align: left;
  cursor: pointer;
  transition: transform 180ms ease, border-color 180ms ease;
}

.nav-btn:hover {
  transform: translateY(-1px);
  border-color: var(--accent-2);
}

.nav-btn-primary {
  border-color: rgba(52, 211, 153, 0.75);
}

.tables-header {
  margin: 14px 0 8px;
}

.tables-nav {
  display: grid;
  gap: 6px;
  max-height: calc(100vh - 290px);
  overflow: auto;
}

.table-link {
  display: block;
  text-decoration: none;
  color: var(--text);
  font-size: 0.92rem;
  padding: 8px;
  border-radius: 8px;
  border: 1px solid transparent;
}

.table-link:hover,
.table-link.active {
  border-color: rgba(56, 189, 248, 0.72);
  background: rgba(15, 23, 42, 0.78);
}

.content-wrap {
  padding: 22px;
  min-height: 70vh;
}

.content {
  line-height: 1.55;
  animation: fadeIn 240ms ease;
}

.content code,
.content pre {
  font-family: 'IBM Plex Mono', monospace;
}

.content table {
  width: 100%;
  border-collapse: collapse;
}

.content th,
.content td {
  border: 1px solid rgba(148, 163, 184, 0.35);
  padding: 8px;
}

.content th {
  background: rgba(15, 23, 42, 0.9);
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .tables-nav {
    max-height: 220px;
  }

  .topbar {
    flex-direction: column;
    gap: 4px;
  }
}
""";

    private const string AppJs = """
const state = {
  manifest: null,
  allTables: [],
  activeTable: null
};

const contentEl = document.getElementById('content');
const tablesNavEl = document.getElementById('tables-nav');
const searchEl = document.getElementById('table-search');
const overviewBtn = document.getElementById('show-overview');
const erBtn = document.getElementById('show-er');

bootstrap().catch((err) => {
  contentEl.innerHTML = `<h1>Failed to load site</h1><p>${escapeHtml(err.message)}</p>`;
});

async function bootstrap() {
  const res = await fetch('manifest.json');
  if (!res.ok) {
    throw new Error(`Unable to load manifest.json (${res.status})`);
  }

  state.manifest = await res.json();
  state.allTables = state.manifest.tables ?? [];

  renderTableNav(state.allTables);
  wireEvents();
  await showOverview();
}

function wireEvents() {
  overviewBtn.addEventListener('click', () => showOverview());
  erBtn.addEventListener('click', () => showEr());
  searchEl.addEventListener('input', () => {
    const term = searchEl.value.trim().toLowerCase();
    const filtered = state.allTables.filter(t => t.toLowerCase().includes(term));
    renderTableNav(filtered);
  });
}

function renderTableNav(tableIds) {
  tablesNavEl.innerHTML = '';

  for (const tableId of tableIds) {
    const link = document.createElement('a');
    link.href = '#';
    link.className = `table-link ${tableId === state.activeTable ? 'active' : ''}`;
    link.textContent = tableId;
    link.addEventListener('click', async (ev) => {
      ev.preventDefault();
      await showTable(tableId);
    });
    tablesNavEl.appendChild(link);
  }
}

async function showOverview() {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());
  const markdown = await loadText(`${state.manifest.generatedRoot}/index.md`);
  contentEl.innerHTML = marked.parse(markdown);
}

async function showTable(tableId) {
  state.activeTable = tableId;
  renderTableNav(getCurrentVisibleTableIds());
  const markdown = await loadText(`${state.manifest.generatedRoot}/tables/${tableId}.md`);
  contentEl.innerHTML = marked.parse(markdown);
}

async function showEr() {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());
  const mermaidDef = await loadText(state.manifest.erPath);
  const mermaid = window.__gitcatalog_mermaid;

  mermaid.initialize({
    startOnLoad: false,
    theme: 'base',
    themeVariables: {
      primaryColor: '#1d4ed8',
      primaryTextColor: '#0f172a',
      lineColor: '#0ea5e9',
      fontFamily: 'Manrope'
    }
  });

  const id = `er_${Date.now()}`;
  const rendered = await mermaid.render(id, mermaidDef);
  contentEl.innerHTML = `<h1>Entity Relationship Diagram</h1>${rendered.svg}`;
}

function getCurrentVisibleTableIds() {
  const term = searchEl.value.trim().toLowerCase();
  if (!term) {
    return state.allTables;
  }

  return state.allTables.filter(t => t.toLowerCase().includes(term));
}

async function loadText(path) {
  const res = await fetch(path);
  if (!res.ok) {
    throw new Error(`Unable to load ${path} (${res.status})`);
  }

  return res.text();
}

function escapeHtml(input) {
  return input
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
""";
}

public sealed record SiteManifest(string GeneratedRoot, string ErPath, IReadOnlyList<string> Tables);