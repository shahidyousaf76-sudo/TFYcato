import { getApps, initializeApp } from 'firebase/app';
import { connectAuthEmulator, getAuth } from 'firebase/auth';
import { connectFirestoreEmulator, getFirestore } from 'firebase/firestore';
import { connectFunctionsEmulator, getFunctions, httpsCallable } from 'firebase/functions';

const useEmulators = process.env.NEXT_PUBLIC_USE_EMULATORS === 'true';

const app = getApps()[0] ?? initializeApp({
  apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY || 'demo-key',
  authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
  projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID || 'demo-tfy-cat',
  appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID,
});

export const auth = getAuth(app);
export const db = getFirestore(app);
export const functions = getFunctions(app, process.env.NEXT_PUBLIC_FUNCTIONS_REGION || 'europe-west1');

let connected = false;
if (useEmulators && typeof window !== 'undefined' && !connected) {
  connected = true;
  connectAuthEmulator(auth, 'http://127.0.0.1:9099', { disableWarnings: true });
  connectFirestoreEmulator(db, '127.0.0.1', 8080);
  connectFunctionsEmulator(functions, '127.0.0.1', 5001);
}

/** Typed wrapper for a callable Cloud Function. Errors carry friendly server messages. */
export function call<Req = unknown, Res = unknown>(name: string) {
  const fn = httpsCallable<Req, Res>(functions, name);
  return async (data?: Req): Promise<Res> => (await fn(data as Req)).data;
}

export const isEmulator = useEmulators;
