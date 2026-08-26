import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';

@Component({
  selector: 'app-adult-home-page',
  imports: [AppBottomNav, RouterLink, UserHeader],
  templateUrl: './adult-home-page.html',
})
export class AdultHomePage {
  private readonly auth = inject(AuthService);
  readonly displayName = computed(() => this.auth.user()?.email?.split('@')[0] || 'familj');
  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', active: true, route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];
}
