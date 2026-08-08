import { useEffect, useRef } from 'react';
import './ConfirmModal.css';

export default function ConfirmModal({
  open,
  title,
  message,
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Hủy',
  loading = false,
  danger = false,
  onConfirm,
  onCancel
}) {
  const dialogRef = useRef(null);
  const cancelRef = useRef(null);
  const openerRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    openerRef.current = document.activeElement;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    requestAnimationFrame(() => cancelRef.current?.focus());

    const onKeyDown = event => {
      if (event.key === 'Escape' && !loading) {
        event.preventDefault();
        onCancel?.();
        return;
      }
      if (event.key !== 'Tab') return;
      const focusable = dialogRef.current?.querySelectorAll(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      );
      if (!focusable?.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.body.style.overflow = previousOverflow;
      requestAnimationFrame(() => openerRef.current?.focus?.());
    };
  }, [loading, onCancel, open]);

  if (!open) return null;
  return <div className="app-dialog-backdrop" onMouseDown={event => {
    if (event.target === event.currentTarget && !loading) onCancel?.();
  }}>
    <section
      ref={dialogRef}
      className="app-dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="app-dialog-title"
      aria-describedby="app-dialog-message"
    >
      <div className="app-dialog-body">
        <h2 id="app-dialog-title">{title}</h2>
        <p id="app-dialog-message">{message}</p>
      </div>
      <div className="app-dialog-actions">
        <button ref={cancelRef} type="button" className="btn btn-outline-secondary" disabled={loading} onClick={onCancel}>{cancelLabel}</button>
        <button type="button" className={`btn ${danger ? 'btn-danger' : 'btn-primary'}`} disabled={loading} onClick={onConfirm}>
          {loading && <span className="spinner-border spinner-border-sm me-2" aria-hidden="true" />}{confirmLabel}
        </button>
      </div>
    </section>
  </div>;
}
