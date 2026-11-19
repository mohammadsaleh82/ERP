/** فرم حساب: اعتبارسنجی مطابق AccountValidators.cs + قید ویرایش غیرقابل‌تغییر + انتخاب والد */
import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import AxiosMockAdapter from 'axios-mock-adapter';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { apiClient } from '@/core/api/client';
import { useAuthStore } from '@/core/auth/auth.store';
import { AccountFormDialog } from '@/features/accounts/components/account-form-dialog';
import type { AccountTreeNodeDto } from '@/core/accounting/accounts.types';

let mock: AxiosMockAdapter;

const tree: AccountTreeNodeDto[] = [
  {
    id: 'p1', parentAccountId: null, code: '1', fullCode: '1', name: 'دارایی‌ها', level: 'Group',
    nature: 'Asset', normalBalance: 'Debit', isActive: true, allowPosting: false, children: [],
  },
];

const editingAccount = {
  id: 'e1', tenantId: 't1', companyId: 'c1', parentAccountId: 'p1', code: '110101', fullCode: '110101', name: 'صندوق',
  description: 'قدیمی', level: 'Detail', nature: 'Asset', normalBalance: 'Debit', isActive: true, isSystemAccount: false,
  allowPosting: true, hasChildren: false, requireCostCenter: false, requireProject: false, requireDetailAccount: false,
  createdAtUtc: '2026-01-01T00:00:00Z', lastModifiedAtUtc: null,
};

beforeEach(() => {
  mock = new AxiosMockAdapter(apiClient, { onNoMatch: 'throwException' });
  useAuthStore.getState().setAuth(
    { userId: 'u1', email: 'a@b.c', fullName: 'مدیر', roles: [], permissions: ['accounting:accounts:create', 'accounting:accounts:update'] },
    'tok', 't1', 'c1',
  );
});
afterEach(() => {
  mock.restore();
  useAuthStore.getState().clearAuth();
});

const renderForm = (props: Partial<Parameters<typeof AccountFormDialog>[0]> = {}) => {
  const queryClient = new QueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AccountFormDialog open onOpenChange={() => undefined} tree={tree} onSaved={() => undefined} {...props} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('AccountFormDialog — create validation (backend validator messages)', () => {
  it('rejects non-numeric code with exact backend message', async () => {
    renderForm();
    fireEvent.change(screen.getByTestId('account-code-input'), { target: { value: '11a1' } });
    fireEvent.click(screen.getByTestId('account-form-submit'));
    expect(await screen.findByText('کد حساب باید صرفاً شامل ارقام عددی باشد.')).toBeInTheDocument();
    expect(mock.history.post.filter((call) => call.url.includes('/Accounts'))).toHaveLength(0);
  });

  it('rejects long code (>20) and empty name', async () => {
    renderForm();
    fireEvent.change(screen.getByTestId('account-code-input'), { target: { value: '1'.repeat(21) } });
    fireEvent.click(screen.getByTestId('account-form-submit'));
    expect(await screen.findByText('کد حساب نمی‌تواند بیش از ۲۰ کاراکتر باشد.')).toBeInTheDocument();
    expect(screen.getByText('نام حساب الزامی است.')).toBeInTheDocument();
  });

  it('submits real CreateAccountRequest payload with parent selection', async () => {
    renderForm();
    // انتخاب والد با picker
    fireEvent.click(screen.getByTestId('pick-parent'));
    fireEvent.click(screen.getByTestId('tree-node-1'));
    fireEvent.click(screen.getByTestId('tree-picker-confirm'));

    fireEvent.change(screen.getByTestId('account-code-input'), { target: { value: '1102' } });
    fireEvent.change(screen.getByTestId('account-name-input'), { target: { value: 'بانک ملت' } });

    mock.onPost('/Accounts').reply(201, { ...editingAccount, id: 'new1', fullCode: '11102', code: '1102', name: 'بانک ملت' });
    fireEvent.click(screen.getByTestId('account-form-submit'));

    await waitFor(() => expect(mock.history.post).toHaveLength(1));
    const body = JSON.parse(mock.history.post[0].data);
    expect(body).toMatchObject({
      parentAccountId: 'p1',
      code: '1102',
      name: 'بانک ملت',
      nature: 'Asset', // از والد به ارث رسید
      allowPosting: true,
    });
  });

  it('shows predicted full code from selected parent', () => {
    renderForm({ presetParent: tree[0] });
    fireEvent.change(screen.getByTestId('account-code-input'), { target: { value: '1102' } });
    expect(screen.getByText('کد کامل پیش‌بینی:')).toBeInTheDocument();
    expect(screen.getByText('11102')).toBeInTheDocument(); // parentFullCode + code
  });
});

describe('AccountFormDialog — edit mode immutable constraints', () => {
  it('renders fixed fields (code/level/nature) read-only and PUTs only UpdateAccountRequest fields', async () => {
    mock.onPut('/Accounts/e1').reply(200, editingAccount);
    renderForm({ editingAccount });

    expect(screen.getByText('110101')).toBeInTheDocument(); // کد فقط نمایش
    expect(screen.getByText('تفصیلی')).toBeInTheDocument(); // سطح
    expect(screen.queryByTestId('account-code-input')).toBeNull(); // فیلد کد در ویرایش وجود ندارد
    expect(screen.getByText('فیلدهای ثابت')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId('account-name-input'), { target: { value: 'صندوق اصلی' } });
    fireEvent.click(screen.getByTestId('account-require-cost-center')); // سوییچ تفصیلی
    fireEvent.click(screen.getByTestId('account-form-submit'));

    await waitFor(() => expect(mock.history.put).toHaveLength(1));
    const body = JSON.parse(mock.history.put[0].data);
    expect(body).toEqual({
      name: 'صندوق اصلی',
      description: 'قدیمی',
      normalBalance: 'Debit',
      requireCostCenter: true,
      requireProject: false,
      requireDetailAccount: false,
    });
    // قید: payload هیچ‌وقت code/parent/nature ندارد
    expect(body.code).toBeUndefined();
    expect(body.parentAccountId).toBeUndefined();
  });
});
