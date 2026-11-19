import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BarChart, Bar } from 'recharts';
import { ChartContainer } from '@/shared/components/charts/chart-container';

const sampleData = [
  { name: 'فروردین', value: 120 },
  { name: 'اردیبهشت', value: 180 },
];

describe('ChartContainer', () => {
  it('wraps a recharts chart with an accessible, themed container', () => {
    render(
      <ChartContainer aria-label="فروش ماهانه">
        <BarChart data={sampleData}>
          <Bar dataKey="value" />
        </BarChart>
      </ChartContainer>,
    );

    expect(screen.getByRole('img', { name: 'فروش ماهانه' })).toBeInTheDocument();
  });

  it('uses a fixed default height with full width', () => {
    const { container } = render(
      <ChartContainer>
        <BarChart data={sampleData}>
          <Bar dataKey="value" />
        </BarChart>
      </ChartContainer>,
    );

    const wrapper = container.firstElementChild as HTMLElement | null;
    expect(wrapper?.style.height).toBe('320px');
    expect(wrapper?.className).toContain('w-full');
  });
});
