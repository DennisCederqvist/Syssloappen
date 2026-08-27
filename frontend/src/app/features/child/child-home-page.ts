import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';
import { focusAfterRender } from '../../shared/focus';
import { ChildChoreAssignment, ChildChoreStatus, ChildReward } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({
  selector: 'app-child-home-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './child-home-page.html',
})
export class ChildHomePage implements OnInit {
  private readonly childChoresService = inject(ChildChoresService);

  readonly assignments = signal<ChildChoreAssignment[]>([]);
  readonly totalPoints = signal(0);
  readonly availablePoints = signal(0);
  readonly rewards = signal<ChildReward[]>([]);
  readonly redeemingIds = signal<ReadonlySet<number>>(new Set());
  readonly rewardError = signal('');
  readonly rewardSuccess = signal('');
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly submittingAssignmentIds = signal<ReadonlySet<number>>(new Set());
  readonly submissionErrors = signal<Readonly<Record<number, string>>>({});

  readonly navItems: NavItem[] = [
    { label: 'Idag', icon: '⌂', active: true, route: '/barn' },
    { label: 'Poäng', icon: '★' },
    { label: 'Profil', icon: '☺' },
  ];

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    forkJoin({
      assignments: this.childChoresService.getAssignments(),
      points: this.childChoresService.getPoints(),
      rewards: this.childChoresService.getRewards(),
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ assignments, points, rewards }) => {
          this.assignments.set(assignments);
          this.totalPoints.set(points.totalPoints);
          this.availablePoints.set(rewards.availablePoints);
          this.rewards.set(rewards.rewards);
        },
        error: () => this.loadError.set('Dina sysslor och poäng kunde inte hämtas. Försök igen.'),
      });
  }

  requestReward(reward: ChildReward): void {
    if (this.redeemingIds().has(reward.id)) return;
    this.redeemingIds.update((ids) => new Set(ids).add(reward.id)); this.rewardError.set(''); this.rewardSuccess.set('');
    this.childChoresService.requestReward(reward.id, crypto.randomUUID()).pipe(finalize(() => this.redeemingIds.update((ids) => { const next = new Set(ids); next.delete(reward.id); return next; }))).subscribe({
      next: (redemption) => { this.availablePoints.set(redemption.availablePoints); this.rewardSuccess.set(`${reward.name} är önskad. En vuxen hjälper dig snart.`); focusAfterRender('child-reward-success'); },
      error: (error: HttpErrorResponse) => this.rewardError.set(error.status === 409 ? 'Du har inte tillräckligt många tillgängliga poäng.' : error.status === 404 ? 'Belöningen finns inte längre.' : 'Belöningen kunde inte önskas. Försök igen.'),
    });
  }

  isRedeeming(rewardId: number): boolean { return this.redeemingIds().has(rewardId); }

  submitAssignment(assignment: ChildChoreAssignment): void {
    if (!this.canSubmit(assignment) || this.isSubmitting(assignment.assignmentId)) return;

    this.submittingAssignmentIds.update((ids) => new Set(ids).add(assignment.assignmentId));
    this.submissionErrors.update(({ [assignment.assignmentId]: _, ...errors }) => errors);

    this.childChoresService
      .submitAssignment(assignment.assignmentId)
      .pipe(
        finalize(() =>
          this.submittingAssignmentIds.update((ids) => {
            const next = new Set(ids);
            next.delete(assignment.assignmentId);
            return next;
          }),
        ),
      )
      .subscribe({
        next: (submitted) => {
          this.assignments.update((assignments) =>
            assignments.map((item) =>
              item.assignmentId === submitted.assignmentId
                ? {
                    ...item,
                    status: submitted.status,
                    submittedAt: submitted.submittedAt,
                    reviewComment: null,
                  }
                : item,
            ),
          );
          focusAfterRender(`child-assignment-${assignment.assignmentId}`);
        },
        error: (error: HttpErrorResponse) => {
          const message =
            error.status === 404
              ? 'Sysslan finns inte längre i din lista.'
              : error.status === 409
                ? 'Sysslan har redan ändrats. Uppdatera listan och försök igen.'
                : 'Sysslan kunde inte rapporteras. Försök igen.';
          this.submissionErrors.update((errors) => ({
            ...errors,
            [assignment.assignmentId]: message,
          }));
        },
      });
  }

  canSubmit(assignment: ChildChoreAssignment): boolean {
    return assignment.status === 'Assigned' || assignment.status === 'NeedsRedo';
  }

  isSubmitting(assignmentId: number): boolean {
    return this.submittingAssignmentIds().has(assignmentId);
  }

  statusLabel(status: ChildChoreStatus): string {
    switch (status) {
      case 'Assigned':
        return 'Att göra';
      case 'PendingApproval':
        return 'Väntar på godkännande';
      case 'NeedsRedo':
        return 'Behöver göras om';
      case 'Approved':
        return 'Godkänd';
    }
  }
}
