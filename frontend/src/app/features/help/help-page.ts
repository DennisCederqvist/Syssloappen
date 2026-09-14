import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AdultPageHeader } from '../adult/ui/page-header';

const FAQ_KEYS = [
  'addAdult',
  'addChild',
  'pairDevice',
  'createChore',
  'assignChore',
  'reviewChore',
  'createReward',
  'redeemReward',
  'points',
] as const;

@Component({
  selector: 'app-help-page',
  imports: [RouterLink, AdultPageHeader, TranslocoPipe],
  templateUrl: './help-page.html',
})
export class HelpPage {
  readonly faqKeys = FAQ_KEYS;
}
