import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class PushService {
  private http = inject(HttpClient);
  private swRegistration: ServiceWorkerRegistration | null = null;
  pushSupported = signal(false);
  pushEnabled = signal(false);

  async init(): Promise<void> {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
      this.pushSupported.set(false);
      return;
    }

    this.pushSupported.set(true);

    try {
      this.swRegistration = await navigator.serviceWorker.ready;
      const subscription = await this.swRegistration.pushManager.getSubscription();
      this.pushEnabled.set(!!subscription);
    } catch (err) {
      console.error('Failed to check push subscription:', err);
    }
  }

  async subscribe(): Promise<void> {
    if (!this.swRegistration) return;

    try {
      const subscription = await this.swRegistration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: this.urlBase64ToUint8Array(environment.vapidPublicKey) as BufferSource
      });

      // Send subscription to backend
      await firstValueFrom(
        this.http.post('/api/push/subscribe', subscription.toJSON(), { withCredentials: true })
      );

      this.pushEnabled.set(true);
    } catch (err) {
      console.error('Failed to subscribe to push notifications:', err);
      throw err;
    }
  }

  async unsubscribe(): Promise<void> {
    if (!this.swRegistration) return;

    try {
      const subscription = await this.swRegistration.pushManager.getSubscription();
      if (subscription) {
        await subscription.unsubscribe();
        this.pushEnabled.set(false);
      }
    } catch (err) {
      console.error('Failed to unsubscribe from push notifications:', err);
      throw err;
    }
  }

  private urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding)
      .replace(/\-/g, '+')
      .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
  }
}
