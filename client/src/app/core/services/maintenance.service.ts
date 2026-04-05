import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface MaintenanceRecord {
  id: number;
  type: string;
  performedAt: string;
  odometer: number;
  cost: number;
  notes?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class MaintenanceService {
  private http = inject(HttpClient);

  private recordsSignal = signal<MaintenanceRecord[]>([]);
  private loadingSignal = signal(false);

  records = this.recordsSignal.asReadonly();
  loading = this.loadingSignal.asReadonly();

  async loadRecords(autoId: number): Promise<void> {
    this.loadingSignal.set(true);
    try {
      const records = await firstValueFrom(
        this.http.get<MaintenanceRecord[]>(`/api/autos/${autoId}/maintenance`, { withCredentials: true })
      );
      this.recordsSignal.set(records);
    } finally {
      this.loadingSignal.set(false);
    }
  }

  async createRecord(autoId: number, data: Omit<MaintenanceRecord, 'id'>): Promise<void> {
    await firstValueFrom(
      this.http.post<{ id: number }>(`/api/autos/${autoId}/maintenance`, data, { withCredentials: true })
    );
    await this.loadRecords(autoId);
  }

  async updateRecord(autoId: number, id: number, data: Omit<MaintenanceRecord, 'id'>): Promise<void> {
    await firstValueFrom(
      this.http.put<{ id: number }>(`/api/autos/${autoId}/maintenance/${id}`, data, { withCredentials: true })
    );
    await this.loadRecords(autoId);
  }

  async deleteRecord(autoId: number, id: number): Promise<void> {
    await firstValueFrom(
      this.http.delete(`/api/autos/${autoId}/maintenance/${id}`, { withCredentials: true })
    );
    this.recordsSignal.update(records => records.filter(r => r.id !== id));
  }
}
