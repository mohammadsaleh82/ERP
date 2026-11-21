/**
 * Mock Backend قراردادمحور برای E2E — دقیقاً مطابق DTOهای واقعی backend
 * (auth.service/documents.types/reports.types/accounts.types/dashboard.types).
 *
 * Stateful است: سند ساخته‌شده → submit → approve → post واقعاً وضعیت را تغییر می‌دهد
 * و دفتر کل/تراز آزمایشی/داشبورد از همان تجمیع می‌شود — نه پاسخ‌های ثابت.
 * جداسازی شرکت با X-Company-Id اعمال می‌شود (شواهد IDOR/عدم نشت داده).
 *
 * ⚠️ این لایه جای تست یکپارچه با backend واقعی را نمی‌گیرد؛ قرارداد را قفل می‌کند
 * و جریان UI را آزمایش می‌کند. موارد backend-dependent در گزارش نهایی Blocked ثبت شده‌اند.
 */
import { Page, Request } from '@playwright/test';

/* ── داده پایه قراردادی ─────────────────────────────────────────────── */
export const TENANT_A = { id: 't-aria', name: 'گروه آریا' };
export const TENANT_B = { id: 't-bam', name: 'هلدینگ بام' };
export const COMPANY_C1 = { id: 'c-aria', name: 'آریا صنعت', fiscalYear: 1404 };
export const COMPANY_C2 = { id: 'c-bam', name: 'بام پارس', fiscalYear: 1403 };

export const FULL_PERMISSIONS = [
  'accounting:documents:read', 'accounting:documents:create', 'accounting:documents:update', 'accounting:documents:submit',
  'accounting:documents:approve', 'accounting:documents:post', 'accounting:accounts:read',
  'accounting:accounts:manage', 'accounting:fiscal-years:read', 'accounting:fiscal-years:manage',
  'accounting:reports:read', 'accounting:ledger:read', 'accounting:settings:read', 'dashboard:read',
];
export const RESTRICTED_PERMISSIONS = ['dashboard:read', 'accounting:documents:read', 'accounting:documents:create'];
export const APPROVER_PERMISSIONS = [
  'dashboard:read', 'accounting:documents:read', 'accounting:documents:approve', 'accounting:documents:post',
  'accounting:accounts:read', 'accounting:reports:read', 'accounting:ledger:read',
];

const USER_FULL = { email: 'accountant@aria.ir', fullName: 'حسابدار ارشد', password: 'Str0ng!Pass' };
const USER_MANAGER = { email: 'manager@aria.ir', fullName: 'مدیر مالی', password: 'M4nager!2026' };
const USER_LIMITED = { email: 'viewer@aria.ir', fullName: 'کاربر محدود', password: 'V3ry!Str0ng' };

const FY1 = 'fy-1404';
const CASH = { id: 'acc-cash', code: '1101', fullCode: '110101', name: 'صندوق', nature: 'Asset', normalBalance: 'Debit' };
const RECEIVABLE = { id: 'acc-ar', code: '1102', fullCode: '110201', name: 'حساب‌های دریافتنی', nature: 'Asset', normalBalance: 'Debit' };
const REVENUE = { id: 'acc-rev', code: '4001', fullCode: '400101', name: 'درآمد فروش', nature: 'Revenue', normalBalance: 'Credit' };
const EXPENSE = { id: 'acc-exp', code: '5001', fullCode: '500101', name: 'هزینه اداری', nature: 'Expense', normalBalance: 'Debit' };

export const ACCOUNTS = [CASH, RECEIVABLE, REVENUE, EXPENSE];

const accDto = (a: typeof CASH, companyId: string) => ({
  id: a.id, tenantId: TENANT_A.id, companyId, parentAccountId: null, code: a.code, fullCode: a.fullCode,
  name: a.name, description: null, level: 'Detail', nature: a.nature, normalBalance: a.normalBalance,
  isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false,
  requireCostCenter: false, requireProject: false, requireDetailAccount: false,
  createdAtUtc: '2025-03-21T00:00:00Z', lastModifiedAtUtc: null,
});

const treeNode = (a: typeof CASH): unknown => ({
  id: a.id, parentAccountId: null, code: a.code, fullCode: a.fullCode, name: a.name,
  level: 'Detail', nature: a.nature, normalBalance: a.normalBalance, isActive: true,
  allowPosting: true, children: [],
});

/* ── state stateful ─────────────────────────────────────────────────── */
interface PostedLine { accountId: string; side: 'Debit' | 'Credit'; amount: number; }
interface PostedDoc {
  id: string; companyId: string; documentNumber: number; documentDate: string;
  description: string; status: string; lines: PostedLine[]; referenceNumber: string | null;
  createdBy: string;
}

const state = {
  tokens: new Map<string, { email: string; permissions: string[] }>(),
  /** refresh cookie value → session — قرارداد واقعی: نشست با HttpOnly cookie */
  refreshTokens: new Map<string, { email: string; permissions: string[]; tenantId: string; companyId: string }>(),
  documents: new Map<string, PostedDoc>(), // key: companyId
  nextNumber: 101,
  requests: [] as { url: string; method: string }[],
};

export const resetState = () => {
  state.tokens.clear();
  state.refreshTokens.clear();
  state.documents.clear();
  state.nextNumber = 101;
  state.requests.length = 0;
  seed();
};

const seedPosted = (companyId: string, number: number, description: string, date: string, lines: PostedLine[], createdBy = USER_MANAGER.email) => {
  const id = `doc-${number}-${companyId}`;
  state.documents.set(`${companyId}:${id}`, { id, companyId, documentNumber: number, documentDate: date, description, status: 'Posted', lines, referenceNumber: null, createdBy });
};

/** هر شرکت یک سند Posted اولیه دارد — تجمیع گزارش‌ها از همین‌ها */
const seed = () => {
  seedPosted(COMPANY_C1.id, 100, 'فروش نقدی فروردین', '2026-04-01', [
    { accountId: CASH.id, side: 'Debit', amount: 5_000_000 },
    { accountId: REVENUE.id, side: 'Credit', amount: 5_000_000 },
  ]);
  seedPosted(COMPANY_C2.id, 1, 'فروش شرکت بام', '2025-04-01', [
    { accountId: CASH.id, side: 'Debit', amount: 900_000 },
    { accountId: REVENUE.id, side: 'Credit', amount: 900_000 },
  ]);
};
seed();

/* ── تجمیع واقعی از اسناد Posted ────────────────────────────────────── */
const postedOf = (companyId: string) =>
  [...state.documents.values()].filter((d) => d.companyId === companyId && d.status === 'Posted');

const aggregate = (companyId: string) => {
  const map = new Map<string, { opening: number; debit: number; credit: number }>();
  for (const a of ACCOUNTS) map.set(a.id, { opening: 0, debit: 0, credit: 0 });
  for (const doc of postedOf(companyId)) {
    for (const line of doc.lines) {
      const row = map.get(line.accountId)!;
      if (line.side === 'Debit') row.debit += line.amount; else row.credit += line.amount;
    }
  }
  return map;
};

/* ── پاسخ‌های قراردادی ──────────────────────────────────────────────── */
const loginResponse = (user: typeof USER_FULL, permissions: string[], companyId: string, accessToken: string) => ({
  accessToken,
  email: user.email,
  fullName: user.fullName,
  tenantId: TENANT_A.id,
  companyId,
  permissions,
});

const companiesResponse = (email: string) => {
  const isFull = email === USER_FULL.email;
  return [
    {
      tenantId: TENANT_A.id,
      tenantName: TENANT_A.name,
      companies: isFull
        ? [
            { companyId: COMPANY_C1.id, name: COMPANY_C1.name, activeFiscalYear: COMPANY_C1.fiscalYear, isActive: true, membershipActive: true },
            { companyId: COMPANY_C2.id, name: COMPANY_C2.name, activeFiscalYear: COMPANY_C2.fiscalYear, isActive: true, membershipActive: true },
          ]
        : [
            { companyId: COMPANY_C1.id, name: COMPANY_C1.name, activeFiscalYear: COMPANY_C1.fiscalYear, isActive: true, membershipActive: true },
          ],
    },
  ];
};

const meResponse = (email: string, permissions: string[], companyId: string | null) => ({
  userId: `u-${email.split('@')[0]}`,
  email,
  activeTenantId: TENANT_A.id,
  activeCompanyId: companyId,
  roles: email === USER_FULL.email ? ['Accountant'] : ['Viewer'],
  permissions,
});

const fiscalYearDto = (companyId: string) => ({
  id: FY1, tenantId: TENANT_A.id, companyId, yearNumber: companyId === COMPANY_C1.id ? 1404 : 1403,
  name: companyId === COMPANY_C1.id ? 'سال مالی ۱۴۰۴' : 'سال مالی ۱۴۰۳',
  startDate: companyId === COMPANY_C1.id ? '2026-03-21' : '2025-03-21',
  endDate: companyId === COMPANY_C1.id ? '2027-03-20' : '2026-03-20',
  calendarType: 'Persian', status: 'Open', isClosed: false, openingBalancePosted: true,
});

const periodsResponse = (companyId: string) => [
  {
    id: 'p-1', fiscalYearId: FY1, periodNumber: 1, name: 'فروردین',
    startDate: companyId === COMPANY_C1.id ? '2026-03-21' : '2025-03-21',
    endDate: companyId === COMPANY_C1.id ? '2026-04-20' : '2025-04-20',
    status: 'Open', isAdjustmentPeriod: false,
  },
];

const documentDto = (doc: PostedDoc) => ({
  id: doc.id, tenantId: TENANT_A.id, companyId: doc.companyId, fiscalYearId: FY1, fiscalPeriodId: 'p-1',
  documentNumber: doc.documentNumber, documentDate: doc.documentDate, description: doc.description,
  documentType: 'General', status: doc.status, referenceNumber: doc.referenceNumber, sourceModule: null,
  sourceDocumentId: null, reversedDocumentId: null,
  totalDebit: doc.lines.filter((l) => l.side === 'Debit').reduce((s, l) => s + l.amount, 0).toFixed(2),
  totalCredit: doc.lines.filter((l) => l.side === 'Credit').reduce((s, l) => s + l.amount, 0).toFixed(2),
  currencyCode: 'IRR', rejectionReason: null,
  createdBy: doc.createdBy, createdAtUtc: new Date().toISOString(), lastModifiedAtUtc: null,
});

const documentDetailDto = (doc: PostedDoc) => ({
  ...documentDto(doc),
  isBalanced:
    doc.lines.filter((l) => l.side === 'Debit').reduce((s, l) => s + l.amount, 0) ===
    doc.lines.filter((l) => l.side === 'Credit').reduce((s, l) => s + l.amount, 0),
  lines: doc.lines.map((line, index) => {
    const account = ACCOUNTS.find((a) => a.id === line.accountId)!;
    return {
      id: `line-${index + 1}`, accountId: account.id, accountFullCode: account.fullCode,
      accountName: account.name, lineNumber: index + 1, side: line.side,
      debitAmount: line.side === 'Debit' ? line.amount.toFixed(2) : '0.00',
      creditAmount: line.side === 'Credit' ? line.amount.toFixed(2) : '0.00',
      description: doc.description, costCenterId: null, projectId: null, detailAccountId: null,
      foreignCurrencyCode: null, foreignCurrencyAmount: null, exchangeRate: 1,
    };
  }),
});

/** بدنه پاسخ دفتر کل — از تجمیع اسناد Posted همان شرکت */
const generalLedgerResponse = (companyId: string) => {
  const agg = aggregate(companyId);
  return {
    fiscalYearId: FY1, fromDate: '2026-03-21', toDate: '2027-03-20',
    totalCount: ACCOUNTS.length, pageNumber: 1, pageSize: 50,
    totalPeriodDebit: [...agg.values()].reduce((s, r) => s + r.debit, 0).toFixed(2),
    totalPeriodCredit: [...agg.values()].reduce((s, r) => s + r.credit, 0).toFixed(2),
    accounts: ACCOUNTS.map((a) => {
      const row = agg.get(a.id)!;
      const closing = row.debit - row.credit;
      return {
        accountId: a.id, accountCode: a.fullCode, accountName: a.name,
        openingDebit: '0.00', openingCredit: '0.00',
        debitTurnover: row.debit.toFixed(2), creditTurnover: row.credit.toFixed(2),
        closingDebit: closing > 0 ? closing.toFixed(2) : '0.00',
        closingCredit: closing < 0 ? (-closing).toFixed(2) : '0.00',
      };
    }),
  };
};

const trialBalanceResponse = (companyId: string) => {
  const agg = aggregate(companyId);
  const rows = ACCOUNTS.map((a) => {
    const row = agg.get(a.id)!;
    const closing = row.debit - row.credit;
    return {
      accountId: a.id, accountCode: a.fullCode, accountName: a.name, level: 'Detail',
      openingDebit: '0.00', openingCredit: '0.00',
      periodDebit: row.debit.toFixed(2), periodCredit: row.credit.toFixed(2),
      closingDebit: closing > 0 ? closing.toFixed(2) : '0.00',
      closingCredit: closing < 0 ? (-closing).toFixed(2) : '0.00',
    };
  });
  const totalDebit = rows.reduce((s, r) => s + Number(r.periodDebit), 0).toFixed(2);
  const totalCredit = rows.reduce((s, r) => s + Number(r.periodCredit), 0).toFixed(2);
  return {
    companyId, fiscalYearName: 'سال مالی ۱۴۰۴', fromDate: '2026-03-21', toDate: '2027-03-20',
    aggregationLevel: 'Detail', isBalanced: totalDebit === totalCredit,
    totalDebit, totalCredit, difference: '0.00', rows,
  };
};

const ledgerBalancesResponse = (companyId: string) => {
  const agg = aggregate(companyId);
  return ACCOUNTS.filter((a) => a.id === CASH.id || a.id === REVENUE.id).map((a) => {
    const row = agg.get(a.id)!;
    const closing = row.debit - row.credit;
    return {
      accountId: a.id, accountFullCode: a.fullCode, accountName: a.name, normalBalance: a.normalBalance,
      openingBalance: 0, periodDebit: row.debit, periodCredit: row.credit, closingBalance: closing,
      openingDirection: 'None', closingDirection: closing >= 0 ? 'Debit' : 'Credit',
      openingDisplayBalance: 0, closingDisplayBalance: Math.abs(closing),
    };
  });
};

const ledgerTransactionsResponse = (companyId: string, accountId: string) => {
  const account = ACCOUNTS.find((a) => a.id === accountId)!;
  const rows = postedOf(companyId)
    .filter((d) => d.lines.some((l) => l.accountId === accountId))
    .map((doc) => {
      const line = doc.lines.find((l) => l.accountId === accountId)!;
      return {
        entryId: `${doc.id}-l`, documentId: doc.id, documentNumber: doc.documentNumber,
        lineNumber: 1, entryDate: doc.documentDate, description: doc.description,
        debit: line.side === 'Debit' ? line.amount.toFixed(2) : '0.00',
        credit: line.side === 'Credit' ? line.amount.toFixed(2) : '0.00',
        currencyCode: 'IRR', sourceModule: null, sourceDocumentId: null,
      };
    });
  const debit = rows.reduce((s, r) => s + Number(r.debit), 0);
  const credit = rows.reduce((s, r) => s + Number(r.credit), 0);
  const closing = debit - credit;
  return {
    accountId, accountFullCode: account.fullCode, accountName: account.name,
    openingBalance: '0.00', totalDebit: debit.toFixed(2), totalCredit: credit.toFixed(2),
    closingBalance: closing.toFixed(2), closingDirection: closing >= 0 ? 'Debit' : 'Credit',
    closingDisplayBalance: Math.abs(closing).toFixed(2),
    totalCount: rows.length, pageNumber: 1, pageSize: 50, transactions: rows,
  };
};

const auditTrailResponse = () => ({
  items: [
    { id: 'at-1', userId: 'u-accountant', action: 'DocumentCreated', entityType: 'Document', entityId: 'doc-100-c-aria', timestampUtc: new Date().toISOString(), correlationId: 'e2e-1', reason: null },
    { id: 'at-2', userId: 'u-accountant', action: 'DocumentPosted', entityType: 'Document', entityId: 'doc-100-c-aria', timestampUtc: new Date().toISOString(), correlationId: 'e2e-2', reason: null },
  ],
});

/* ── نصب interception روی صفحه ──────────────────────────────────────── */
export const installMockBackend = async (page: Page) => {
  await page.route('**/api/v1/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^.*\/api\/v1/, '');
    const method = request.method();
    state.requests.push({ url: `${method} ${path}${url.search}`, method });

    const body = method === 'POST' || method === 'PUT' || method === 'PATCH' ? request.postDataJSON() ?? {} : {};
    const authHeader = request.headers()['authorization'] ?? '';
    const session = [...state.tokens.entries()].find(([token]) => authHeader === `Bearer ${token}`)?.[1];
    const companyIdHeader = request.headers()['x-company-id'] ?? COMPANY_C1.id;
    const companyScoped = companyIdHeader === COMPANY_C2.id ? COMPANY_C2 : COMPANY_C1;
    const json = (data: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) });

    /* Auth */
    if (path === '/Auth/login' && method === 'POST') {
      const user = [USER_FULL, USER_MANAGER, USER_LIMITED].find((u) => u.email === body.email && u.password === body.password);
      if (!user) return json({ title: 'Unauthorized', status: 401, detail: 'ایمیل یا کلمه عبور اشتباه است' }, 401);
      const permissions = user.email === USER_FULL.email ? FULL_PERMISSIONS : user.email === USER_MANAGER.email ? APPROVER_PERMISSIONS : RESTRICTED_PERMISSIONS;
      const accessToken = `e2e-token-${encodeURIComponent(user.email)}-${Date.now()}`;
      const refreshToken = `rt-${user.email}-${Date.now()}`;
      state.tokens.set(accessToken, { email: user.email, permissions });
      state.refreshTokens.set(refreshToken, { email: user.email, permissions, tenantId: TENANT_A.id, companyId: COMPANY_C1.id });
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'Set-Cookie': `erp-refresh=${refreshToken}; HttpOnly; Path=/api/v1/Auth; SameSite=Strict` },
        body: JSON.stringify(loginResponse(user, permissions, COMPANY_C1.id, accessToken)),
      });
    }
    if (path === '/Auth/companies' && method === 'GET') return json(companiesResponse(session?.email ?? USER_FULL.email));
    if (path === '/Auth/me' && method === 'GET') return json(meResponse(session?.email ?? USER_FULL.email, session?.permissions ?? FULL_PERMISSIONS, companyScoped.id));
    if (path === '/Auth/logout' && method === 'POST') {
      state.tokens.clear();
      const cookie = request.headers()['cookie'] ?? '';
      const rt = cookie.match(/erp-refresh=([^;]+)/)?.[1];
      if (rt) state.refreshTokens.delete(rt);
      return route.fulfill({ status: 204, headers: { 'Set-Cookie': 'erp-refresh=; HttpOnly; Path=/api/v1/Auth; Max-Age=0' } });
    }
    if (path === '/Auth/refresh-token' && method === 'POST') {
      // قرارداد واقعی: بدون refresh cookie معتبر → 401 (نشست مهمان نمی‌سازد)
      const cookie = request.headers()['cookie'] ?? '';
      const rt = cookie.match(/erp-refresh=([^;]+)/)?.[1];
      const refreshSession = rt ? state.refreshTokens.get(rt) : undefined;
      if (!refreshSession) return json({ title: 'Unauthorized', status: 401, detail: 'نشست منقضی شده است' }, 401);
      const targetCompany = request.headers()['x-company-id'] === COMPANY_C2.id ? COMPANY_C2 : COMPANY_C1;
      const token = `tok-refreshed-${Date.now()}`;
      state.tokens.set(token, { email: refreshSession.email, permissions: refreshSession.permissions });
      return json({ accessToken: token, tenantId: TENANT_A.id, companyId: targetCompany.id, permissions: refreshSession.permissions });
    }

    /* داده‌های scopped به شرکت فعال */
    if (path === '/FiscalYears' && method === 'GET') return json([fiscalYearDto(companyScoped.id)]);
    if (path === '/FiscalYears/fy-1404/periods' && method === 'GET') return json(periodsResponse(companyScoped.id));
    if (path === '/Accounts/tree' && method === 'GET') return json(ACCOUNTS.map(treeNode));
    if (path === '/Accounts/postable' && method === 'GET') return json(ACCOUNTS.map((a) => accDto(a, companyScoped.id)));
    if (path === '/Accounts' && method === 'GET') {
      const term = url.searchParams.get('searchTerm') ?? '';
      const items = ACCOUNTS.filter((a) => a.name.includes(term) || a.fullCode.includes(term)).map((a) => accDto(a, companyScoped.id));
      return json({ items, totalCount: items.length, pageNumber: 1, pageSize: 10 });
    }
    if (path === '/Documents' && method === 'GET') {
      const docs = [...state.documents.values()].filter((d) => d.companyId === companyScoped.id)
        .sort((a, b) => b.documentNumber - a.documentNumber);
      const status = url.searchParams.get('status');
      const filtered = status ? docs.filter((d) => d.status === status) : docs;
      return json({ items: filtered.map(documentDto), totalCount: filtered.length, pageNumber: 1, pageSize: 20 });
    }
    if (path === '/Documents' && method === 'POST') {
      if (!session?.permissions.includes('accounting:documents:create')) return json({ title: 'Forbidden', status: 403, detail: 'مجوز ایجاد سند ندارید' }, 403);
      const number = state.nextNumber++;
      const id = `doc-${number}-${companyScoped.id}`;
      const lines: PostedLine[] = body.lines.map((l: { accountId: string; side: string; amount: string }) => ({
        accountId: l.accountId, side: l.side, amount: Number(l.amount),
      }));
      state.documents.set(`${companyScoped.id}:${id}`, {
        id, companyId: companyScoped.id, documentNumber: number, documentDate: body.documentDate,
        description: body.description, status: 'Draft', lines, referenceNumber: body.referenceNumber ?? null,
        createdBy: session.email,
      });
      return json(documentDto(state.documents.get(`${companyScoped.id}:${id}`)!), 201);
    }
    const docMatch = path.match(/^\/Documents\/([^/]+)$/);
    if (docMatch && method === 'GET') {
      const doc = state.documents.get(`${companyScoped.id}:${docMatch[1]}`);
      if (!doc) return json({ title: 'NotFound', status: 404, detail: 'سند یافت نشد' }, 404);
      return json(documentDetailDto(doc));
    }
    const auditMatch = path.match(/^\/Documents\/([^/]+)\/audit-trail$/);
    if (auditMatch && method === 'GET') {
      return json([
        { id: 'al-1', documentId: auditMatch[1], action: 'DocumentCreated', previousStatus: null, newStatus: 'Draft', details: null, performedBy: USER_FULL.email, performedAtUtc: new Date().toISOString() },
      ]);
    }
    const actionMatch = path.match(/^\/Documents\/([^/]+)\/(submit|approve|post)$/);
    if (actionMatch && method === 'POST') {
      const doc = state.documents.get(`${companyScoped.id}:${actionMatch[1]}`);
      if (!doc) return json({ title: 'NotFound', status: 404, detail: 'سند یافت نشد' }, 404);
      const required = { submit: 'accounting:documents:submit', approve: 'accounting:documents:approve', post: 'accounting:documents:post' }[actionMatch[2] as 'submit' | 'approve' | 'post'];
      if (!session?.permissions.includes(required)) return json({ title: 'Forbidden', status: 403, detail: `مجوز ${actionMatch[2]} را ندارید` }, 403);
      // سازوکار تفکیک وظایف (SOD) — مطابق backend: ایجادکننده نمی‌تواند تأیید کند
      if (actionMatch[2] === 'approve' && doc.createdBy === session.email) {
        return json({ title: 'Conflict', status: 409, detail: 'به دلیل تفکیک وظایف، ایجادکننده سند نمی‌تواند آن را تأیید کند' }, 409);
      }
      doc.status = { submit: 'PendingApproval', approve: 'Approved', post: 'Posted' }[actionMatch[2] as 'submit' | 'approve' | 'post'];
      if (actionMatch[2] === 'post') {
        return json({ documentId: doc.id, documentNumber: doc.documentNumber, totalDebit: documentDto(doc).totalDebit, totalCredit: documentDto(doc).totalCredit, postedAtUtc: new Date().toISOString(), postedBy: USER_FULL.email, wasAlreadyPosted: false });
      }
      return json(documentDto(doc));
    }

    /* گزارش‌ها — مجوز backend-level هم شبیه‌سازی می‌شود (دفاع دوم در برابر UI بایپس) */
    const reportsAllowed = session?.permissions.includes('accounting:reports:read') || session?.permissions.includes('accounting:ledger:read');
    if ((path.startsWith('/accounting/reports/') || path.startsWith('/accounting/ledger/')) && !reportsAllowed && path !== '/accounting/reports/export') {
      return json({ title: 'Forbidden', status: 403, detail: 'مجوز مشاهده گزارش را ندارید' }, 403);
    }
    if (path === '/accounting/ledger/journal' && method === 'GET') {
      // قرارداد JournalReportDto — entries به تفکیک سند + جمع روز/دوره
      const posted = postedOf(companyScoped.id).sort((a, b) => a.documentNumber - b.documentNumber);
      const entries = posted.map((doc) => ({
        documentId: doc.id, documentNumber: doc.documentNumber, documentDate: doc.documentDate,
        description: doc.description, sourceModule: null,
        totalDebit: doc.lines.filter((l) => l.side === 'Debit').reduce((sum, l) => sum + l.amount, 0).toFixed(2),
        totalCredit: doc.lines.filter((l) => l.side === 'Credit').reduce((sum, l) => sum + l.amount, 0).toFixed(2),
        lines: doc.lines.map((line, index) => {
          const account = ACCOUNTS.find((a) => a.id === line.accountId)!;
          return {
            accountId: account.id, accountFullCode: account.fullCode, accountName: account.name,
            lineNumber: index + 1, description: doc.description,
            debit: line.side === 'Debit' ? line.amount.toFixed(2) : '0.00',
            credit: line.side === 'Credit' ? line.amount.toFixed(2) : '0.00',
          };
        }),
      }));
      const byDate = new Map<string, { count: number; debit: number; credit: number }>();
      for (const doc of posted) {
        const row = byDate.get(doc.documentDate) ?? { count: 0, debit: 0, credit: 0 };
        row.count += 1;
        row.debit += doc.lines.filter((l) => l.side === 'Debit').reduce((sum, l) => sum + l.amount, 0);
        row.credit += doc.lines.filter((l) => l.side === 'Credit').reduce((sum, l) => sum + l.amount, 0);
        byDate.set(doc.documentDate, row);
      }
      const periodDebit = entries.reduce((sum, e) => sum + Number(e.totalDebit), 0).toFixed(2);
      const periodCredit = entries.reduce((sum, e) => sum + Number(e.totalCredit), 0).toFixed(2);
      return json({
        entries,
        dailyTotals: [...byDate.entries()].map(([date, row]) => ({ date, documentsCount: row.count, totalDebit: row.debit.toFixed(2), totalCredit: row.credit.toFixed(2) })),
        periodTotalDebit: periodDebit, periodTotalCredit: periodCredit,
        totalCount: entries.length, pageNumber: 1, pageSize: 50,
      });
    }
    if (path === '/accounting/ledger/general-ledger' && method === 'GET') return json(generalLedgerResponse(companyScoped.id));
    if (path.startsWith('/accounting/ledger/general-ledger/accounts/') && method === 'GET') {
      const accountId = path.split('/')[4];
      return json(ledgerTransactionsResponse(companyScoped.id, accountId));
    }
    if (path === '/accounting/reports/trial-balance' && method === 'GET') return json(trialBalanceResponse(companyScoped.id));
    if (path === '/accounting/reports/journal' && method === 'GET') {
      const rows = postedOf(companyScoped.id).flatMap((doc) =>
        doc.lines.map((line) => {
          const account = ACCOUNTS.find((a) => a.id === line.accountId)!;
          return {
            documentNumber: doc.documentNumber, entryDate: doc.documentDate, accountId: account.id,
            accountCode: account.fullCode, accountName: account.name, description: doc.description,
            debit: line.side === 'Debit' ? line.amount.toFixed(2) : '0.00',
            credit: line.side === 'Credit' ? line.amount.toFixed(2) : '0.00', currencyCode: 'IRR',
          };
        }),
      );
      return json({ rows, totalCount: rows.length, pageNumber: 1, pageSize: 50 });
    }
    if (path === '/Ledger/balances' && method === 'GET') return json(ledgerBalancesResponse(companyScoped.id));
    if (path === '/AuditTrail' && method === 'GET') return json(auditTrailResponse());

    /* خروجی گزارش — CSV واقعی از داده همان شرکت */
    if (path === '/accounting/reports/export' && method === 'POST') {
      const tb = trialBalanceResponse(companyScoped.id);
      const csv = 'کد حساب,نام حساب,بدهکار دوره,بستانکار دوره,بدهکار پایانی,بستانکار پایانی\n' +
        tb.rows.map((r) => [r.accountCode, r.accountName, r.periodDebit, r.periodCredit, r.closingDebit, r.closingCredit].join(',')).join('\n');
      return route.fulfill({ status: 200, contentType: 'text/csv; charset=utf-8', headers: { 'Content-Disposition': 'attachment; filename="trialbalance.csv"' }, body: csv });
    }

    /* صورت‌های مالی و بقیه — پاسخ قراردادی خالی سالم */
    if (path.startsWith('/accounting/reports/') || path.startsWith('/accounting/dimensions')) return json({ rows: [], rowsCount: 0, accounts: [], items: [], totalCount: 0, pageNumber: 1, pageSize: 50, totalDebit: '0.00', totalCredit: '0.00', difference: '0.00', isBalanced: true, companyId: companyScoped.id, fromDate: '2026-03-21', toDate: '2027-03-20', aggregationLevel: 'Detail' });
    return json({ title: 'NotMocked', status: 404, detail: `بدون mock: ${method} ${path}` }, 404);
  });
};

export const requestLog = () => state.requests;
export const findRequests = (predicate: (r: { url: string; method: string }) => boolean) => state.requests.filter(predicate);
export const users = { full: USER_FULL, manager: USER_MANAGER, limited: USER_LIMITED };
export const companyRefs = { c1: COMPANY_C1, c2: COMPANY_C2, tenantA: TENANT_A, tenantB: TENANT_B };
export const accountRefs = { cash: CASH, receivable: RECEIVABLE, revenue: REVENUE, expense: EXPENSE, fy1: FY1 };
export const docIdFor = (number: number, companyId = COMPANY_C1.id) => `doc-${number}-${companyId}`;
