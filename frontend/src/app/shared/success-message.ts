import { signal } from '@angular/core';

/** A transient success banner: shows a message, starts fading it after 3s,
 * then clears it after 5s. Used by pages with a "saved!"-style toast. */
export class SuccessMessage {
  private timer: number | null = null;
  private clearTimer: number | null = null;
  readonly message = signal('');
  readonly fading = signal(false);

  show(message: string): void {
    if (this.timer !== null) window.clearTimeout(this.timer);
    if (this.clearTimer !== null) window.clearTimeout(this.clearTimer);
    this.fading.set(false);
    this.message.set(message);
    this.timer = window.setTimeout(() => this.fading.set(true), 3000);
    this.clearTimer = window.setTimeout(() => {
      this.message.set('');
      this.timer = null;
      this.clearTimer = null;
      this.fading.set(false);
    }, 5000);
  }

  clear(): void {
    this.message.set('');
  }
}
