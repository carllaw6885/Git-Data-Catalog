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

  const domain = entity.domain
    ? `<p><strong>Domain:</strong> <span class="badge">${escapeHtml(entity.domain)}</span></p>`
    : '';
  contentEl.innerHTML = `
    <h1>${escapeHtml(entity.name)}</h1>
    <p><strong>Type:</strong> <span class="badge">${escapeHtml(entity.type)}</span></p>
    <p><strong>ID:</strong> <code>${escapeHtml(entity.id)}</code></p>
    ${domain}
  `;
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