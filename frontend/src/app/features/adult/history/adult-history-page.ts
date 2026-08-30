import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AppBottomNav } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { AdultAssignment } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import { AdultRewardRedemption, RewardRedemptionsService } from '../rewards/reward-redemptions.service';

type HistoryItem = { id: string; title: string; childName: string; detail: string; status: string; occurredAt: string; hidden: boolean };

@Component({ selector: 'app-adult-history-page', imports: [AppBottomNav, DatePipe, UserHeader], templateUrl: './adult-history-page.html' })
export class AdultHistoryPage implements OnInit {
  private readonly chores = inject(ChoresService);
  private readonly redemptions = inject(RewardRedemptionsService);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly rewards = signal<AdultRewardRedemption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly showHidden = signal(false);
  readonly items = computed(() => {
    const chores = this.assignments().flatMap((item): HistoryItem[] => item.status === 'Approved' && item.reviewedAt ? [{ id: `chore-${item.assignmentId}`, title: item.choreTitle, childName: item.childName, detail: `${item.points} poäng`, status: 'Godkänd syssla', occurredAt: item.reviewedAt, hidden: !!item.adultArchivedAt }] : item.status === 'Cancelled' && item.cancelledAt ? [{ id: `chore-${item.assignmentId}`, title: item.choreTitle, childName: item.childName, detail: 'Tilldelningen togs bort', status: 'Avbruten syssla', occurredAt: item.cancelledAt, hidden: !!item.adultArchivedAt }] : []);
    const rewards = this.rewards().flatMap((item): HistoryItem[] => item.status === 'Delivered' && item.deliveredAt ? [{ id: `reward-${item.id}`, title: item.rewardName, childName: item.childName, detail: `${item.pointsCost} poäng användes`, status: 'Utlämnad belöning', occurredAt: item.deliveredAt, hidden: !!item.adultArchivedAt }] : item.status === 'Cancelled' && item.reviewedAt ? [{ id: `reward-${item.id}`, title: item.rewardName, childName: item.childName, detail: 'Önskan fick avslag', status: 'Avslagen belöning', occurredAt: item.reviewedAt, hidden: !!item.adultArchivedAt }] : []);
    return [...chores, ...rewards].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  });
  readonly visibleItems = computed(() => this.items().filter((item) => this.showHidden() || !item.hidden));
  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading.set(true); this.error.set('');
    forkJoin({ assignments: this.chores.getAssignments(true), rewards: this.redemptions.get() }).subscribe({
      next: ({ assignments, rewards }) => { this.assignments.set(assignments); this.rewards.set(rewards); this.loading.set(false); },
      error: () => { this.error.set('Historiken kunde inte hämtas. Försök igen.'); this.loading.set(false); },
    });
  }
}
