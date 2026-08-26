export function focusAfterRender(elementId: string): void {
  setTimeout(() => document.getElementById(elementId)?.focus());
}
