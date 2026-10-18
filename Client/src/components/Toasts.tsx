import { createContext, use, useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { Icon } from "./Icon";
import styles from "./Toasts.module.css";

type Tone = "info" | "error";

interface Toast {
  id: number;
  message: string;
  tone: Tone;
}

interface ToastContextValue {
  /** Shows a visible message that is also announced to screen readers. */
  notify: (message: string, tone?: Tone) => void;
  /** Announces a message to screen readers only. */
  announce: (message: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const INFO_TIMEOUT = 6000;
const MAX_TOASTS = 3;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [announcement, setAnnouncement] = useState<{ id: number; message: string } | null>(null);
  const nextId = useRef(0);
  const timers = useRef(new Map<number, ReturnType<typeof setTimeout>>());
  const regionRef = useRef<HTMLDivElement>(null);

  const dismiss = useCallback((id: number) => {
    clearTimeout(timers.current.get(id));
    timers.current.delete(id);
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const scheduleDismiss = useCallback(
    function schedule(id: number) {
      timers.current.set(
        id,
        setTimeout(() => {
          // Keep messages on screen while the pointer or keyboard focus is on them.
          if (regionRef.current?.matches(":hover, :focus-within")) schedule(id);
          else dismiss(id);
        }, INFO_TIMEOUT),
      );
    },
    [dismiss],
  );

  const notify = useCallback(
    (message: string, tone: Tone = "info") => {
      const id = ++nextId.current;
      setToasts((current) => [...current, { id, message, tone }].slice(-MAX_TOASTS));
      // Errors stay until dismissed so nobody misses them.
      if (tone === "info") scheduleDismiss(id);
    },
    [scheduleDismiss],
  );

  const announce = useCallback((message: string) => {
    setAnnouncement({ id: ++nextId.current, message });
  }, []);

  const value = useMemo(() => ({ notify, announce }), [notify, announce]);

  const renderToasts = (tone: Tone) =>
    toasts
      .filter((toast) => toast.tone === tone)
      .map((toast) => (
        <div key={toast.id} className={styles.toast} data-tone={toast.tone}>
          {tone === "error" && <Icon name="alert" className={styles.icon} />}
          <p className={styles.message}>{toast.message}</p>
          <button
            type="button"
            className="icon-button"
            onClick={() => {
              dismiss(toast.id);
            }}
          >
            <Icon name="close" size={18} />
            <span className="visually-hidden">Dismiss</span>
          </button>
        </div>
      ));

  return (
    <ToastContext value={value}>
      {children}
      <div ref={regionRef} className={styles.region}>
        {/* Live regions stay mounted so additions are reliably announced. */}
        <div role="status" className={styles.stack}>
          {renderToasts("info")}
        </div>
        <div role="alert" className={styles.stack}>
          {renderToasts("error")}
        </div>
      </div>
      <div role="status" className="visually-hidden">
        {announcement && <p key={announcement.id}>{announcement.message}</p>}
      </div>
    </ToastContext>
  );
}

export function useToast(): ToastContextValue {
  const context = use(ToastContext);
  if (!context) throw new Error("useToast must be used inside <ToastProvider>.");
  return context;
}
