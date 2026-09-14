export function focusAfterRender(elementId: string): void {
  setTimeout(() => document.getElementById(elementId)?.focus());
}

/** Scrolls a just-rendered element (e.g. a success message) into view without moving
 * keyboard focus — appropriate for a role="status" element, which screen readers
 * announce on their own. */
export function scrollIntoViewAfterRender(elementId: string): void {
  setTimeout(() =>
    document.getElementById(elementId)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }),
  );
}
