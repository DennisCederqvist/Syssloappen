import { Component, signal } from '@angular/core';

interface ConfettiPiece {
  id: number;
  left: number;
  top: number;
  dx: number;
  dy: number;
  spin: number;
  delayMs: number;
  color: string;
  shape: 'circle' | 'star' | 'square';
}

const COLORS = ['#7fe0c8', '#ffc93c', '#ffb6b6', '#ffd9a0', '#b7c9ff'];
const PIECE_COUNT = 16;
const VISIBLE_MS = 1700;

function randomPieces(): ConfettiPiece[] {
  return Array.from({ length: PIECE_COUNT }, (_, index) => {
    const angle = (Math.PI * 2 * index) / PIECE_COUNT + (Math.random() - 0.5) * 0.6;
    const distance = 70 + Math.random() * 90;
    return {
      id: index,
      left: 50 + (Math.random() - 0.5) * 10,
      top: 50 + (Math.random() - 0.5) * 10,
      dx: Math.cos(angle) * distance,
      dy: Math.sin(angle) * distance - 30,
      spin: (Math.random() - 0.5) * 720,
      delayMs: Math.random() * 120,
      color: COLORS[index % COLORS.length],
      shape: index % 3 === 0 ? 'star' : index % 3 === 1 ? 'circle' : 'square',
    };
  });
}

/** Quick, non-blocking "Bra jobbat!" burst — confetti pieces fading outward
 * plus a message pill. Fires on demand via play(); no sound (a fanfare may be
 * added later, out of scope for now). */
@Component({
  selector: 'app-child-celebration',
  template: `
    @if (visible()) {
      <div class="pointer-events-none fixed inset-0 z-[60] flex items-center justify-center">
        @for (piece of pieces(); track piece.id) {
          <span
            class="absolute size-3 rounded-sm"
            [style.left.%]="piece.left"
            [style.top.%]="piece.top"
            [style.background]="piece.shape === 'star' ? 'transparent' : piece.color"
            [style.border-radius]="piece.shape === 'circle' ? '999px' : '4px'"
            [style.--dx.px]="piece.dx"
            [style.--dy.px]="piece.dy"
            [style.--spin.deg]="piece.spin"
            [style.animation-name]="'child-confetti-piece'"
            [style.animation-duration.ms]="950"
            [style.animation-timing-function]="'ease-out'"
            [style.animation-delay.ms]="piece.delayMs"
            [style.animation-fill-mode]="'forwards'"
            aria-hidden="true"
          >
            @if (piece.shape === 'star') {
              <svg viewBox="0 0 20 20" class="size-3" [style.fill]="piece.color">
                <path
                  d="M10 1.5l2.6 5.3 5.9.85-4.27 4.16 1.01 5.87L10 14.9l-5.24 2.78 1.01-5.87L1.5 7.65l5.9-.85z"
                />
              </svg>
            }
          </span>
        }

        <div
          class="flex items-center gap-2 rounded-[18px] bg-white px-5 py-3 shadow-[4px_6px_0_rgba(0,0,0,0.08)]"
        >
          <svg
            viewBox="0 0 20 20"
            class="size-5 shrink-0"
            fill="var(--color-child-star-fill)"
            stroke="var(--color-child-star-stroke)"
            stroke-width="1"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path
              d="M10 1.5l2.6 5.3 5.9.85-4.27 4.16 1.01 5.87L10 14.9l-5.24 2.78 1.01-5.87L1.5 7.65l5.9-.85z"
            />
          </svg>
          <span class="font-display text-lg font-semibold text-child-text">Bra jobbat!</span>
        </div>
      </div>
    }
  `,
})
export class ChildCelebration {
  protected readonly visible = signal(false);
  protected readonly pieces = signal<ConfettiPiece[]>([]);
  private hideTimeout?: ReturnType<typeof setTimeout>;

  play(): void {
    clearTimeout(this.hideTimeout);
    this.pieces.set(randomPieces());
    this.visible.set(true);
    this.hideTimeout = setTimeout(() => this.visible.set(false), VISIBLE_MS);
  }
}
