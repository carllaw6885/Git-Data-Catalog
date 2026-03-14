using System.Text.Json;
using GitCatalog.Core;

namespace GitCatalog.Generation;

public static class StaticSiteGenerator
{
    public static IReadOnlyList<GeneratedDocument> GenerateSiteAssets(
        IEnumerable<TableDefinition> tables,
        CatalogGraph graph)
    {
        var tableIds = tables
            .Select(t => t.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var viewpointIds = graph.Viewpoints
            .Select(v => v.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entities = graph.Entities
            .Where(e => e.Type is not (CatalogEntityType.Table or CatalogEntityType.Column))
            .OrderBy(e => e.Type.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Select(e => new SiteEntitySummary(e.Id, e.Name, e.Type.ToString(), e.Domain, e.Description, e.Owner.Team))
            .ToArray();

    var lineagePaths = graph.Viewpoints
      .Where(v => v.Id.Contains("lineage", StringComparison.OrdinalIgnoreCase)
           || v.Name.Contains("lineage", StringComparison.OrdinalIgnoreCase))
      .OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
      .Select(v => $"../generated/lineage/{v.Id}.mmd")
      .ToArray();

    var manifest = JsonSerializer.Serialize(
      new SiteManifest(
        GeneratedRoot: "../generated",
        ErPath: "../generated/er.mmd",
        Tables: tableIds,
        ViewpointIds: viewpointIds,
        LineagePaths: lineagePaths,
        DomainDepsPath: "../generated/domain/domain-dependencies.mmd",
        C4ContextPath: "../generated/c4/context.mmd",
        C4ContainerPath: "../generated/c4/container.mmd",
        C4ComponentPath: "../generated/c4/component.mmd",
        Entities: entities),
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
      <label class="label" for="table-search">Search</label>
      <input id="table-search" type="search" placeholder="tables, systems, pipelines…" />

      <label class="label" for="type-filter" style="margin-top:8px">Filter by type</label>
      <select id="type-filter">
        <option value="">All types</option>
        <option value="table">Table</option>
        <option value="system">System</option>
        <option value="pipeline">Pipeline</option>
        <option value="dataset">Dataset</option>
        <option value="domain">Domain</option>
        <option value="consumer">Consumer</option>
        <option value="actor">Actor</option>
        <option value="component">Component</option>
        <option value="container">Container</option>
      </select>

      <button id="show-overview" class="nav-btn nav-btn-primary" type="button">Overview</button>
      <button id="show-er" class="nav-btn" type="button">ER Diagram</button>
  <button id="show-architecture" class="nav-btn" type="button">Architecture Explorer</button>

      <div class="section-header">Lineage &amp; Domain</div>
      <button id="show-lineage" class="nav-btn" type="button">Data Lineage</button>
      <button id="show-domain-deps" class="nav-btn" type="button">Domain Dependencies</button>

      <div class="section-header">C4 Architecture</div>
      <button id="show-c4-context" class="nav-btn" type="button">C4 Context</button>
      <button id="show-c4-container" class="nav-btn" type="button">C4 Container</button>
      <button id="show-c4-component" class="nav-btn" type="button">C4 Component</button>

      <div id="viewpoints-section" style="display:none">
        <div class="section-header">Viewpoints</div>
        <nav id="viewpoints-nav" class="tables-nav"></nav>
      </div>

      <div class="section-header">Tables</div>
      <nav id="tables-nav" class="tables-nav"></nav>

      <div id="entities-section" style="display:none">
        <div class="section-header">Entities</div>
        <nav id="entities-nav" class="tables-nav"></nav>
      </div>
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

  #type-filter {
    width: 100%;
    margin: 4px 0 10px;
    padding: 8px;
    border-radius: 10px;
    border: 1px solid rgba(148, 163, 184, 0.34);
    background: rgba(15, 23, 42, 0.75);
    color: var(--text);
    font-family: inherit;
    font-size: 0.9rem;
  }
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

.section-header {
  color: var(--text-dim);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 0.72rem;
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

.loading {
  color: var(--text-dim);
  font-style: italic;
  padding: 32px 0;
}

.diagram-error {
  color: #f87171;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 8px;
  padding: 12px;
}

.badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 0.78rem;
  background: rgba(56, 189, 248, 0.15);
  border: 1px solid rgba(56, 189, 248, 0.45);
  color: var(--accent-2);

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px;
  margin: 12px 0;
}

.detail-card {
  border: 1px solid rgba(148, 163, 184, 0.35);
  border-radius: 10px;
  padding: 10px;
  background: rgba(15, 23, 42, 0.58);
}

.detail-card strong {
  display: block;
  color: var(--text-dim);
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  margin-bottom: 4px;
}
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
  allEntities: [],
  activeTable: null
};

const contentEl = document.getElementById('content');
const tablesNavEl = document.getElementById('tables-nav');
const entitiesNavEl = document.getElementById('entities-nav');
const entitiesSectionEl = document.getElementById('entities-section');
const viewpointsNavEl = document.getElementById('viewpoints-nav');
const viewpointsSectionEl = document.getElementById('viewpoints-section');
const searchEl = document.getElementById('table-search');
const typeFilterEl = document.getElementById('type-filter');
const overviewBtn = document.getElementById('show-overview');
const erBtn = document.getElementById('show-er');
const architectureBtn = document.getElementById('show-architecture');
const lineageBtn = document.getElementById('show-lineage');
const domainDepsBtn = document.getElementById('show-domain-deps');
const c4ContextBtn = document.getElementById('show-c4-context');
const c4ContainerBtn = document.getElementById('show-c4-container');
const c4ComponentBtn = document.getElementById('show-c4-component');

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
  state.allEntities = state.manifest.entities ?? [];

  renderTableNav(state.allTables);
  renderEntitiesNav(state.allEntities);
  renderViewpointsNav(state.manifest.viewpointIds ?? []);
  wireEvents();
  await showOverview();
}

function wireEvents() {
  overviewBtn.addEventListener('click', () => showOverview());
  erBtn.addEventListener('click', () => showMermaidDiagram(state.manifest.erPath, 'Entity Relationship Diagram'));
    architectureBtn.addEventListener('click', () => showArchitectureExplorer());
  lineageBtn.addEventListener('click', () => {
    const paths = state.manifest.lineagePaths ?? [];
    if (paths.length === 1) {
      showMermaidDiagram(paths[0], 'Data Lineage');
    } else if (paths.length > 1) {
      showDiagramList('Data Lineage', paths);
    } else {
      contentEl.innerHTML = '<h1>Data Lineage</h1><p class="loading">No lineage diagrams found. Define viewpoints with "lineage" in the id or name.</p>';
    }
  });
  domainDepsBtn.addEventListener('click', () => showMermaidDiagram(state.manifest.domainDepsPath, 'Domain Dependencies'));
  c4ContextBtn.addEventListener('click', () => showMermaidDiagram(state.manifest.c4ContextPath, 'C4 Context'));
  c4ContainerBtn.addEventListener('click', () => showMermaidDiagram(state.manifest.c4ContainerPath, 'C4 Container'));
  c4ComponentBtn.addEventListener('click', () => showMermaidDiagram(state.manifest.c4ComponentPath, 'C4 Component'));
  typeFilterEl?.addEventListener('change', () => applyFilters());
  searchEl.addEventListener('input', () => applyFilters());
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

function renderEntitiesNav(entities) {
  if (!entitiesNavEl) return;
  entitiesNavEl.innerHTML = '';

  for (const entity of entities) {
    const link = document.createElement('a');
    link.href = '#';
    link.className = 'table-link';
    link.textContent = `${entity.name}`;
    link.title = entity.type;
    link.addEventListener('click', (ev) => {
      ev.preventDefault();
      showEntity(entity);
    });
    entitiesNavEl.appendChild(link);
  }

  if (entitiesSectionEl) {
    entitiesSectionEl.style.display = entities.length > 0 ? '' : 'none';
  }
}

function renderViewpointsNav(viewpointIds) {
  if (!viewpointsNavEl) return;
  viewpointsNavEl.innerHTML = '';

  for (const vpId of viewpointIds) {
    const btn = document.createElement('button');
    btn.className = 'nav-btn';
    btn.type = 'button';
    btn.textContent = vpId;
    btn.addEventListener('click', () => {
      showMermaidDiagram(`${state.manifest.generatedRoot}/viewpoints/${vpId}.mmd`, vpId);
    });
    viewpointsNavEl.appendChild(btn);
  }

  if (viewpointsSectionEl) {
    viewpointsSectionEl.style.display = viewpointIds.length > 0 ? '' : 'none';
  }
}

async function showOverview() {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());

  try {
    const markdown = await loadText(`${state.manifest.generatedRoot}/index.md`);
    contentEl.innerHTML = marked.parse(markdown);
  } catch {
    contentEl.innerHTML = `<h1>GitCatalog</h1><p>Use <code>generate-all</code> to regenerate all documentation and diagrams.</p>`;
  }
}

async function showTable(tableId) {
  state.activeTable = tableId;
  renderTableNav(getCurrentVisibleTableIds());

  try {
    const markdown = await loadText(`${state.manifest.generatedRoot}/tables/${tableId}.md`);
    contentEl.innerHTML = marked.parse(markdown);
  } catch (err) {
    contentEl.innerHTML = `<h1>${escapeHtml(tableId)}</h1><p>Documentation not found: ${escapeHtml(err.message)}</p>`;
  }
}

async function showMermaidDiagram(path, title) {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());

  contentEl.innerHTML = `<h1>${escapeHtml(title)}</h1><p class="loading">Loading diagram\u2026</p>`;

  try {
    const mermaidDef = await loadText(path);
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

    const id = `diagram_${Date.now()}`;
    const rendered = await mermaid.render(id, mermaidDef);
    contentEl.innerHTML = `<h1>${escapeHtml(title)}</h1>${rendered.svg}`;
  } catch (err) {
    contentEl.innerHTML = `<h1>${escapeHtml(title)}</h1><p class="diagram-error">Unable to render diagram: ${escapeHtml(err.message)}</p>`;
  }
}

function showEntity(entity) {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());

  const domain = entity.domain ? `<span class="badge">${escapeHtml(entity.domain)}</span>` : '<span class="badge">none</span>';
  const description = entity.description ? `<p>${escapeHtml(entity.description)}</p>` : '<p class="loading">No description metadata provided.</p>';

  contentEl.innerHTML = `
    <h1>${escapeHtml(entity.name)}</h1>
    ${description}
    <div class="detail-grid">
      <div class="detail-card"><strong>Type</strong><span class="badge">${escapeHtml(entity.type)}</span></div>
      <div class="detail-card"><strong>ID</strong><code>${escapeHtml(entity.id)}</code></div>
      <div class="detail-card"><strong>Domain</strong>${domain}</div>
      <div class="detail-card"><strong>Owner</strong>${entity.ownerTeam ? escapeHtml(entity.ownerTeam) : '<span class="badge">unassigned</span>'}</div>
    </div>
  `;
}

function showArchitectureExplorer() {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());

  const groups = new Map();
  for (const entity of state.allEntities) {
    const type = entity.type || 'Unknown';
    if (!groups.has(type)) {
      groups.set(type, []);
    }
    groups.get(type).push(entity);
  }

  const sections = [...groups.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([type, entities]) => {
      const rows = entities
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(e => `<li><a href="#" class="table-link" onclick="event.preventDefault(); showEntityById('${escapeHtml(e.id)}')">${escapeHtml(e.name)}</a></li>`)
        .join('');
      return `<section><h2>${escapeHtml(type)} <span class="badge">${entities.length}</span></h2><ul style="list-style:none;padding:0">${rows}</ul></section>`;
    })
    .join('');

  contentEl.innerHTML = `<h1>Architecture Explorer</h1>${sections || '<p class="loading">No architecture entities found.</p>'}`;
}

function showEntityById(entityId) {
  const entity = state.allEntities.find(e => e.id === entityId);
  if (!entity) {
    contentEl.innerHTML = `<h1>Entity Not Found</h1><p class="diagram-error">Unknown entity: ${escapeHtml(entityId)}</p>`;
    return;
  }

  showEntity(entity);
}

function showDiagramList(title, paths) {
  state.activeTable = null;
  renderTableNav(getCurrentVisibleTableIds());
  const items = paths.map(p => {
    const name = p.split('/').pop().replace('.mmd', '');
    return `<li><a href="#" class="table-link" onclick="event.preventDefault(); showMermaidDiagram('${escapeHtml(p)}', '${escapeHtml(name)}')">${escapeHtml(name)}</a></li>`;
  }).join('');
  contentEl.innerHTML = `<h1>${escapeHtml(title)}</h1><ul style="list-style:none;padding:0">${items}</ul>`;
}

function getCurrentVisibleTableIds() {
  const term = searchEl.value.trim().toLowerCase();
  if (!term) {
    return state.allTables;
  }

  return state.allTables.filter(t => t.toLowerCase().includes(term));

function applyFilters() {
  const term = searchEl.value.trim().toLowerCase();
  const typeFilter = typeFilterEl?.value.toLowerCase() ?? '';

  const showTables = !typeFilter || typeFilter === 'table';
  const filteredTables = showTables
    ? state.allTables.filter(t => !term || t.toLowerCase().includes(term))
    : [];

  const filteredEntities = state.allEntities.filter(e => {
    const matchesTerm = !term || e.name.toLowerCase().includes(term)
      || e.id.toLowerCase().includes(term) || e.type.toLowerCase().includes(term);
    const matchesType = !typeFilter || typeFilter === 'table'
      ? false
      : e.type.toLowerCase() === typeFilter;
    return matchesTerm && (typeFilter ? matchesType : true);
  });

  renderTableNav(filteredTables);
  renderEntitiesNav(filteredEntities);
}
}

async function loadText(path) {
  const res = await fetch(path);
  if (!res.ok) {
    throw new Error(`Unable to load ${path} (${res.status})`);
  }

  return res.text();
}

function escapeHtml(input) {
  return String(input)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
""";
}

public sealed record SiteManifest(
    string GeneratedRoot,
    string ErPath,
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> ViewpointIds,
    IReadOnlyList<string> LineagePaths,
    string DomainDepsPath,
    string C4ContextPath,
    string C4ContainerPath,
    string C4ComponentPath,
    IReadOnlyList<SiteEntitySummary> Entities);

public sealed record SiteEntitySummary(string Id, string Name, string Type, string? Domain, string? Description, string? OwnerTeam);