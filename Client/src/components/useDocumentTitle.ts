import { useEffect } from "react";

const APP_NAME = "JustAnotherList";

/** Gives every page a unique, descriptive title (WCAG 2.4.2). */
export function useDocumentTitle(title: string | null): void {
  useEffect(() => {
    document.title = title ? `${title} – ${APP_NAME}` : APP_NAME;
  }, [title]);
}
