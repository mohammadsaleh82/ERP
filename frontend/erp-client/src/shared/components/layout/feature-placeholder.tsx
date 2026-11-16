import React from 'react';
import { Breadcrumb } from '@/shared/components/layout/breadcrumb';
import { EmptyState } from '@/shared/components/feedback/empty-state';

interface FeaturePlaceholderProps {
  title: string;
  moduleName: string;
  description: string;
}

export const FeaturePlaceholder: React.FC<FeaturePlaceholderProps> = ({
  title,
  moduleName,
  description,
}) => {
  return (
    <div className="space-y-6">
      <Breadcrumb items={[{ label: title }]} />
      <div>
        <h1 className="text-2xl font-bold text-foreground">{title}</h1>
        <p className="text-sm text-muted-foreground mt-1">{description}</p>
      </div>

      <EmptyState
        title={`ماژول ${moduleName}`}
        description="اسکلت این ماژول بر پایه معماری Feature-Based مستقر شده و در مراحل بعدی توسعه منطق تجاری آن پیاده‌سازی خواهد شد."
      />
    </div>
  );
};
