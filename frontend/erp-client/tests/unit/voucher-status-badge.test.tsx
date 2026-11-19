import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VoucherStatusBadge } from '@/shared/components/financial/voucher-status-badge';
import { FiscalPeriodStatusBadge } from '@/shared/components/financial/fiscal-period';

describe('VoucherStatusBadge', () => {
  it('renders known statuses with their tone colors', () => {
    const { container } = render(<VoucherStatusBadge status="posted" />);
    expect(screen.getByText('قطعی شده')).toBeInTheDocument();
    expect(container.firstElementChild?.className).toContain('bg-success');
  });

  it('falls back to neutral for unknown status strings', () => {
    const { container } = render(<VoucherStatusBadge status="future-status" />);
    expect(screen.getByText('future-status')).toBeInTheDocument();
    expect(container.firstElementChild?.className).not.toContain('bg-success');
  });
});

describe('FiscalPeriodStatusBadge', () => {
  it('renders open/closed periods distinctly', () => {
    const { container, rerender } = render(<FiscalPeriodStatusBadge status="open" />);
    expect(screen.getByText('باز')).toBeInTheDocument();
    expect(container.firstElementChild?.className).toContain('bg-success');

    rerender(<FiscalPeriodStatusBadge status="closed" />);
    expect(screen.getByText('بسته')).toBeInTheDocument();
    expect(container.firstElementChild?.className).not.toContain('bg-success');
  });
});
