import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';
import { ChildrenService } from './children/children.service';
import { ChildSummary } from './children/children.models';
import { AdultAssignment } from './chores/chores.models';
import { ChoresService } from './chores/chores.service';

interface ChildOverview extends ChildSummary { completed: number; total: number; }

@Component({ selector: 'app-adult-home-page', imports: [AppBottomNav, RouterLink, UserHeader], templateUrl: './adult-home-page.html' })
export class AdultHomePage {
  private readonly auth = inject(AuthService);
  private readonly childrenService = inject(ChildrenService);
  private readonly choresService = inject(ChoresService);
  readonly displayName = computed(() => this.auth.user()?.email?.split('@')[0] || 'familj');
  readonly children = signal<ChildSummary[]>([]);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly busyAssignmentId = signal<number | null>(null);
  readonly loadError = signal('');
  readonly childOverviews = computed(() => this.children().map((child) => this.overview(child)));
  readonly pendingAssignments = computed(() => this.assignments().filter((item) => item.status === 'PendingApproval'));
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', active: true, route: '/vuxen' }, { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' }, { label: 'Barn', icon: '♧', route: '/vuxen/barn' }, { label: 'Önskningar', icon: '★', route: '/vuxen/onskningar' }, { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];

  constructor() {
    forkJoin({ children: this.childrenService.getActiveChildren(), assignments: this.choresService.getAssignments() }).subscribe({
      next: ({ children, assignments }) => { this.children.set(children); this.assignments.set(assignments); },
      error: () => this.loadError.set('Översikten kunde inte hämtas. Försök igen.'),
    });
  }

  approve(item: AdultAssignment): void { this.review(item, 'approve'); }
  needsRedo(item: AdultAssignment): void { this.review(item, 'reject'); }

  private review(item: AdultAssignment, decision: 'approve' | 'reject'): void {
    if (this.busyAssignmentId() !== null) return;
    this.busyAssignmentId.set(item.assignmentId);
    const request = decision === 'approve' ? this.choresService.approveAssignment(item.assignmentId, { comment: null }) : this.choresService.rejectAssignment(item.assignmentId, { comment: null });
    request.subscribe({ next: (reviewed) => this.assignments.update((items) => items.map((current) => current.assignmentId === reviewed.assignmentId ? { ...current, status: reviewed.status, reviewedAt: reviewed.reviewedAt } : current)), error: () => this.loadError.set('Granskningen kunde inte sparas. Försök igen.'), complete: () => this.busyAssignmentId.set(null) });
  }

  private overview(child: ChildSummary): ChildOverview {
    const today = new Date().toLocaleDateString('sv-SE');
    const relevant = this.assignments().filter((item) => item.childId === child.id && item.dueDate === today && item.status !== 'Cancelled');
    return { ...child, completed: relevant.filter((item) => item.status === 'Approved').length, total: relevant.length };
  }
}
