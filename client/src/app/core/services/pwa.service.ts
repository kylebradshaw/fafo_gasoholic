import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { ToastService } from './toast.service';

@Injectable({
  providedIn: 'root'
})
export class PwaService {
  private swUpdate = inject(SwUpdate);
  private toastService = inject(ToastService);

  updateAvailable = signal(false);

  constructor() {
    if (this.swUpdate.isEnabled) {
      // Check for updates every hour
      setInterval(() => this.checkForUpdates(), 3600000);
      // Also check on startup
      this.checkForUpdates();
    }
  }

  private checkForUpdates() {
    this.swUpdate.checkForUpdate().then(
      (updateAvailable) => {
        if (updateAvailable) {
          this.updateAvailable.set(true);
          this.toastService.show('New version available. Refresh to update.', 'info', 0);
        }
      },
      (err) => console.error('Error checking for updates:', err)
    );
  }

  applyUpdate() {
    this.swUpdate.activateUpdate().then(() => {
      window.location.reload();
    });
  }
}
