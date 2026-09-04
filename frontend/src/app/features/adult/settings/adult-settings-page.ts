import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultPageHeader } from '../ui/page-header';

@Component({
  selector: 'app-adult-settings-page',
  imports: [RouterLink, AdultBottomNav, AdultPageHeader],
  templateUrl: './adult-settings-page.html',
})
export class AdultSettingsPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }
}
