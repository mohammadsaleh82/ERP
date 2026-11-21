import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NumberInput } from '@/shared/components/forms/number-input';

describe('NumberInput', () => {
  it('parses mixed Persian/English digits into a number', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<NumberInput value={null} onChange={onChange} />);
    const input = screen.getByRole('textbox');

    await user.type(input, '1۰2.5');
    expect(onChange).toHaveBeenLastCalledWith(102.5);
  });

  it('clamps to min on blur', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<NumberInput value={null} onChange={onChange} min={5} />);
    const input = screen.getByRole('textbox');

    await user.type(input, '2');
    await user.tab();
    expect(onChange).toHaveBeenLastCalledWith(5);
  });

  it('formats the display with grouping after blur', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<NumberInput value={null} onChange={onChange} />);
    const input = screen.getByRole('textbox');

    await user.type(input, '2500000');
    await user.tab();
    expect((input as HTMLInputElement).value).toBe('2,500,000');
  });

  it('respects negative values when provided', async () => {
    const onChange = vi.fn();
    render(<NumberInput value={-42} onChange={onChange} />);
    expect((screen.getByRole('textbox') as HTMLInputElement).value).toBe('-42');
  });
});
