import type { Metadata } from 'next';
import { ReactNode } from 'react';
import { Shell } from '@/components/Shell';
import './globals.css';

export const metadata: Metadata = { title: 'TFY Cat Adventure — Admin', robots: { index: false, follow: false } };

export default function RootLayout({ children }: { children: ReactNode }) {
  return <html lang="en"><body><Shell>{children}</Shell></body></html>;
}
