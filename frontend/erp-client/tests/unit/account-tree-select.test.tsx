import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AccountTreeSelect, type AccountNode } from '@/shared/components/financial/account-tree-select';

const nodes: readonly AccountNode[] = [
  { id: '1100', code: '1100', title: 'دارایی‌های جاری', parentId: null },
  { id: '1101', code: '1101', title: 'صندوق', parentId: '1100' },
  { id: '110101', code: '110101', title: 'صندوق مرکزی', parentId: '1101' },
];

describe('AccountTreeSelect', () => {
  it('opens the tree, shows hierarchy with codes, and selects a leaf', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<AccountTreeSelect nodes={nodes} value={null} onChange={onChange} aria-label="انتخاب حساب" />);

    await user.click(screen.getByRole('button', { name: 'انتخاب حساب' }));

    const tree = screen.getByRole('tree');
    expect(tree).toBeInTheDocument();
    expect(screen.getByText('دارایی‌های جاری')).toBeInTheDocument();
    expect(screen.getByText('صندوق مرکزی')).toBeInTheDocument();

    // leaf level click (کد حساب نمایش داده شود)
    await user.click(screen.getByText('صندوق مرکزی'));
    expect(onChange).toHaveBeenCalledWith('110101');
  });

  it('expands collapsed branches via the chevron toggle', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<AccountTreeSelect nodes={nodes} value={null} onChange={onChange} aria-label="انتخاب حساب" />);

    await user.click(screen.getByRole('button', { name: 'انتخاب حساب' }));
    // هر سه سطح قبل از collapse دیده می‌شوند
    expect(screen.getByText('110101')).toBeInTheDocument();
    // collapse والد '1101'
    const expandBtns = screen.getAllByRole('button', { name: 'بستن' });
    await user.click(expandBtns[expandBtns.length - 1]);
    expect(screen.queryByText('صندوق مرکزی')).not.toBeInTheDocument();
  });

  it('displays the selected account code in the trigger', () => {
    render(<AccountTreeSelect nodes={nodes} value="110101" onChange={() => undefined} />);
    expect(screen.getByText('110101')).toBeInTheDocument();
    expect(screen.getByText('صندوق مرکزی')).toBeInTheDocument();
  });
});
