import { Component, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AdultApprovalCard } from './ui/approval-card';
import { AdultBadge } from './ui/badge';
import { AdultBottomNav } from './ui/bottom-nav';
import { AdultPageHeader } from './ui/page-header';
import { AdultPrimaryButton, AdultDangerOutlineButton } from './ui/buttons';
import { AdultTile } from './ui/tile';
import { ChildrenService } from './children/children.service';
import { ChildSummary } from './children/children.models';
import { AdultAssignment } from './chores/chores.models';
import { ChoresService } from './chores/chores.service';
import { AdultRewardRedemption, RewardRedemptionsService } from './rewards/reward-redemptions.service';

interface ChildOverview extends ChildSummary {
  completed: number;
  total: number;
}

@Component({
  selector: 'app-adult-home-page',
  imports: [
    AdultBottomNav,
    AdultPageHeader,
    AdultTile,
    AdultApprovalCard,
    AdultBadge,
    AdultPrimaryButton,
    AdultDangerOutlineButton,
  ],
  templateUrl: './adult-home-page.html',
})
export class AdultHomePage {
  private readonly auth = inject(AuthService);
  private readonly childrenService = inject(ChildrenService);
  private readonly choresService = inject(ChoresService);
  private readonly rewardRedemptionsService = inject(RewardRedemptionsService);
  readonly displayName = computed(() => this.auth.user()?.email?.split('@')[0] || 'familj');
  readonly children = signal<ChildSummary[]>([]);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly rewardRedemptions = signal<AdultRewardRedemption[]>([]);
  readonly busyAssignmentId = signal<number | null>(null);
  readonly busyRewardRedemptionId = signal<number | null>(null);
  readonly rejectingAssignmentId = signal<number | null>(null);
  readonly rejectComments = signal<Readonly<Record<number, string>>>({});
  readonly loadError = signal('');
  readonly childOverviews = computed(() => this.children().map((child) => this.overview(child)));
  readonly pendingAssignments = computed(() =>
    this.assignments().filter((item) => item.status === 'PendingApproval'),
  );
  readonly activeRewardRedemptions = computed(() =>
    this.rewardRedemptions().filter(
      (item) => item.status === 'Requested' || item.status === 'Approved',
    ),
  );

  constructor() {
    forkJoin({
      children: this.childrenService.getActiveChildren(),
      assignments: this.choresService.getAssignments(),
      rewardRedemptions: this.rewardRedemptionsService.get(),
    }).subscribe({
      next: ({ children, assignments, rewardRedemptions }) => {
        this.children.set(children);
        this.assignments.set(assignments);
        this.rewardRedemptions.set(rewardRedemptions);
      },
      error: () => this.loadError.set('Översikten kunde inte hämtas. Försök igen.'),
    });
  }

  approve(item: AdultAssignment): void {
    this.review(item, 'approve', null);
  }

  requestReject(item: AdultAssignment): void {
    if (this.busyAssignmentId() !== null) return;
    this.rejectingAssignmentId.set(item.assignmentId);
  }

  cancelReject(): void {
    this.rejectingAssignmentId.set(null);
  }

  setRejectComment(assignmentId: number, comment: string): void {
    this.rejectComments.update((comments) => ({ ...comments, [assignmentId]: comment }));
  }

  confirmReject(item: AdultAssignment): void {
    const rawComment = this.rejectComments()[item.assignmentId] ?? '';
    this.review(item, 'reject', rawComment.trim() || null);
  }

  changeRewardRedemption(item: AdultRewardRedemption, action: 'approve' | 'cancel' | 'deliver'): void {
    if (this.busyRewardRedemptionId() !== null) return;
    this.busyRewardRedemptionId.set(item.id);
    this.rewardRedemptionsService.change(item.id, action, null).subscribe({
      next: (updated) =>
        this.rewardRedemptions.update((items) =>
          items.map((current) => (current.id === updated.id ? updated : current)),
        ),
      error: () => this.loadError.set('Belöningsönskan kunde inte hanteras. Försök igen.'),
      complete: () => this.busyRewardRedemptionId.set(null),
    });
  }

  private review(item: AdultAssignment, decision: 'approve' | 'reject', comment: string | null): void {
    if (this.busyAssignmentId() !== null) return;
    this.busyAssignmentId.set(item.assignmentId);
    const request =
      decision === 'approve'
        ? this.choresService.approveAssignment(item.assignmentId, { comment })
        : this.choresService.rejectAssignment(item.assignmentId, { comment });
    request.subscribe({
      next: (reviewed) => {
        this.assignments.update((items) =>
          items.map((current) =>
            current.assignmentId === reviewed.assignmentId
              ? { ...current, status: reviewed.status, reviewedAt: reviewed.reviewedAt }
              : current,
          ),
        );
        if (decision === 'reject') {
          this.rejectingAssignmentId.set(null);
          this.rejectComments.update(({ [item.assignmentId]: _, ...rest }) => rest);
        }
      },
      error: () => this.loadError.set('Granskningen kunde inte sparas. Försök igen.'),
      complete: () => this.busyAssignmentId.set(null),
    });
  }

  private overview(child: ChildSummary): ChildOverview {
    const today = new Date().toLocaleDateString('sv-SE');
    const relevant = this.assignments().filter(
      (item) => item.childId === child.id && item.dueDate === today && item.status !== 'Cancelled',
    );
    return {
      ...child,
      completed: relevant.filter((item) => item.status === 'Approved').length,
      total: relevant.length,
    };
  }
}
