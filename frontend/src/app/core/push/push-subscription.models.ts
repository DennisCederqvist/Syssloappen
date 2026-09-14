export interface PushPublicKeyResponse {
  publicKey: string;
}

export interface SubscribeToPushRequest {
  endpoint: string;
  p256dh: string;
  auth: string;
}

export interface UnsubscribeFromPushRequest {
  endpoint: string;
}
