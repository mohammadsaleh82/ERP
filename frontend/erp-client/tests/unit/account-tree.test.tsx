/**
 * فاز ۵ — درخت حساب‌ها: عمق متغیر واقعی backend (سطوح با تعداد نامحدود از API،
 * شامل شاخه‌های تفصیلی چندسطحی)، باز/بسته، جست‌وجو، فیلتر سطح/ماهیت/وضعیت و قابل‌ثبت‌ها.
 */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { useCompanyStore } from '@/core/company/company.store';
import { AccountTree } from '@/features/accounts/components/account-tree';
import { AccountsPage } from '@/features/accounts';
import type { AccountTreeNodeDto } from '@/core/accounting/accounts.types';

let mock: AxiosMockAdapter;

const node = (partial: Partial<AccountTreeNodeDto> & { id: string; fullCode: string; name: string }): AccountTreeNodeDto => ({
  parentAccountId: null,
  code: partial.fullCode,
  level: 'Group',
  nature: 'Asset',
  normalBalance: 'Debit',
  isActive: true,
  allowPosting: false,
  children: [],
  ...partial,
});

/** درخت با عمق ۵ — سطح Detail خودش فرزند دارد (عمق متغیر، نه چهار سطح ثابت) */
const deepTree: AccountTreeNodeDto[] = [
  node({
    id: 'n1', fullCode: '1', name: 'دارایی‌ها', level: 'Group', children: [
      node({
        id: 'n11', fullCode: '10', name: 'دارایی جاری', level: 'General', children: [
          node({
            id: 'n111', fullCode: '1101', name: 'موجودی نقد', level: 'Subsidiary', children: [
              node({
                id: 'n1111', fullCode: '110101', name: 'صندوق', level: 'Detail', allowPosting: true, children: [
                  node({ id: 'n11111', fullCode: '11010101', name: 'صندوق مرکزی', level: 'Detail', allowPosting: true, normalBalance: 'Debit' }),
                ],
              }),
              node({ id: 'n1112', fullCode: '110102', name: 'بانک', level: 'Detail', allowPosting: true }),
            ],
          }),
        ],
      }),
    ],
  }),
  node({ id: 'n2', fullCode: '2', name: 'بدهی‌ها', level: 'Group', nature: 'Liability', normalBalance: 'Credit' }),
  node({ id: 'n3', fullCode: '3', name: 'حساب غیرفعال', level: 'Group', isActive: false }),
];

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر', roles: [], permissions: ['accounting:accounts:read', 'accounting:accounts:create', 'accounting:accounts:update', 'accounting:accounts:deactivate', 'accounting:accounts:delete'] },
    'tok', 't1', 'c1',
  );
  useCompanyStore.getState().setCompanyContext(
    { id: 't1', name: 'گروه آریا', companies: [{ id: 'c1', name: 'آریا', fiscalYear: 1404 }] },
    { id: 'c1', name: 'آریا', fiscalYear: 1404 },
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderTree = (ui: React.ReactElement) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('AccountTree — variable depth from backend', () => {
  it('renders 5-level nested detail branches recursively (no fixed 4-level limit)', () => {
    renderTree(<AccountTree nodes={deepTree} expandAll />);
    expect(screen.getByText('دارایی‌ها')).toBeInTheDocument();
    expect(screen.getByText('صندوق مرکزی')).toBeInTheDocument(); // عمق ۵
    expect(screen.getByTestId('tree-node-11010101')).toBeInTheDocument();
  });

  it('collapses children by default and expands on toggle', () => {
    renderTree(<AccountTree nodes={deepTree} />);
    expect(screen.queryByText('دارایی جاری')).toBeNull(); // ریشه بسته
    fireEvent.click(screen.getByRole('button', { name: 'باز کردن شاخه دارایی‌ها' }));
    expect(screen.getByText('دارایی جاری')).toBeInTheDocument();
  });

  it('filters by search term across any depth (parent stays visible with match)', () => {
    renderTree(<AccountTree nodes={deepTree} filterTerm="صندوق مرکزی" expandAll />);
    expect(screen.getByText('صندوق مرکزی')).toBeInTheDocument();
    expect(screen.queryByText('بدهی‌ها')).toBeNull();
    expect(screen.getByText('موجودی نقد')).toBeInTheDocument(); // والد مسیر حفظ می‌شود
  });

  it('filters by level, nature and status client-side on the tree data', () => {
    renderTree(
      <AccountTree
        nodes={deepTree.filter((n) => n.id !== 'n3').map((root) => ({
          ...root,
          children: root.children.filter((c) => c.level !== 'General'),
        }))}
      />,
    );
    expect(screen.queryByText('دارایی جاری')).toBeNull();
    expect(screen.getByText('بدهی‌ها')).toBeInTheDocument();
  });

  it('postableOnly hides summary-only branches', () => {
    renderTree(<AccountTree nodes={deepTree} postableOnly expandAll />);
    expect(screen.queryByText('بدهی‌ها')).toBeNull(); // تجمیعی بدون فرزند قابل‌ثبت
    expect(screen.getByText('صندوق')).toBeInTheDocument();
  });

  it('shows error and empty states', () => {
    const { rerender } = renderTree(<AccountTree nodes={[]} isError onRetry={() => undefined} />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
    rerender(<AccountTree nodes={[]} />);
    expect(screen.getByText('حسابی ثبت نشده است')).toBeInTheDocument();
  });
});

describe('AccountsPage — full integration', () => {
  it('loads real tree from GET /Accounts/tree and opens detail on click', async () => {
    mock.onGet('/Accounts/tree').reply(200, deepTree);
    mock.onGet('/Accounts/n1111').reply(200, {
      id: 'n1111', tenantId: 't1', companyId: 'c1', parentAccountId: 'n111', code: '110101', fullCode: '110101', name: 'صندوق',
      description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false,
      allowPosting: true, hasChildren: true, requireCostCenter: false, requireProject: false, requireDetailAccount: false,
      createdAtUtc: '2026-01-01T00:00:00Z', lastModifiedAtUtc: null,
    });
    mock.onGet('/Accounts/n1111/breadcrumb').reply(200, [
      { id: 'n1', code: '1', fullCode: '1', name: 'دارایی‌ها', level: 'Group' },
      { id: 'n11', code: '10', fullCode: '10', name: 'دارایی جاری', level: 'General' },
      { id: 'n111', code: '1101', fullCode: '1101', name: 'موجودی نقد', level: 'Subsidiary' },
    ]);
    mock.onGet('/Accounts/n1111/children').reply(200, [
      { id: 'n11111', tenantId: 't1', companyId: 'c1', parentAccountId: 'n1111', code: '11010101', fullCode: '11010101', name: 'صندوق مرکزی', description: null, level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false, allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false, createdAtUtc: '2026-01-01T00:00:00Z', lastModifiedAtUtc: null },
    ]);

    renderTree(<AccountsPage />);
    await waitFor(() => expect(screen.getByText('دارایی‌ها')).toBeInTheDocument());

    // باز کردن شاخه‌ها تا رسیدن به حساب هدف
    fireEvent.click(screen.getByRole('button', { name: 'باز کردن شاخه دارایی‌ها' }));
    fireEvent.click(screen.getByRole('button', { name: 'باز کردن شاخه دارایی جاری' }));
    fireEvent.click(screen.getByRole('button', { name: 'باز کردن شاخه موجودی نقد' }));
    fireEvent.click(screen.getByTestId('tree-node-110101'));
    expect(await screen.findByTestId('account-details')).toBeInTheDocument();
    expect(screen.getAllByText('صندوق').length).toBeGreaterThanOrEqual(1);
    await waitFor(() => expect(screen.getByTestId('account-breadcrumb').textContent).toContain('دارایی جاری'));
    expect(screen.getByText('صندوق مرکزی')).toBeInTheDocument(); // فرزندان
    expect(screen.getByTestId('edit-account')).toBeInTheDocument();
  });
});
