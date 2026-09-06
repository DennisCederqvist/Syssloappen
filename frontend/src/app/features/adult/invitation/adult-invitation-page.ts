import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { HouseholdInvitation } from '../../../core/auth/auth.models';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultPrimaryButton, AdultSecondaryTintButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { HouseholdAdultsService } from '../household/household-adults.service';
import { HouseholdAdult } from '../household/household.models';

@Component({
  selector: 'app-adult-invitation-page',
  imports: [
    DatePipe,
    RouterLink,
    AdultBottomNav,
    AdultPrimaryButton,
    AdultSecondaryTintButton,
    AdultPageHeader,
  ],
  templateUrl: './adult-invitation-page.html',
})
export class AdultInvitationPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly householdAdults = inject(HouseholdAdultsService);
  readonly invitation = signal<HouseholdInvitation | null>(null);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly copied = signal(false);
  readonly adults = signal<HouseholdAdult[]>([]);
  readonly isLoadingAdults = signal(true);

  ngOnInit(): void {
    this.householdAdults
      .list()
      .pipe(finalize(() => this.isLoadingAdults.set(false)))
      .subscribe({
        next: (adults) => this.adults.set(adults),
        error: () => this.isLoadingAdults.set(false),
      });
  }

  createInvitation(): void {
    this.isSubmitting.set(true);
    this.errorMessage.set('');
    this.copied.set(false);
    this.auth
      .createHouseholdInvitation()
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
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
