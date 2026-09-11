import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ChildCardMotion } from './ui/card-motion';
import { CHILD_CARD_PALETTES, ChildCardPalette } from './ui/palette';
import { ChildPageHeader } from './ui/page-header';
import { ChildRedemptionCard } from './ui/redemption-card';
import { ChildSideNav } from './ui/side-nav';
import { RewardRedemption } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

// 2-column grid reads much better filled out than the previous 5, which
// always left an awkward lone card on its own row.
const RECENT_FINAL_ITEMS_LIMIT = 6;

@Component({
  selector: 'app-child-redemptions-page',
  imports: [ChildSideNav, ChildPageHeader, ChildRedemptionCard, TranslocoPipe],
  templateUrl: './child-redemptions-page.html',
})
export class ChildRedemptionsPage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly service = inject(ChildChoresService);
  private readonly transloco = inject(TranslocoService);
  private readonly motion = new ChildCardMotion(() =>
    [...this.activeItems(), ...this.recentFinalItems()].map((item) => item.id),
  );

  readonly childName = computed(() => {
    this.transloco.activeLang();
    return this.auth.user()?.name || this.transloco.translate('child.common.fallbackName');
  });
  readonly wobblingRedemptionId = this.motion.wobblingId;
  readonly items = signal<RewardRedemption[]>([]);
  readonly availablePoints = signal(0);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly activeItems = computed(() =>
    this.items().filter((item) => item.status === 'Requested' || item.status === 'Approved'),
  );
  readonly recentFinalItems = computed(() =>
    this.items()
      .filter((item) => item.status === 'Cancelled' || item.status === 'Delivered')
      .slice(0, RECENT_FINAL_ITEMS_LIMIT),
  );

  ngOnInit(): void {
    this.load();
    this.motion.start();
  }

  ngOnDestroy(): void {
    this.motion.stop();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      redemptions: this.service.getRewardRedemptions(),
      rewards: this.service.getRewards(),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ redemptions, rewards }) => {
          this.items.set(redemptions);
          this.availablePoints.set(rewards.availablePoints);
        },
        error: () => this.error.set(this.transloco.translate('child.redemptions.loadError')),
      });
  }

  paletteFor(index: number): ChildCardPalette {
    return CHILD_CARD_PALETTES[index % CHILD_CARD_PALETTES.length];
  }

  tiltFor(redemptionId: number): number {
    return this.motion.tiltFor(redemptionId);
  }
}
