/** A text field from submitted form data; empty when missing (or a file). */
export function formText(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === "string" ? value : "";
}
