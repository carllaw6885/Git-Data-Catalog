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