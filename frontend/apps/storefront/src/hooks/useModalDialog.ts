import { useEffect, useRef, type KeyboardEvent, type MouseEvent, type RefObject, type SyntheticEvent } from "react";

const openDialogs = new Set<HTMLDialogElement>();
let previousBodyOverflow = "";

/** Native dialogs provide focus containment, focus restoration and nested Escape handling. */
export function useModalDialog(isOpen: boolean, onDismiss: () => void, initialFocus?: RefObject<HTMLElement | null>, returnFocus?: RefObject<HTMLElement | null>) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!isOpen || !dialog) return;

    const previousFocus = returnFocus?.current ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    dialog.showModal();
    if (openDialogs.size === 0) {
      previousBodyOverflow = document.body.style.overflow;
      document.body.style.overflow = "hidden";
    }
    openDialogs.add(dialog);
    initialFocus?.current?.focus();

    return () => {
      dialog.close();
      openDialogs.delete(dialog);
      if (openDialogs.size === 0) document.body.style.overflow = previousBodyOverflow;
      if (previousFocus?.isConnected) previousFocus.focus();
    };
  }, [initialFocus, isOpen, returnFocus]);

  return {
    ref,
    onCancel: (event: SyntheticEvent<HTMLDialogElement>) => { event.preventDefault(); onDismiss(); },
    onKeyDown: (event: KeyboardEvent<HTMLDialogElement>) => {
      if (event.key !== "Tab") return;
      const elements = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(
        'a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), summary, [tabindex="0"]'
      )).filter((element) => element.getClientRects().length > 0 && !element.closest("[inert]"));
      const first = elements[0];
      const last = elements.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    },
    onClick: (event: MouseEvent<HTMLDialogElement>) => {
      if (event.target !== event.currentTarget) return;
      const bounds = event.currentTarget.getBoundingClientRect();
      if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) onDismiss();
    }
  };
}
