import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import {
  PushPublicKeyResponse,
  SubscribeToPushRequest,
  UnsubscribeFromPushRequest,
} from './push-subscription.models';

/**
 * Wraps Angular's SwPush around the backend's /api/push-subscriptions endpoints. Only
 * functional in the production service-worker build (SwPush.isEnabled is false under
 * `ng serve` and in any browser without push support), same as the rest of the PWA setup.
 */
@Injectable({ providedIn: 'root' })
export class PushNotificationsService {
  private readonly http = inject(HttpClient);
  private readonly swPush = inject(SwPush);

  readonly isSupported = this.swPush.isEnabled;
  readonly isSubscribed = signal(false);

  constructor() {
    if (this.isSupported) {
      this.swPush.subscription.subscribe((subscription) =>
        this.isSubscribed.set(subscription !== null),
      );
    }
  }

  async enable(): Promise<void> {
    if (!this.isSupported) return;

    const { publicKey } = await firstValueFrom(
      this.http.get<PushPublicKeyResponse>('/api/push-subscriptions/public-key'),
    );
    const subscription = await this.swPush.requestSubscription({ serverPublicKey: publicKey });
    const json = subscription.toJSON();

    await firstValueFrom(
      this.http.post<void>('/api/push-subscriptions', {
        endpoint: json.endpoint ?? '',
        p256dh: json.keys?.['p256dh'] ?? '',
        auth: json.keys?.['auth'] ?? '',
      } satisfies SubscribeToPushRequest),
    );
  }

  async disable(): Promise<void> {
    if (!this.isSupported) return;

    const subscription = await firstValueFrom(this.swPush.subscription);
    if (subscription) {
      await firstValueFrom(
        this.http.request('DELETE', '/api/push-subscriptions', {
          body: { endpoint: subscription.endpoint } satisfies UnsubscribeFromPushRequest,
        }),
      );
    }

    await this.swPush.unsubscribe();
  }
}
