'use client';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { signOut } from 'firebase/auth';
import { ReactNode } from 'react';
import { auth } from '@/lib/firebase';
import { AuthGate, useAdmin } from './AuthGate';

const NAV = [
  ['/', 'Overview'], ['/levels/', 'Levels'], ['/content/', 'Worlds, Shop & Achievements'], ['/rewards/', 'Rewards & Economy'],
  ['/remote-config/', 'Remote Config'], ['/events/', 'Events & Announcements'], ['/leaderboards/', 'Leaderboards'],
  ['/errors/', 'Crashes & Errors'], ['/admins/', 'Admins'], ['/audit/', 'Audit log'],
] as const;

function Frame({ children }: { children: ReactNode }) {
  const path = usePathname();
  const { user, role } = useAdmin();
  return (
    <div className="shell">
      <nav>
        <div className="brand"><img src="/tfy-logo.png" alt="" width={36} height={36} /> Cat Adventure</div>
        {NAV.filter(([href]) => href !== '/admins/' || role === 'super_admin').map(([href, label]) => (
          <Link key={href} href={href} className={path === href || (href !== '/' && path.startsWith(href)) ? 'active' : ''}>{label}</Link>
        ))}
        <div className="me">{user?.email}<br /><span className="pill">{role}</span><button onClick={() => signOut(auth)}>Sign out</button></div>
      </nav>
      <main>{children}</main>
    </div>
  );
}

export function Shell({ children }: { children: ReactNode }) {
  return <AuthGate><Frame>{children}</Frame></AuthGate>;
}
