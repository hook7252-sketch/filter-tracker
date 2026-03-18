'use strict';

// ─── 상태 ────────────────────────────────────────────────────────────────────
let allTx = [];          // 파싱된 전체 거래내역
let filtered = [];       // 필터 적용 후
let currentPage = 1;
const PAGE_SIZE = 30;

// ─── 파서 정의 ───────────────────────────────────────────────────────────────
/**
 * 각 파서는 rows(string[][])를 받아 Transaction[] 또는 null 반환.
 * Transaction: { date: Date, desc: string, debit: number, credit: number, balance: number }
 */
const PARSERS = [
  { name: 'KB국민은행',   fn: parseKB },
  { name: '신한은행',     fn: parseShinhan },
  { name: '우리은행',     fn: parseWoori },
  { name: '하나은행',     fn: parseHana },
  { name: '카카오페이',   fn: parseKakao },
  { name: '네이버페이',   fn: parseNaver },
  { name: '공통 CSV',     fn: parseGeneric },
];

function parseKB(rows) {
  // 거래일시, 내용, 출금금액, 입금금액, 잔액, ...
  const hi = rows.findIndex(r => r.some(c => /거래일/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate  = h.findIndex(c => /거래일/.test(c));
  const iDesc  = h.findIndex(c => /내용|적요/.test(c));
  const iDebit = h.findIndex(c => /출금/.test(c));
  const iCredit= h.findIndex(c => /입금/.test(c));
  const iBal   = h.findIndex(c => /잔액/.test(c));
  if (iDate < 0 || iDesc < 0) return null;
  return rows.slice(hi + 1).map(r => toTx(r, iDate, iDesc, iDebit, iCredit, iBal)).filter(Boolean);
}

function parseShinhan(rows) {
  const hi = rows.findIndex(r => r.some(c => /거래일자|날짜/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate  = h.findIndex(c => /거래일자|날짜/.test(c));
  const iDesc  = h.findIndex(c => /거래내용|적요|내용/.test(c));
  const iDebit = h.findIndex(c => /출금/.test(c));
  const iCredit= h.findIndex(c => /입금/.test(c));
  const iBal   = h.findIndex(c => /잔액/.test(c));
  if (iDate < 0 || iDesc < 0) return null;
  return rows.slice(hi + 1).map(r => toTx(r, iDate, iDesc, iDebit, iCredit, iBal)).filter(Boolean);
}

function parseWoori(rows) {
  const hi = rows.findIndex(r => r.some(c => /거래일/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate  = h.findIndex(c => /거래일/.test(c));
  const iDesc  = h.findIndex(c => /적요|내용/.test(c));
  const iDebit = h.findIndex(c => /출금/.test(c));
  const iCredit= h.findIndex(c => /입금/.test(c));
  const iBal   = h.findIndex(c => /잔액/.test(c));
  if (iDate < 0 || iDesc < 0) return null;
  return rows.slice(hi + 1).map(r => toTx(r, iDate, iDesc, iDebit, iCredit, iBal)).filter(Boolean);
}

function parseHana(rows) {
  const hi = rows.findIndex(r => r.some(c => /거래일시|날짜/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate  = h.findIndex(c => /거래일시|날짜/.test(c));
  const iDesc  = h.findIndex(c => /거래내용|적요/.test(c));
  const iDebit = h.findIndex(c => /출금/.test(c));
  const iCredit= h.findIndex(c => /입금/.test(c));
  const iBal   = h.findIndex(c => /잔액/.test(c));
  if (iDate < 0 || iDesc < 0) return null;
  return rows.slice(hi + 1).map(r => toTx(r, iDate, iDesc, iDebit, iCredit, iBal)).filter(Boolean);
}

function parseKakao(rows) {
  // 카카오페이 거래일시, 구분, 거래처, 금액
  const hi = rows.findIndex(r => r.some(c => /거래일시/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate   = h.findIndex(c => /거래일시/.test(c));
  const iType   = h.findIndex(c => /구분/.test(c));
  const iDesc   = h.findIndex(c => /거래처|가맹점|내용/.test(c));
  const iAmount = h.findIndex(c => /금액/.test(c));
  if (iDate < 0 || iAmount < 0) return null;
  return rows.slice(hi + 1).flatMap(r => {
    if (!r[iDate]) return [];
    const d = parseDate(r[iDate]);
    if (!d) return [];
    const amt = parseNum(r[iAmount]);
    const type = r[iType] || '';
    const isCredit = /충전|입금|환불/.test(type);
    return [{ date: d, desc: r[iDesc] || type || '카카오페이', debit: isCredit ? 0 : amt, credit: isCredit ? amt : 0, balance: 0 }];
  });
}

function parseNaver(rows) {
  // 네이버페이: 결제일, 가맹점, 결제금액
  const hi = rows.findIndex(r => r.some(c => /결제일|주문일/.test(c)));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate   = h.findIndex(c => /결제일|주문일/.test(c));
  const iDesc   = h.findIndex(c => /가맹점|상품명|내용/.test(c));
  const iAmount = h.findIndex(c => /결제금액|금액/.test(c));
  if (iDate < 0 || iAmount < 0) return null;
  return rows.slice(hi + 1).flatMap(r => {
    if (!r[iDate]) return [];
    const d = parseDate(r[iDate]);
    if (!d) return [];
    const amt = parseNum(r[iAmount]);
    return [{ date: d, desc: r[iDesc] || '네이버페이', debit: amt, credit: 0, balance: 0 }];
  });
}

function parseGeneric(rows) {
  // 컬럼 이름으로 최대한 자동 매핑
  const hi = rows.findIndex(r => r.length >= 2 && r.some(c => c && c.trim().length > 0));
  if (hi < 0) return null;
  const h = rows[hi];
  const iDate  = h.findIndex(c => /date|날짜|일자|일시/i.test(c));
  const iDesc  = h.findIndex(c => /desc|내용|적요|가맹점|상호/i.test(c));
  const iDebit = h.findIndex(c => /출금|지출|debit/i.test(c));
  const iCredit= h.findIndex(c => /입금|수입|credit/i.test(c));
  const iAmt   = h.findIndex(c => /금액|amount/i.test(c));
  const iBal   = h.findIndex(c => /잔액|balance/i.test(c));
  if (iDate < 0 && iDesc < 0) return null;
  const effectiveDebit  = iDebit  >= 0 ? iDebit  : iAmt;
  const effectiveCredit = iCredit >= 0 ? iCredit : -1;
  return rows.slice(hi + 1).map(r => toTx(r, iDate, iDesc, effectiveDebit, effectiveCredit, iBal)).filter(Boolean);
}

// ─── 헬퍼 ────────────────────────────────────────────────────────────────────
function toTx(r, iDate, iDesc, iDebit, iCredit, iBal) {
  if (!r || r.every(c => !c || !c.trim())) return null;
  const d = parseDate(r[iDate]);
  if (!d) return null;
  return {
    date:    d,
    desc:    (r[iDesc] || '').trim() || '(내용없음)',
    debit:   iDebit  >= 0 ? parseNum(r[iDebit])  : 0,
    credit:  iCredit >= 0 ? parseNum(r[iCredit]) : 0,
    balance: iBal    >= 0 ? parseNum(r[iBal])    : 0,
  };
}

function parseDate(s) {
  if (!s) return null;
  const clean = s.trim().replace(/[./년월일\s]/g, c => /\d/.test(c) ? c : '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
  // YYYY-MM-DD or YYYYMMDD
  let m = clean.match(/^(\d{4})-?(\d{2})-?(\d{2})/);
  if (!m) return null;
  const d = new Date(+m[1], +m[2] - 1, +m[3]);
  return isNaN(d) ? null : d;
}

function parseNum(s) {
  if (!s) return 0;
  const n = parseFloat(s.toString().replace(/[^\d.-]/g, ''));
  return isNaN(n) ? 0 : Math.abs(n);
}

function fmtDate(d) {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}.${mm}.${dd}`;
}

function fmtNum(n) {
  if (!n) return '';
  return n.toLocaleString('ko-KR') + '원';
}

// ─── CSV 파싱 ─────────────────────────────────────────────────────────────────
function parseCSV(text) {
  const lines = text.split(/\r?\n/);
  return lines.map(line => {
    const cols = [];
    let cur = '', inQ = false;
    for (let i = 0; i < line.length; i++) {
      const ch = line[i];
      if (ch === '"') { inQ = !inQ; continue; }
      if (!inQ && ch === ',') { cols.push(cur.trim()); cur = ''; continue; }
      cur += ch;
    }
    cols.push(cur.trim());
    return cols;
  });
}

// ─── 파일 읽기 ────────────────────────────────────────────────────────────────
async function readFile(file, encoding) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = e => resolve(e.target.result);
    reader.onerror = reject;
    reader.readAsText(file, encoding);
  });
}

async function loadFile(file, encoding) {
  const text = await readFile(file, encoding);
  const rows = parseCSV(text);

  let result = null;
  let parserName = '';
  for (const p of PARSERS) {
    const r = p.fn(rows);
    if (r && r.length > 0) { result = r; parserName = p.name; break; }
  }

  if (!result || result.length === 0) {
    alert('거래내역을 인식하지 못했습니다.\n인코딩을 변경하거나 다른 파일을 시도해 보세요.');
    return;
  }

  allTx = result.sort((a, b) => b.date - a.date);
  console.log(`[${parserName}] ${allTx.length}건 로드 완료`);

  // 날짜 기본값 설정
  const dates = allTx.map(t => t.date);
  const minD = new Date(Math.min(...dates));
  const maxD = new Date(Math.max(...dates));
  document.getElementById('dateFrom').value = toInputDate(minD);
  document.getElementById('dateTo').value   = toInputDate(maxD);

  showSections();
  applyFilter();
}

function toInputDate(d) {
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
}

// ─── UI 제어 ──────────────────────────────────────────────────────────────────
function showSections() {
  document.getElementById('encodingRow').hidden  = false;
  document.getElementById('filterSection').hidden = false;
  document.getElementById('tableSection').hidden  = false;
}

function applyFilter() {
  const from   = document.getElementById('dateFrom').value;
  const to     = document.getElementById('dateTo').value;
  const search = document.getElementById('searchText').value.toLowerCase();
  const type   = document.getElementById('typeFilter').value;

  filtered = allTx.filter(t => {
    if (from && t.date < new Date(from)) return false;
    if (to   && t.date > new Date(to + 'T23:59:59')) return false;
    if (search && !t.desc.toLowerCase().includes(search)) return false;
    if (type === 'debit'  && t.debit  === 0) return false;
    if (type === 'credit' && t.credit === 0) return false;
    return true;
  });

  currentPage = 1;
  renderTable();
  renderSummary();
}

function renderSummary() {
  const totalDebit  = filtered.reduce((s, t) => s + t.debit,  0);
  const totalCredit = filtered.reduce((s, t) => s + t.credit, 0);
  document.getElementById('summary').innerHTML =
    `<span>${filtered.length}건</span>` +
    `<span>출금 <span class="debit">${fmtNum(totalDebit)}</span></span>` +
    `<span>입금 <span class="credit">${fmtNum(totalCredit)}</span></span>`;

  const empty = document.getElementById('emptyMsg');
  empty.hidden = filtered.length > 0;
  document.getElementById('tableSection').hidden = filtered.length === 0;
}

function renderTable() {
  const start = (currentPage - 1) * PAGE_SIZE;
  const page  = filtered.slice(start, start + PAGE_SIZE);

  const tbody = document.getElementById('txBody');
  tbody.innerHTML = page.map(t => `
    <tr>
      <td class="td-date">${fmtDate(t.date)}</td>
      <td class="td-desc">${escHtml(t.desc)}</td>
      <td class="number amount-debit">${t.debit  ? fmtNum(t.debit)  : ''}</td>
      <td class="number amount-credit">${t.credit ? fmtNum(t.credit) : ''}</td>
      <td class="number amount-balance">${t.balance ? fmtNum(t.balance) : ''}</td>
    </tr>
  `).join('');

  renderPagination();
}

function renderPagination() {
  const total = Math.ceil(filtered.length / PAGE_SIZE);
  const el    = document.getElementById('pagination');
  if (total <= 1) { el.innerHTML = ''; return; }

  const range = [];
  for (let i = Math.max(1, currentPage - 2); i <= Math.min(total, currentPage + 2); i++) range.push(i);

  el.innerHTML = [
    currentPage > 1 ? `<button class="page-btn" data-p="${currentPage-1}">‹</button>` : '',
    ...range.map(p => `<button class="page-btn ${p === currentPage ? 'active' : ''}" data-p="${p}">${p}</button>`),
    currentPage < total ? `<button class="page-btn" data-p="${currentPage+1}">›</button>` : '',
  ].join('');

  el.querySelectorAll('.page-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      currentPage = +btn.dataset.p;
      renderTable();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  });
}

function escHtml(s) {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ─── 이벤트 바인딩 ─────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  const fileInput    = document.getElementById('fileInput');
  const dropZone     = document.getElementById('dropZone');
  const encodingSelect = document.getElementById('encodingSelect');
  let currentFile    = null;

  // 파일 선택
  fileInput.addEventListener('change', e => {
    currentFile = e.target.files[0];
    if (currentFile) loadFile(currentFile, encodingSelect.value);
  });

  // 드래그 앤 드롭
  dropZone.addEventListener('dragover', e => { e.preventDefault(); dropZone.classList.add('over'); });
  dropZone.addEventListener('dragleave', () => dropZone.classList.remove('over'));
  dropZone.addEventListener('drop', e => {
    e.preventDefault();
    dropZone.classList.remove('over');
    currentFile = e.dataTransfer.files[0];
    if (currentFile) loadFile(currentFile, encodingSelect.value);
  });
  dropZone.addEventListener('click', () => fileInput.click());

  // 인코딩 변경 후 다시 읽기
  document.getElementById('reloadBtn').addEventListener('click', () => {
    if (currentFile) loadFile(currentFile, encodingSelect.value);
  });

  // 필터 이벤트
  ['dateFrom','dateTo','searchText','typeFilter'].forEach(id => {
    document.getElementById(id).addEventListener('input', applyFilter);
    document.getElementById(id).addEventListener('change', applyFilter);
  });

  document.getElementById('resetDate').addEventListener('click', () => {
    if (allTx.length === 0) return;
    const dates = allTx.map(t => t.date);
    document.getElementById('dateFrom').value = toInputDate(new Date(Math.min(...dates)));
    document.getElementById('dateTo').value   = toInputDate(new Date(Math.max(...dates)));
    applyFilter();
  });
});
