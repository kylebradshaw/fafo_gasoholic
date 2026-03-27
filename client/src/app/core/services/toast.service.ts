import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private toastsSignal = signal<Toast[]>([]);
  toasts = this.toastsSignal.asReadonly();
  private idCounter = 0;

  show(message: string, type: 'success' | 'error' | 'info' = 'info', duration = 5000) {
    const id = String(this.idCounter++);
    const toast: Toast = { id, message, type, duration };
    this.toastsSignal.update(t => [...t, toast]);

    if (duration) {
      setTimeout(() => this.remove(id), duration);
    }

    return id;
  }

  remove(id: string) {
    this.toastsSignal.update(t => t.filter(toast => toast.id !== id));
  }

  clear() {
    this.toastsSignal.set([]);
  }
}
