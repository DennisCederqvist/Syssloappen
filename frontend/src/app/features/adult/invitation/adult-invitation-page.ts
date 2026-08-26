import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { HouseholdInvitation } from '../../../core/auth/auth.models';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';

@Component({
  selector: 'app-adult-invitation-page',
  imports: [AppBottomNav, DatePipe, RouterLink, UserHeader],
  templateUrl: './adult-invitation-page.html',
})
export class AdultInvitationPage {
  private readonly auth = inject(AuthService);
  readonly invitation = signal<HouseholdInvitation | null>(null);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly copied = signal(false);
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];

  createInvitation(): void {
    this.isSubmitting.set(true);
    this.errorMessage.set('');
    this.copied.set(false);
    this.auth.createHouseholdInvitation().pipe(finalize(() => this.isSubmitting.set(false))).subscribe({
      next: (invitation) => this.invitation.set(invitation),
      error: () => this.errorMessage.set('Inbjudningskoden kunde inte skapas. Försök igen.'),
    });
  }

  async copyCode(): Promise<void> {
    const code = this.invitation()?.code;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.copied.set(true);
    } catch {
      this.errorMessage.set('Koden kunde inte kopieras automatiskt. Kopiera den manuellt.');
    }
  }
}