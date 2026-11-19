import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Checkbox, Radio, Switch } from '@/shared/components/ui/selection-controls';

describe('Checkbox / Radio', () => {
  it('toggles checkbox and reports change', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Checkbox label="فقط اسناد قطعی" onCheckedChange={onChange} onChange={onChange} />);
    const box = screen.getByRole('checkbox');
    await user.click(box);
    expect(onChange).toHaveBeenCalled();
    expect(screen.getByText('فقط اسناد قطعی')).toBeInTheDocument();
  });

  it('renders radio with a label', () => {
    render(<Radio label="بدهکار" name="nature" />);
    expect(screen.getByRole('radio', { name: 'بدهکار' })).toBeInTheDocument();
  });
});

describe('Switch', () => {
  it('toggles role=switch via click and keyboard', async () => {
    const user = userEvent.setup();
    const onCheckedChange = vi.fn();
    const { rerender } = render(<Switch checked={false} onCheckedChange={onCheckedChange} label="نمایش ارقام فارسی" />);
    const sw = screen.getByRole('switch');

    expect(sw).toHaveAttribute('aria-checked', 'false');
    await user.click(sw);
    expect(onCheckedChange).toHaveBeenCalledWith(true);

    // keyboard: Space on a focused native button
    rerender(<Switch checked={true} onCheckedChange={onCheckedChange} />);
    screen.getByRole('switch').focus();
    await user.keyboard(' ');
    expect(onCheckedChange).toHaveBeenLastCalledWith(false);
    // کنترل‌شده است: aria-checked فقط با تغییر prop والد تغییر می‌کند
    rerender(<Switch checked={false} onCheckedChange={onCheckedChange} />);
    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'false');
  });
});
