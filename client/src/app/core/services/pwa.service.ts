import { Injectable, inject, signal } from '@angular/core';
import { ToastService } from './toast.service';

// TODO: Install @angular/service-worker and uncomment SwUpdate import
// import { SwUpdate } from '@angular/service-worker';

@Injectable({
  providedIn: 'root'
})
export class PwaService {
  // private swUpdate = inject(SwUpdate);
  private toastService = inject(ToastService);

  updateAvailable = signal(false);

  constructor() {
    // PWA update checking will be enabled after @angular/service-worker is installed
    // if (this.swUpdate.isEnabled) {
    //   setInterval(() => this.checkForUpdates(), 3600000);
    //   this.checkForUpdates();
    // }
  }

  private checkForUpdates() {
    // TODO: Enable when SwUpdate is available
    // this.swUpdate.checkForUpdate().then(
    //   (updateAvailable) => {
    //     if (updateAvailable) {
    //       this.updateAvailable.set(true);
    //       this.toastService.show('New version available. Refresh to update.', 'info', 0);
    //     }
    //   },
    //   (err) => console.error('Error checking for updates:', err)
    // );
  }

  applyUpdate() {
    // TODO: Enable when SwUpdate is available
    // this.swUpdate.activateUpdate().then(() => {
    //   window.location.reload();
    // });
  }
}
