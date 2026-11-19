import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  BalanceDisplay,
  DebitCreditDisplay,
  MoneyDisplay,
} from '@/shared/components/financial/money-display';

describe('MoneyDisplay', () => {
  it('renders a large amount with grouping and currency', () => {
    render(<MoneyDisplay amount={1234567890} options={{ withCurrency: true }} aria-label="جمع سند" />);
    const el = screen.getByLabelText('جمع سند');
    expect(el).toHaveTextContent('۱٬۲۳۴٬۵۶۷٬۸۹۰');
    expect(el).toHaveTextContent('ریال');
    expect(el).toHaveAttribute('dir', 'ltr');
  });

  it('marks negative amounts with the danger class for financial scanning', () => {
    const { container } = render(<MoneyDisplay amount={-900} highlightNegative />);
    expect(container.firstElementChild?.className).toContain('text-danger');
    expect(container.firstElementChild?.textContent).toContain('−۹۰۰');
  });

  it('shows em-dash for zero when zeroAsDash', () => {
    render(<MoneyDisplay amount={0} options={{ zeroAsDash: true }} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});

describe('DebitCreditDisplay', () => {
  it('colors debit amounts red and credit amounts green', () => {
    const { container: debitContainer } = render(<DebitCreditDisplay amount={850000} nature="debit" />);
    expect(debitContainer.firstElementChild?.className).toContain('text-debit');
    expect(debitContainer.firstElementChild?.className).toContain('bg-debit-soft');

    const { container: creditContainer } = render(<DebitCreditDisplay amount={850000} nature="credit" />);
    expect(creditContainer.firstElementChild?.className).toContain('text-credit');
  });

  it('renders zero amounts neutrally without a nature tone', () => {
    const { container } = render(<DebitCreditDisplay amount={0} nature="debit" />);
    expect(container.firstElementChild?.className).not.toContain('text-debit');
    expect(container.firstElementChild?.className).not.toContain('bg-debit-soft');
    expect(container.firstElementChild?.textContent).toContain('۰');
  });
});

describe('BalanceDisplay', () => {
  it('shows positive balance as بدهکار', () => {
    render(<BalanceDisplay balance={2500000} currency="ریال" />);
    expect(screen.getByText('بدهکار')).toBeInTheDocument();
    expect(screen.getByText('(ریال)')).toBeInTheDocument();
  });

  it('shows negative balance as بستانکار', () => {
    render(<BalanceDisplay balance={-180000} />);
    expect(screen.getByText('بستانکار')).toBeInTheDocument();
  });

  it('shows an explicit zero-balance pill', () => {
    render(<BalanceDisplay balance={0} />);
    expect(screen.getByText('مانده صفر')).toBeInTheDocument();
  });
});
