import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { AdultAssignment, Chore } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import {
  AdultRewardRedemption,
  RewardRedemptionsService,
} from '../rewards/reward-redemptions.service';
import { ChildSummary } from './children.models';
import { ChildrenService } from './children.service';

@Component({
  standalone: true,
  selector: 'app-adult-child-profile-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './adult-child-profile-page.html',
})
export class AdultChildProfilePage {
  private readonly route = inject(ActivatedRoute);
  private readonly childrenService = inject(ChildrenService);
  private readonly choresService = inject(ChoresService);
  private readonly rewardRedemptionsService = inject(RewardRedemptionsService);
  private readonly childId = Number(this.route.snapshot.paramMap.get('childId'));
  readonly child = signal<ChildSummary | null>(null);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly chores = signal<Chore[]>([]);
  readonly rewardRedemptions = signal<AdultRewardRedemption[]>([]);
  readonly error = signal('');
  readonly showAssignmentPicker = signal(false);
  readonly busyAssignmentId = signal<number | null>(null);
  readonly busyRewardRedemptionId = signal<number | null>(null);
  readonly assigningChoreId = signal<number | null>(null);
  readonly activeAssignments = computed(() =>
    this.assignments().filter((item) => item.status === 'Assigned' || item.status === 'NeedsRedo'),
  );
  readonly pendingAssignments = computed(() =>
    this.assignments().filter((item) => item.status === 'PendingApproval'),
  );
  readonly recentApprovedAssignments = computed(() =>
    this.assignments()
      .filter((item) => item.status === 'Approved')
      .slice(0, 10),
  );
  readonly activeRewardRedemptions = computed(() =>
    this.rewardRedemptions().filter(
      (item) => item.status === 'Requested' || item.status === 'Approved',
    ),
  );
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', active: true, route: '/vuxen/barn' },
    { label: 'Önskningar', icon: '★', route: '/vuxen/onskningar' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];

  constructor() {
    this.load();
  }
  toggleAssignmentPicker(): void {
    this.showAssignmentPicker.update((value) => !value);
  }
  approve(item: AdultAssignment): void {
    this.review(item, 'approve');
  }
  needsRedo(item: AdultAssignment): void {
    this.review(item, 'reject');
  }
  changeRewardRedemption(
    item: AdultRewardRedemption,
    action: 'approve' | 'cancel' | 'deliver',
  ): void {
    if (this.busyRewardRedemptionId() !== null) return;
    this.busyRewardRedemptionId.set(item.id);
    this.rewardRedemptionsService.change(item.id, action, null).subscribe({
      next: (updated) =>
        this.rewardRedemptions.update((items) =>
          items.map((current) => (current.id === updated.id ? updated : current)),
        ),
      error: () => this.error.set('Belöningsönskan kunde inte hanteras. Försök igen.'),
      complete: () => this.busyRewardRedemptionId.set(null),
    });
  }

  assign(chore: Chore): void {
    if (this.assigningChoreId() !== null) return;
    this.assigningChoreId.set(chore.id);
    this.choresService
      .createAssignment({
        choreId: chore.id,
        childId: this.childId,
        dueDate: new Date().toLocaleDateString('sv-SE'),
      })
      .subscribe({
        next: () => {
          this.showAssignmentPicker.set(false);
          this.load();
        },
        error: () => {
          this.error.set('Sysslan kunde inte tilldelas. Försök igen.');
          this.assigningChoreId.set(null);
        },
      });
  }

  private load(): void {
    forkJoin({
      children: this.childrenService.getActiveChildren(),
      assignments: this.choresService.getAssignments(),
      chores: this.choresService.getChores(),
      rewardRedemptions: this.rewardRedemptionsService.get(),
    }).subscribe({
      next: ({ children, assignments, chores, rewardRedemptions }) => {
        const child = children.find((item) => item.id === this.childId) ?? null;
        this.child.set(child);
        this.assignments.set(assignments.filter((item) => item.childId === this.childId));
        this.chores.set(chores);
        this.rewardRedemptions.set(
          rewardRedemptions.filter((item) => item.childId === this.childId),
        );
        this.assigningChoreId.set(null);
        if (!child) this.error.set('Barnet kunde inte hittas i din familj.');
      },
      error: () => this.error.set('Barnets översikt kunde inte hämtas. Försök igen.'),
    });
  }

  private review(item: AdultAssignment, decision: 'approve' | 'reject'): void {
    if (this.busyAssignmentId() !== null) return;
    this.busyAssignmentId.set(item.assignmentId);
    const request =
      decision === 'approve'
        ? this.choresService.approveAssignment(item.assignmentId, { comment: null })
        : this.choresService.rejectAssignment(item.assignmentId, { comment: null });
    request.subscribe({
      next: (reviewed) =>
        this.assignments.update((items) =>
          items.map((current) =>
            current.assignmentId === reviewed.assignmentId
              ? { ...current, status: reviewed.status, reviewedAt: reviewed.reviewedAt }
              : current,
          ),
        ),
      error: () => this.error.set('Granskningen kunde inte sparas. Försök igen.'),
      complete: () => this.busyAssignmentId.set(null),
    });
  }
}
