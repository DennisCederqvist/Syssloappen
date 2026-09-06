import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { focusAfterRender } from '../../../shared/focus';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultDangerOutlineButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { HouseholdAdultsService } from './household-adults.service';
import { HouseholdAdult } from './household.models';

@Component({
  selector: 'app-adult-manage-adults-page',
  imports: [RouterLink, AdultBottomNav, AdultDangerOutlineButton, AdultPageHeader],
  templateUrl: './adult-manage-adults-page.html',
})
export class AdultManageAdultsPage implements OnInit {
  private readonly householdAdults = inject(HouseholdAdultsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly adults = signal<HouseholdAdult[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly confirmingId = signal<string | null>(null);
  readonly disconnectingId = signal<string | null>(null);
  readonly disconnectError = signal('');

  readonly ownUserId = this.auth.user()?.userId ?? null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.householdAdults
      .list()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (adults) => this.adults.set(adults),
        error: () => this.loadError.set('De vuxna kunde inte hämtas. Försök igen.'),
      });
  }

  requestDisconnect(adultId: string): void {
    this.confirmingId.set(adultId);
    this.disconnectError.set('');
    focusAfterRender(`cancel-disconnect-${adultId}`);
  }

  cancelDisconnect(adultId: string): void {
    this.confirmingId.set(null);
    focusAfterRender(`request-disconnect-${adultId}`);
  }

  disconnect(adult: HouseholdAdult): void {
    if (this.disconnectingId()) return;

    this.disconnectingId.set(adult.id);
    this.disconnectError.set('');
    const isSelf = adult.id === this.ownUserId;

    this.householdAdults
      .disconnect(adult.id)
      .pipe(finalize(() => this.disconnectingId.set(null)))
      .subscribe({
        next: () => {
          if (isSelf) {
            this.auth.forgetSession();
            this.router.navigateByUrl('/login');
            return;
          }
          this.adults.update((adults) => adults.filter((current) => current.id !== adult.id));
          this.confirmingId.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.disconnectError.set(
            error.status === 404
              ? 'Den vuxna finns inte längre i familjen. Uppdatera listan och försök igen.'
              : error.status === 409
                ? 'Den här personen kan inte kopplas bort just nu.'
                : 'Kunde inte koppla bort. Försök igen om en liten stund.',
          ),
      });
  }
}
