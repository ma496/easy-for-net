"use client"

import { cn } from '@/lib/utils';
import { ClassValue } from 'clsx';
import React, { ReactNode, CSSProperties } from 'react';

interface AppScrollbarProps {
  children: ReactNode
  className?: ClassValue
  style?: CSSProperties
}

const AppScrollbar: React.FC<AppScrollbarProps> = ({ children, className, style }) => {
  const scrollbarStyle: CSSProperties = {
    ...style,
  };

  return (
    <div
      className={cn('overflow-auto scrollbar-thin scrollbar-thumb-secondary-scrollbar', className)}
      style={scrollbarStyle}
    >
      {children}
    </div>
  );
};

export { AppScrollbar }
