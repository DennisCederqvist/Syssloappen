import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';

@Component({
  selector: 'app-adult-home-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './adult-home-page.html',
})
export class AdultHomePage {
  private readonly auth = inject(AuthService);
  readonly displayName = computed(() => this.auth.user()?.email?.split('@')[0] || 'familj');
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', active: true },
    { label: 'Sysslor', icon: '☷' },
    { label: 'Barn', icon: '♧' },
    { label: 'Granska', icon: '✓' },
  ];
}
