'use client';
import { onIdTokenChanged, signInWithEmailAndPassword, signOut, User } from 'firebase/auth';
import { createContext, ReactNode, useContext, useEffect, useState } from 'react';
import { auth, isEmulator } from '@/lib/firebase';

export type Role = 'admin' | 'super_admin';
const RoleContext = createContext<{ user: User | null; role: Role | null }>({ user: null, role: null });
export const useAdmin = () => useContext(RoleContext);

/**
 * Only email accounts with a `role` custom claim (admin / super_admin) get in. Players use anonymous accounts and can never
 * hold a role; the Cloud Functions and Firestore rules check the same claim, so hiding the UI is not the only protection.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [role, setRole] = useState<Role | null>(null);
  const [ready, setReady] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  useEffect(() => onIdTokenChanged(auth, async (u) => {
    setUser(u);
    if (u && !u.isAnonymous) {
      const token = await u.getIdTokenResult();
      const r = token.claims.role;
      setRole(r === 'admin' || r === 'super_admin' ? r : null);
    } else setRole(null);
    setReady(true);
  }), []);

  if (!ready) return <div className="center">Loading…</div>;

  if (!user || !role) {
    return (
      <div className="center">
        <form className="card login" onSubmit={async (e) => {
          e.preventDefault(); setError('');
          try { await signInWithEmailAndPassword(auth, email, password); }
          catch { setError('Sign-in failed. Check the email and password.'); }
        }}>
          <img src="/tfy-logo.png" alt="TFY" width={96} height={96} />
          <h1>TFY Cat Adventure — Admin</h1>
          {user && !role && <p className="warn">This account is not an administrator. Ask a super admin to grant access, then sign in again.</p>}
          <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" required /></label>
          <label>Password<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required /></label>
          {error && <p className="error">{error}</p>}
          <button className="primary" type="submit">Sign in</button>
          {user && <button type="button" onClick={() => signOut(auth)}>Sign out</button>}
          {isEmulator && <p className="muted">Emulator mode</p>}
        </form>
      </div>
    );
  }
  return <RoleContext.Provider value={{ user, role }}>{children}</RoleContext.Provider>;
}
