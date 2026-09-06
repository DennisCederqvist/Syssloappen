import { signal } from '@angular/core';

// One card wobbles at a time, at a random moment — feels alive rather than a
// mechanical loop. 3–7s between events averages about one wobble every 5s;
// short enough to catch the eye now and then, long enough not to be twitchy.
const MIN_WOBBLE_INTERVAL_MS = 3000;
const MAX_WOBBLE_INTERVAL_MS = 7000;
const WOBBLE_EVENT_MS = 900;

/** Shared per-page motion state for child-view card grids (task cards,
 * reward cards, ...): a stable random permanent tilt per card id (1.5–3°,
 * random sign — random rather than alternating by grid position, since
 * alternating made every left-column card lean one way and every
 * right-column card lean the other, a mirrored/forced-looking pattern), and
 * a scheduler that wobbles one randomly-chosen card at a random interval.
 *
 * One instance per component; call `start()` in ngOnInit and `stop()` in
 * ngOnDestroy. `getCandidateIds` is re-invoked on every wobble tick, so it's
 * safe to pass a closure over a signal that changes over time. */
export class ChildCardMotion {
  readonly wobblingId = signal<number | null>(null);
  private readonly tiltById = new Map<number, number>();
  private lastWobbledId: number | null = null;
  private wobbleTimeout?: ReturnType<typeof setTimeout>;
  private clearTimeout?: ReturnType<typeof setTimeout>;

  constructor(private readonly getCandidateIds: () => number[]) {}

  start(): void {
    this.scheduleNext();
  }

  stop(): void {
    clearTimeout(this.wobbleTimeout);
    clearTimeout(this.clearTimeout);
  }

  tiltFor(id: number): number {
    let tilt = this.tiltById.get(id);
    if (tilt === undefined) {
      const magnitude = 1.5 + Math.random() * 1.5;
      tilt = Math.random() < 0.5 ? -magnitude : magnitude;
      this.tiltById.set(id, tilt);
    }
    return tilt;
  }

  private scheduleNext(): void {
    const delayMs =
      MIN_WOBBLE_INTERVAL_MS + Math.random() * (MAX_WOBBLE_INTERVAL_MS - MIN_WOBBLE_INTERVAL_MS);
    this.wobbleTimeout = setTimeout(() => this.triggerRandomWobble(), delayMs);
  }

  private triggerRandomWobble(): void {
    const candidates = this.getCandidateIds();
    if (candidates.length > 0) {
      // Avoid picking the same card twice in a row when there's a choice.
      const pool =
        candidates.length > 1 ? candidates.filter((id) => id !== this.lastWobbledId) : candidates;
      const chosen = pool[Math.floor(Math.random() * pool.length)];
      this.lastWobbledId = chosen;
      this.wobblingId.set(chosen);
      this.clearTimeout = setTimeout(() => this.wobblingId.set(null), WOBBLE_EVENT_MS);
    }
    this.scheduleNext();
  }
}
