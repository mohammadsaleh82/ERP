import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Input } from '@/shared/components/ui/input';
import { FormField } from '@/shared/components/forms/form-field';

describe('FormField', () => {
  it('associates the label with the control via htmlFor/id', () => {
    render(
      <FormField id="f1" label="نام حساب" required>
        <Input id="f1" />
      </FormField>,
    );
    const label = screen.getByText('نام حساب');
    expect(label).toHaveAttribute('for', 'f1');
    expect(screen.getByRole('textbox')).toBeInTheDocument();
    expect(screen.getByText('*')).toBeInTheDocument();
  });

  it('renders the field error near the control with role=alert', () => {
    render(
      <FormField id="f2" label="مبلغ" error="مبلغ باید بزرگ‌تر از صفر باشد">
        <Input id="f2" aria-invalid />
      </FormField>,
    );
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('مبلغ باید بزرگ‌تر از صفر باشد');
  });

  it('shows hint only when there is no error', () => {
    const { rerender } = render(
      <FormField id="f3" label="کد" hint="کد پنج‌رقمی است">
        <Input id="f3" />
      </FormField>,
    );
    expect(screen.getByText('کد پنج‌رقمی است')).toBeInTheDocument();

    rerender(
      <FormField id="f3" label="کد" hint="کد پنج‌رقمی است" error="کد تکراری است">
        <Input id="f3" />
      </FormField>,
    );
    expect(screen.queryByText('کد پنج‌رقمی است')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
