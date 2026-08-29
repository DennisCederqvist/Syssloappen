import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppBottomNav } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';

@Component({
  selector: 'app-adult-settings-page',
  imports: [AppBottomNav, RouterLink, UserHeader],
  templateUrl: './adult-settings-page.html',
})
export class AdultSettingsPage {}
