import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Fillup {
  id: number;
  autoId: number;
  filledAt: string;
  fuelType: number;
  pricePerGallon: number;
  gallons: number;
  odometer: number;
  isPartialFill: boolean;
  mpg?: number;
}

@Injectable({
  providedIn: 'root'
})
export class FillupsService {
  private http = inject(HttpClient);

  private fillupsSignal = signal<Fillup[]>([]);
  private loadingSignal = signal(false);

  fillups = this.fillupsSignal.asReadonly();
  loading = this.loadingSignal.asReadonly();

  async loadFillups(autoId: number): Promise<void> {
    this.loadingSignal.set(true);
    try {
      const fillups = await firstValueFrom(
        this.http.get<Fillup[]>(`/api/autos/${autoId}/fillups`, { withCredentials: true })
      );
      this.fillupsSignal.set(fillups);
    } finally {
      this.loadingSignal.set(false);
    }
  }

  async createFillup(autoId: number, fillup: Omit<Fillup, 'id' | 'autoId'>): Promise<Fillup> {
    const created = await firstValueFrom(
      this.http.post<Fillup>(`/api/autos/${autoId}/fillups`, fillup, { withCredentials: true })
    );
    this.fillupsSignal.update(fillups => [created, ...fillups]);
    return created;
  }

  async updateFillup(autoId: number, id: number, fillup: Omit<Fillup, 'id' | 'autoId'>): Promise<Fillup> {
    const updated = await firstValueFrom(
      this.http.put<Fillup>(`/api/autos/${autoId}/fillups/${id}`, fillup, { withCredentials: true })
    );
    this.fillupsSignal.update(fillups =>
      fillups.map(f => f.id === id ? updated : f)
    );
    return updated;
  }

  async deleteFillup(autoId: number, id: number): Promise<void> {
    await firstValueFrom(
      this.http.delete(`/api/autos/${autoId}/fillups/${id}`, { withCredentials: true })
    );
    this.fillupsSignal.update(fillups => fillups.filter(f => f.id !== id));
  }
}
