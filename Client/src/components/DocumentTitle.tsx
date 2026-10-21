const APP_NAME = "JustAnotherList";

/**
 * Gives every page a unique, descriptive title (WCAG 2.4.2). React hoists
 * <title> into <head>, so pages render it like any other element.
 */
export function DocumentTitle({ title }: { title?: string }) {
  return <title>{title ? `${title} – ${APP_NAME}` : APP_NAME}</title>;
}
