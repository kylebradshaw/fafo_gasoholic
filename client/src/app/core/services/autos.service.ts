import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Auto {
  id: number;
  brand: string;
  model: string;
  plate: string;
  odometer: number;
  latestFillupOdometer?: number | null;
  latestFillupAt?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AutosService {
  private http = inject(HttpClient);

  private autosSignal = signal<Auto[]>([]);
  private currentAutoIdSignal = signal<number | null>(null);
  private loadingSignal = signal(false);

  autos = this.autosSignal.asReadonly();
  currentAutoId = this.currentAutoIdSignal.asReadonly();
  loading = this.loadingSignal.asReadonly();
  currentAuto = computed(() => {
    const id = this.currentAutoIdSignal();
    return id ? this.autosSignal().find(a => a.id === id) : null;
  });

  async loadAutos(): Promise<void> {
    this.loadingSignal.set(true);
    try {
      const autos = await firstValueFrom(
        this.http.get<Auto[]>('/api/autos', { withCredentials: true })
      );
      this.autosSignal.set(autos);
      // Set current auto to first one, or most recently fueled
      if (autos.length > 0) {
        this.currentAutoIdSignal.set(autos[0].id);
      }
    } finally {
      this.loadingSignal.set(false);
    }
  }

  setCurrentAuto(id: number): void {
    this.currentAutoIdSignal.set(id);
  }

  async createAuto(auto: Omit<Auto, 'id'>): Promise<Auto> {
    const created = await firstValueFrom(
      this.http.post<Auto>('/api/autos', auto, { withCredentials: true })
    );
    this.autosSignal.update(autos => [...autos, created]);
    return created;
  }

  async updateAuto(id: number, auto: Omit<Auto, 'id'>): Promise<Auto> {
    const updated = await firstValueFrom(
      this.http.put<Auto>(`/api/autos/${id}`, auto, { withCredentials: true })
    );
    this.autosSignal.update(autos =>
      autos.map(a => a.id === id ? updated : a)
    );
    return updated;
  }

  async deleteAuto(id: number): Promise<void> {
    await firstValueFrom(
      this.http.delete(`/api/autos/${id}`, { withCredentials: true })
    );
    this.autosSignal.update(autos => autos.filter(a => a.id !== id));
    // Reset current auto if deleted
    if (this.currentAutoIdSignal() === id) {
      const remaining = this.autosSignal();
      this.currentAutoIdSignal.set(remaining.length > 0 ? remaining[0].id : null);
    }
  }
}
