import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Pagination } from '@/shared/components/ui/pagination';

describe('Pagination', () => {
  it('renders Persian page numbers with the current one highlighted', () => {
    const { container } = render(<Pagination page={2} pageCount={6} onChange={() => undefined} />);
    expect(container.querySelector('[aria-current="page"]')?.textContent).toBe('۲');
    expect(screen.getByText('۳')).toBeInTheDocument();
  });

  it('disables previous on first page and navigates on click', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { rerender } = render(<Pagination page={1} pageCount={6} onChange={onChange} />);

    expect(screen.getByRole('button', { name: 'صفحه قبل' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'صفحه بعد' }));
    expect(onChange).toHaveBeenCalledWith(2);

    rerender(<Pagination page={6} pageCount={6} onChange={onChange} />);
    expect(screen.getByRole('button', { name: 'صفحه بعد' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'صفحه قبل' })).toBeEnabled();
  });
});
