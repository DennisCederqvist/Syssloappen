import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { PushNotificationsService } from '../../../core/push/push-notifications.service';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultLanguageToggle } from '../ui/language-toggle';
import { AdultPageHeader } from '../ui/page-header';

@Component({
  selector: 'app-adult-settings-page',
  imports: [RouterLink, AdultBottomNav, AdultLanguageToggle, AdultPageHeader, TranslocoPipe],
  templateUrl: './adult-settings-page.html',
})
export class AdultSettingsPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly push = inject(PushNotificationsService);
  private readonly transloco = inject(TranslocoService);

  readonly isTogglingPush = signal(false);
  readonly pushError = signal('');

  logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }

  async togglePush(): Promise<void> {
    if (this.isTogglingPush()) return;
    this.isTogglingPush.set(true);
    this.pushError.set('');
    try {
      if (this.push.isSubscribed()) {
        await this.push.disable();
      } else {
        await this.push.enable();
      }
    } catch {
      this.pushError.set(this.transloco.translate('adult.settings.notifications.error'));
    } finally {
      this.isTogglingPush.set(false);
    }
  }
}
