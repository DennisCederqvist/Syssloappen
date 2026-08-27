import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';
import { focusAfterRender } from '../../shared/focus';
import { ChildChoreAssignment, ChildChoreStatus } from './child-chores.models';
import { ChildChoresService } from './child-chores.service';

@Component({
  selector: 'app-child-home-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './child-home-page.html',
})
export class ChildHomePage implements OnInit {
  private readonly childChoresService = inject(ChildChoresService);

  readonly assignments = signal<ChildChoreAssignment[]>([]);
  readonly availablePoints = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly submittingAssignmentIds = signal<ReadonlySet<number>>(new Set());
  readonly submissionErrors = signal<Readonly<Record<number, string>>>({});

  readonly navItems: NavItem[] = [
    { label: 'Idag', icon: '⌂', active: true, route: '/barn' },
    { label: 'Belöningar', icon: '★', route: '/barn/beloningar' },
    { label: 'Önskningar', icon: '♡', route: '/barn/onskningar' },
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
      rewards: this.childChoresService.getRewards(),
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ assignments, rewards }) => {
          this.assignments.set(assignments);
          this.availablePoints.set(rewards.availablePoints);
        },
        error: () => this.loadError.set('Dina sysslor och poäng kunde inte hämtas. Försök igen.'),
      });
  }

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
