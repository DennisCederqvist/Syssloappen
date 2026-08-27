import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { focusAfterRender } from '../../../shared/focus';
import { AdultAssignment, ReviewedAssignment } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';

type ReviewDecision = 'approve' | 'reject';

@Component({
  selector: 'app-adult-review-page',
  imports: [AppBottomNav, DatePipe, UserHeader],
  templateUrl: './adult-review-page.html',
})
export class AdultReviewPage implements OnInit {
  private readonly choresService = inject(ChoresService);
  private archiveUndoTimer: number | null = null;
  private archiveUndoClearTimer: number | null = null;

  readonly assignments = signal<AdultAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly reviewingAssignmentId = signal<number | null>(null);
  readonly reviewComments = signal<Readonly<Record<number, string>>>({});
  readonly reviewErrors = signal<Readonly<Record<number, string>>>({});
  readonly successMessage = signal('');
  readonly historyError = signal('');
  readonly historyBusyId = signal<number | null>(null);
  readonly lastArchivedId = signal<number | null>(null);
  readonly archiveUndoFading = signal(false);
  readonly showHiddenHistory = signal(false);

  readonly pendingAssignments = computed(() =>
    this.assignments().filter((assignment) => assignment.status === 'PendingApproval'),
  );
  readonly needsRedoAssignments = computed(() =>
    this.assignments().filter((assignment) => assignment.status === 'NeedsRedo'),
  );
  readonly reviewedAssignments = computed(() =>
    this.assignments()
      .filter((assignment) => assignment.status === 'Approved' && !assignment.adultArchivedAt)
      .slice(0, 10),
  );
  readonly reviewedHistoryAssignments = computed(() =>
    this.assignments()
      .filter((assignment) => assignment.status === 'Approved' && (!assignment.adultArchivedAt || assignment.assignmentId === this.lastArchivedId()))
      .slice(0, 10),
  );
  readonly hiddenAssignments = computed(() =>
    this.assignments().filter((assignment) => assignment.status === 'Approved' && assignment.adultArchivedAt),
  );

  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', active: true, route: '/vuxen/granska' },
  ];

  ngOnInit(): void {
    this.loadAssignments();
  }

  loadAssignments(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.choresService
      .getAssignments()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (assignments) => this.assignments.set(assignments),
        error: () =>
          this.loadError.set('Familjens rapporterade sysslor kunde inte hämtas. Försök igen.'),
      });
  }

  setComment(assignmentId: number, comment: string): void {
    this.reviewComments.update((comments) => ({ ...comments, [assignmentId]: comment }));
  }

  reviewAssignment(assignment: AdultAssignment, decision: ReviewDecision): void {
    if (assignment.status !== 'PendingApproval' || this.reviewingAssignmentId() !== null) return;

    const rawComment = this.reviewComments()[assignment.assignmentId] ?? '';
    const comment = rawComment.trim() || null;
    if (rawComment.length > 500) {
      this.setReviewError(assignment.assignmentId, 'Kommentaren får innehålla högst 500 tecken.');
      return;
    }

    this.reviewingAssignmentId.set(assignment.assignmentId);
    this.reviewErrors.update(({ [assignment.assignmentId]: _, ...errors }) => errors);
    this.successMessage.set('');

    const request = { comment };
    const reviewRequest =
      decision === 'approve'
        ? this.choresService.approveAssignment(assignment.assignmentId, request)
        : this.choresService.rejectAssignment(assignment.assignmentId, request);

    reviewRequest.pipe(finalize(() => this.reviewingAssignmentId.set(null))).subscribe({
      next: (reviewed) => this.applyReview(assignment, reviewed),
      error: (error: HttpErrorResponse) => {
        const message =
          error.status === 404
            ? 'Tilldelningen finns inte längre i din familj.'
            : error.status === 409
              ? 'Tilldelningen har redan granskats eller ändrats. Uppdatera listan.'
              : error.status === 400
                ? 'Kommentaren är för lång eller innehåller ogiltiga uppgifter.'
                : 'Granskningen kunde inte sparas. Försök igen.';
        this.setReviewError(assignment.assignmentId, message);
      },
    });
  }

  archive(assignment: AdultAssignment): void {
    if (this.historyBusyId() !== null) return;
    this.historyBusyId.set(assignment.assignmentId);
    this.historyError.set('');
    this.choresService.archiveAssignment(assignment.assignmentId)
      .pipe(finalize(() => this.historyBusyId.set(null)))
      .subscribe({
        next: () => {
          this.assignments.update((items) => items.map((item) => item.assignmentId === assignment.assignmentId
            ? { ...item, adultArchivedAt: new Date().toISOString() }
            : item));
          this.showArchiveUndo(assignment.assignmentId);
        },
        error: () => this.historyError.set('Historiken kunde inte döljas. Försök igen.'),
      });
  }

  restore(assignment: AdultAssignment): void {
    if (this.historyBusyId() !== null) return;
    this.historyBusyId.set(assignment.assignmentId);
    this.historyError.set('');
    this.choresService.restoreAssignment(assignment.assignmentId)
      .pipe(finalize(() => this.historyBusyId.set(null)))
      .subscribe({
        next: () => {
          this.assignments.update((items) => items.map((item) => item.assignmentId === assignment.assignmentId
            ? { ...item, adultArchivedAt: null }
            : item));
          this.clearArchiveUndo();
        },
        error: () => this.historyError.set('Historiken kunde inte återställas. Försök igen.'),
      });
  }

  private applyReview(assignment: AdultAssignment, reviewed: ReviewedAssignment): void {
    this.assignments.update((assignments) =>
      assignments.map((item) =>
        item.assignmentId === reviewed.assignmentId
          ? {
              ...item,
              status: reviewed.status,
              reviewedAt: reviewed.reviewedAt,
              reviewComment: reviewed.reviewComment,
            }
          : item,
      ),
    );
    this.reviewComments.update(({ [assignment.assignmentId]: _, ...comments }) => comments);
    this.successMessage.set(
      reviewed.status === 'Approved'
        ? `${assignment.choreTitle} är godkänd och ${assignment.points} poäng har delats ut till ${assignment.childName}.`
        : `${assignment.childName} har fått veta att ${assignment.choreTitle} behöver göras om.`,
    );
    focusAfterRender('review-success-message');
  }

  private setReviewError(assignmentId: number, message: string): void {
    this.reviewErrors.update((errors) => ({ ...errors, [assignmentId]: message }));
  }

  private showArchiveUndo(assignmentId: number): void {
    this.clearArchiveUndo();
    this.lastArchivedId.set(assignmentId);
    this.archiveUndoTimer = window.setTimeout(() => this.archiveUndoFading.set(true), 3000);
    this.archiveUndoClearTimer = window.setTimeout(() => this.clearArchiveUndo(), 5000);
  }

  private clearArchiveUndo(): void {
    if (this.archiveUndoTimer !== null) window.clearTimeout(this.archiveUndoTimer);
    if (this.archiveUndoClearTimer !== null) window.clearTimeout(this.archiveUndoClearTimer);
    this.archiveUndoTimer = null;
    this.archiveUndoClearTimer = null;
    this.archiveUndoFading.set(false);
    this.lastArchivedId.set(null);
  }
}
