import * as React from 'react';
import { SearchableSelect } from '@/shared/components/forms/combobox';

export interface DetailAccount {
  id: string;
  title: string;
  /** کد تفصیلی مثل D-1024 */
  code: string;
}

export interface DetailAccountSelectProps {
  accounts: readonly DetailAccount[];
  value: string | null;
  onChange: (id: string | null) => void;
  placeholder?: string;
  disabled?: boolean;
  'aria-label'?: string;
}

/** انتخاب تفصیلی — جستجو بر اساس عنوان و کد، کد با فونت monospace LTR */
export const DetailAccountSelect: React.FC<DetailAccountSelectProps> = ({
  accounts,
  value,
  onChange,
  placeholder = 'انتخاب تفصیلی…',
  disabled,
  'aria-label': ariaLabel,
}) => {
  const options = React.useMemo(
    () =>
      accounts.map((acc) => ({
        value: acc.id,
        label: acc.title,
        description: acc.code,
      })),
    [accounts],
  );

  return (
    <SearchableSelect
      options={options}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      disabled={disabled}
      aria-label={ariaLabel}
    />
  );
};
