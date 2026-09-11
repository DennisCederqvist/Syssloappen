import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';
import { AdultApprovalCard } from '../ui/approval-card';
import { AdultBadge } from '../ui/badge';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultDangerOutlineButton, AdultPrimaryButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultSheet } from '../ui/sheet';
import { AdultAssignment, Chore } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import { AdultRewardRedemption, RewardRedemptionsService } from '../rewards/reward-redemptions.service';
import { ChildSummary } from './children.models';
import { ChildrenService } from './children.service';

@Component({
  selector: 'app-adult-child-profile-page',
  imports: [
    AdultApprovalCard,
    AdultBadge,
    AdultBottomNav,
    AdultDangerOutlineButton,
    AdultPrimaryButton,
    AdultPageHeader,
    AdultSheet,
    TranslocoPipe,
  ],
  templateUrl: './adult-child-profile-page.html',
})
export class AdultChildProfilePage {
  private readonly route = inject(ActivatedRoute);
  private readonly childrenService = inject(ChildrenService);
  private readonly choresService = inject(ChoresService);
  private readonly rewardRedemptionsService = inject(RewardRedemptionsService);
  private readonly transloco = inject(TranslocoService);
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
  readonly rejectingAssignmentId = signal<number | null>(null);
  readonly rejectComments = signal<Readonly<Record<number, string>>>({});
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

  constructor() {
    this.load();
  }

  toggleAssignmentPicker(): void {
    this.showAssignmentPicker.update((value) => !value);
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
      error: () => this.error.set(this.transloco.translate('adult.home.rewardChangeError')),
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
          this.error.set(this.transloco.translate('adult.chores.assignError.generic'));
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
        if (!child) this.error.set(this.transloco.translate('adult.childProfile.notFound'));
      },
      error: () => this.error.set(this.transloco.translate('adult.childProfile.loadError')),
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
      error: () => this.error.set(this.transloco.translate('adult.home.reviewError')),
      complete: () => this.busyAssignmentId.set(null),
    });
  }
}
