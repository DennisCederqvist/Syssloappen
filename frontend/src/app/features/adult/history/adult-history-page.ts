import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultDangerOutlineButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultAssignment } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import { AdultRewardRedemption, RewardRedemptionsService } from '../rewards/reward-redemptions.service';

type HistoryItem = {
  id: string;
  kind: 'chore' | 'reward';
  rawId: number;
  title: string;
  childName: string;
  detail: string;
  status: string;
  occurredAt: string;
  hidden: boolean;
};

@Component({
  selector: 'app-adult-history-page',
  imports: [DatePipe, AdultBottomNav, AdultDangerOutlineButton, AdultPageHeader],
  templateUrl: './adult-history-page.html',
})
export class AdultHistoryPage implements OnInit {
  private readonly chores = inject(ChoresService);
  private readonly redemptions = inject(RewardRedemptionsService);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly rewards = signal<AdultRewardRedemption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly showHidden = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly actionError = signal('');
  readonly items = computed(() => {
    const chores = this.assignments().flatMap((item): HistoryItem[] =>
      item.status === 'Approved' && item.reviewedAt
        ? [
            {
              id: `chore-${item.assignmentId}`,
              kind: 'chore',
              rawId: item.assignmentId,
              title: item.choreTitle,
              childName: item.childName,
              detail: `${item.points} poäng`,
              status: 'Godkänd syssla',
              occurredAt: item.reviewedAt,
              hidden: !!item.adultArchivedAt,
            },
          ]
        : item.status === 'Cancelled' && item.cancelledAt
          ? [
              {
                id: `chore-${item.assignmentId}`,
                kind: 'chore',
                rawId: item.assignmentId,
                title: item.choreTitle,
                childName: item.childName,
                detail: 'Tilldelningen togs bort',
                status: 'Avbruten syssla',
                occurredAt: item.cancelledAt,
                hidden: !!item.adultArchivedAt,
              },
            ]
          : [],
    );
    const rewards = this.rewards().flatMap((item): HistoryItem[] =>
      item.status === 'Delivered' && item.deliveredAt
        ? [
            {
              id: `reward-${item.id}`,
              kind: 'reward',
              rawId: item.id,
              title: item.rewardName,
              childName: item.childName,
              detail: `${item.pointsCost} poäng användes`,
              status: 'Utlämnad belöning',
              occurredAt: item.deliveredAt,
              hidden: !!item.adultArchivedAt,
            },
          ]
        : item.status === 'Cancelled' && item.reviewedAt
          ? [
              {
                id: `reward-${item.id}`,
                kind: 'reward',
                rawId: item.id,
                title: item.rewardName,
                childName: item.childName,
                detail: 'Önskan fick avslag',
                status: 'Avslagen belöning',
                occurredAt: item.reviewedAt,
                hidden: !!item.adultArchivedAt,
              },
            ]
          : [],
    );
    return [...chores, ...rewards].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  });
  readonly visibleItems = computed(() => this.items().filter((item) => this.showHidden() || !item.hidden));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({ assignments: this.chores.getAssignments(true), rewards: this.redemptions.get() }).subscribe({
      next: ({ assignments, rewards }) => {
        this.assignments.set(assignments);
        this.rewards.set(rewards);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Historiken kunde inte hämtas. Försök igen.');
        this.loading.set(false);
      },
    });
  }

  hide(item: HistoryItem): void {
    if (this.busyId() !== null) return;
    this.busyId.set(item.id);
    this.actionError.set('');
    const request =
      item.kind === 'chore'
        ? this.chores.archiveAssignment(item.rawId)
        : this.redemptions.archive(item.rawId);
    request.subscribe({
      next: () => this.setArchived(item, new Date().toISOString()),
      error: () => this.actionError.set('Posten kunde inte döljas. Försök igen.'),
      complete: () => this.busyId.set(null),
    });
  }

  unhide(item: HistoryItem): void {
    if (this.busyId() !== null) return;
    this.busyId.set(item.id);
    this.actionError.set('');
    const request =
      item.kind === 'chore'
        ? this.chores.restoreAssignment(item.rawId)
        : this.redemptions.restore(item.rawId);
    request.subscribe({
      next: () => this.setArchived(item, null),
      error: () => this.actionError.set('Posten kunde inte återställas. Försök igen.'),
      complete: () => this.busyId.set(null),
    });
  }

  private setArchived(item: HistoryItem, archivedAt: string | null): void {
    if (item.kind === 'chore') {
      this.assignments.update((items) =>
        items.map((current) =>
          current.assignmentId === item.rawId ? { ...current, adultArchivedAt: archivedAt } : current,
        ),
      );
    } else {
      this.rewards.update((items) =>
        items.map((current) =>
          current.id === item.rawId ? { ...current, adultArchivedAt: archivedAt } : current,
        ),
      );
    }
  }
}
