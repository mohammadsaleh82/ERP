import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CurrencyInput } from '@/shared/components/forms/currency-input';

describe('CurrencyInput', () => {
  it('accepts Persian digits and commits the real numeric value on blur', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<CurrencyInput value={null} onChange={onChange} currencyLabel="ریال" />);
    const input = screen.getByRole('textbox');

    await user.type(input, '۱۲۵۰۰۰۰');
    await user.tab(); // blur → commit

    const last = onChange.mock.calls.at(-1)?.[0];
    expect(last).toBe(1250000);
  });

  it('shows the currency label inside the field', () => {
    render(<CurrencyInput value={1000} onChange={() => undefined} currencyLabel="ریال" />);
    expect(screen.getByText('ریال')).toBeInTheDocument();
  });

  it('strips negatives unless explicitly allowed (financial safety)', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { rerender } = render(<CurrencyInput value={null} onChange={onChange} />);
    const input = screen.getByRole('textbox');

    await user.type(input, '-500');
    await user.tab();
    const committed = onChange.mock.calls.at(-1)?.[0];
    expect(committed).toBe(500); // Math.abs

    rerender(<CurrencyInput value={null} onChange={onChange} allowNegative />);
    await user.clear(input);
    await user.type(input, '-300');
    await user.tab();
    expect(onChange.mock.calls.at(-1)?.[0]).toBe(-300);
  });

  it('handles empty input by committing null', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<CurrencyInput value={12345} onChange={onChange} />);
    const input = screen.getByRole('textbox');
    await user.clear(input);
    await user.tab();
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('is disabled and shows grouped display value', () => {
    render(<CurrencyInput value={9876543} onChange={() => undefined} disabled />);
    const input = screen.getByRole('textbox') as HTMLInputElement;
    expect(input.disabled).toBe(true);
    expect(input.value).toBe('9,876,543');
  });
});
