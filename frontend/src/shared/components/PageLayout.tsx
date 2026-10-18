import type { PropsWithChildren } from 'react';

export default function PageLayout({
  children,
  wide = false,
  className = '',
  as: Tag = 'main',
}: PropsWithChildren<{
  wide?: boolean;
  className?: string;
  as?: 'main' | 'div';
}>) {
  return (
    <Tag
      className={`w-full ${wide ? 'max-w-none' : 'max-w-[1600px]'} mx-auto p-4 md:px-6 md:pt-6 md:pb-8 xl:px-8 flex flex-col gap-6 ${className}`}
    >
      {children}
    </Tag>
  );
}
