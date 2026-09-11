import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ChildCardMotion } from './ui/card-motion';
import { CHILD_CARD_PALETTES, ChildCardPalette } from './ui/palette';
import { ChildPageHeader } from './ui/page-header';
import { ChildRewardCard } from './ui/reward-card';
import { ChildSideNav } from './ui/side-nav';
import { ChildReward } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({
  selector: 'app-child-rewards-page',
  imports: [ChildSideNav, ChildPageHeader, ChildRewardCard, TranslocoPipe],
  templateUrl: './child-rewards-page.html',
})
export class ChildRewardsPage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly service = inject(ChildChoresService);
  private readonly transloco = inject(TranslocoService);
  private readonly motion = new ChildCardMotion(() => this.rewards().map((reward) => reward.id));

  readonly childName = computed(() => {
    this.transloco.activeLang();
    return this.auth.user()?.name || this.transloco.translate('child.common.fallbackName');
  });
  readonly wobblingRewardId = this.motion.wobblingId;
  readonly rewards = signal<ChildReward[]>([]);
  readonly availablePoints = signal(0);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly busyId = signal<number | null>(null);

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
    this.service
      .getRewards()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.rewards.set(result.rewards);
          this.availablePoints.set(result.availablePoints);
        },
        error: () => this.error.set(this.transloco.translate('child.rewards.loadError')),
      });
  }

  request(reward: ChildReward): void {
    if (this.busyId() !== null) return;
    this.busyId.set(reward.id);
    this.error.set('');
    this.service
      .requestReward(reward.id, crypto.randomUUID())
      .pipe(finalize(() => this.busyId.set(null)))
      .subscribe({
        next: (redemption) => {
          this.availablePoints.set(redemption.availablePoints);
          this.rewards.update((items) => items.filter((item) => item.id !== reward.id));
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(
            this.transloco.translate(
              error.status === 409
                ? 'child.rewards.requestErrorConflict'
                : 'child.rewards.requestErrorGeneric',
            ),
          ),
      });
  }

  canAfford(reward: ChildReward): boolean {
    return this.availablePoints() >= reward.pointsCost;
  }

  paletteFor(index: number): ChildCardPalette {
    return CHILD_CARD_PALETTES[index % CHILD_CARD_PALETTES.length];
  }

  tiltFor(rewardId: number): number {
    return this.motion.tiltFor(rewardId);
  }
}
