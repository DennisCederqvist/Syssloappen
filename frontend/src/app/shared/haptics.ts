/** Progressive-enhancement haptic tap. Only Android/Chrome (incl. installed
 * PWA) implements navigator.vibrate — iOS Safari/WebKit never does, even
 * installed. No-ops silently everywhere else; not a feature to build UI around. */
export function vibrateOnTap(): void {
  navigator.vibrate?.(40);
}
