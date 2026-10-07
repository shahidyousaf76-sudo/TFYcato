import { getApps, initializeApp } from 'firebase-admin/app';
import { getAuth } from 'firebase-admin/auth';
import { getFirestore } from 'firebase-admin/firestore';
import { CallableRequest, HttpsError } from 'firebase-functions/v2/https';

if (getApps().length === 0) initializeApp();

export const db = getFirestore();
export const auth = getAuth();

/** Region close to the first market (Denmark / EU). Change in one place if needed. */
export const REGION = 'europe-west1';

export type Role = 'admin' | 'super_admin';

export function requireUser(req: CallableRequest): string {
  if (!req.auth?.uid) throw new HttpsError('unauthenticated', 'Please restart the game.');
  return req.auth.uid;
}

export function requireAdmin(req: CallableRequest, role: Role = 'admin'): string {
  const uid = requireUser(req);
  const r = req.auth?.token.role as string | undefined;
  const ok = role === 'super_admin' ? r === 'super_admin' : r === 'admin' || r === 'super_admin';
  // Anonymous players can never hold a role: roles are only set by adminSetRole on email accounts.
  if (!ok || req.auth?.token.firebase?.sign_in_provider === 'anonymous') throw new HttpsError('permission-denied', 'Admins only.');
  return uid;
}

/** Converts a domain failure into a callable error the client understands. */
export function fail(code: string, message: string): never {
  const known = ['invalid-argument', 'not-found', 'failed-precondition', 'permission-denied', 'already-exists', 'resource-exhausted', 'unauthenticated'];
  throw new HttpsError((known.includes(code) ? code : 'internal') as ConstructorParameters<typeof HttpsError>[0], message);
}
