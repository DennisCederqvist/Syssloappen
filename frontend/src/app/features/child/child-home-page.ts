import { Component } from '@angular/core';
import { AppBottomNav, NavItem } from '../../shared/app-bottom-nav';
import { UserHeader } from '../../shared/user-header';

@Component({
  selector: 'app-child-home-page',
  imports: [AppBottomNav, UserHeader],
  templateUrl: './child-home-page.html',
})
export class ChildHomePage {
  readonly navItems: NavItem[] = [
    { label: 'Idag', icon: '⌂', active: true },
    { label: 'Poäng', icon: '★' },
    { label: 'Profil', icon: '☺' },
  ];
}
