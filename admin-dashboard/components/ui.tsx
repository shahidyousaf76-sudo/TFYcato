'use client';
import { ReactNode, useCallback, useEffect, useState } from 'react';
import { errorMessage } from '@/lib/api';

export function useLoad<T>(load: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const reload = useCallback(async () => {
    setLoading(true); setError('');
    try { setData(await load()); } catch (e) { setError(errorMessage(e)); } finally { setLoading(false); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  useEffect(() => { reload(); }, [reload]);
  return { data, error, loading, reload, setData };
}

/** Runs an admin action and reports the result (server validation messages are shown as-is). */
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const run = async (fn: () => Promise<unknown>, success = 'Saved.') => {
    setBusy(true); setMsg(null);
    try { await fn(); setMsg({ ok: true, text: success }); return true; }
    catch (e) { setMsg({ ok: false, text: errorMessage(e) }); return false; }
    finally { setBusy(false); }
  };
  return { busy, msg, run, setMsg };
}

export function Status({ msg }: { msg: { ok: boolean; text: string } | null }) {
  if (!msg) return null;
  return <p className={msg.ok ? 'ok' : 'error'}>{msg.text}</p>;
}

export function Card({ title, children, actions }: { title?: string; children: ReactNode; actions?: ReactNode }) {
  return <section className="card">{(title || actions) && <header><h2>{title}</h2><div>{actions}</div></header>}{children}</section>;
}

export function Stat({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return <div className="stat"><div className="value">{value}</div><div className="label">{label}</div>{hint && <div className="muted">{hint}</div>}</div>;
}

/** JSON editor with live parse errors. The server validates again before saving. */
export function JsonEditor<T>({ value, onSave, busy, rows = 22 }: { value: T; onSave: (v: T) => void; busy?: boolean; rows?: number }) {
  const [text, setText] = useState(JSON.stringify(value, null, 2));
  const [parseError, setParseError] = useState('');
  useEffect(() => { setText(JSON.stringify(value, null, 2)); }, [value]);
  return (
    <div className="json">
      <textarea rows={rows} value={text} spellCheck={false} onChange={(e) => {
        setText(e.target.value);
        try { JSON.parse(e.target.value); setParseError(''); } catch (err) { setParseError((err as Error).message); }
      }} />
      {parseError && <p className="error">{parseError}</p>}
      <button className="primary" disabled={busy || !!parseError} onClick={() => onSave(JSON.parse(text))}>Validate & save</button>
    </div>
  );
}

export const fmt = (n: number | undefined) => (n ?? 0).toLocaleString('en-GB');
export const date = (ms: number | undefined | null) => (ms ? new Date(ms).toLocaleString('en-GB') : '—');
