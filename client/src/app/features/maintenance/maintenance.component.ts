import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AutosService } from '../../core/services/autos.service';
import { MaintenanceService, MaintenanceRecord } from '../../core/services/maintenance.service';
import { ToastService } from '../../core/services/toast.service';
import { MaintenanceTypePipe } from '../../core/pipes/maintenance-type.pipe';
import { MaintenanceModalComponent } from './maintenance-modal/maintenance-modal.component';

@Component({
  selector: 'app-maintenance',
  standalone: true,
  imports: [CommonModule, RouterLink, MaintenanceTypePipe, MaintenanceModalComponent],
  template: `
    <div class="maintenance-container">
      @if (autosService.autos().length === 0) {
        <p class="empty-state">Create an <a routerLink="/app/autos">Auto</a> first.</p>
      } @else if (!autosService.currentAuto()) {
        <p class="empty-state">Select a vehicle to view maintenance history.</p>
      } @else {
        <div class="maintenance-header">
          <h2>Maintenance History</h2>
          <button (click)="openAddModal()" class="btn-add">+ Add Record</button>
        </div>

        @if (maintenanceService.records().length === 0) {
          <p class="empty-state">No maintenance records yet. Add one to get started.</p>
        } @else {
          <div class="table-wrap">
            <table class="maintenance-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Type</th>
                  <th>Odometer</th>
                  <th>Cost</th>
                  <th>Notes</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (record of maintenanceService.records(); track record.id) {
                  <tr>
                    <td>{{ record.performedAt | date:'mediumDate' }}</td>
                    <td>{{ record.type | maintenanceType }}</td>
                    <td>{{ record.odometer | number }}</td>
                    <td>\${{ record.cost | number:'1.2-2' }}</td>
                    <td class="notes-cell">{{ record.notes || '' }}</td>
                    <td class="actions-cell">
                      <button (click)="openEditModal(record)" class="btn-secondary">Edit</button>
                      <button (click)="deleteRecord(record.id)" class="btn-danger">Del</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <app-maintenance-modal
          [isOpen]="showModal()"
          [mode]="modalMode()"
          [record]="selectedRecord()"
          (close)="closeModal()"
          (save)="onSave($event)"
        ></app-maintenance-modal>
      }
    </div>
  `,
  styles: [`
    .maintenance-container {
      padding: 0;
    }

    .maintenance-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }

    .maintenance-header h2 {
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-primary);
    }

    .btn-add {
      padding: 0.4rem 0.75rem;
      background: var(--primary-color);
      color: #fff;
      border: none;
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.875rem;
      font-weight: 500;
      transition: opacity 0.15s;
    }

    .btn-add:hover {
      opacity: 0.9;
      filter: brightness(0.9);
    }

    .empty-state {
      text-align: center;
      color: var(--text-secondary);
      padding: 2rem;
    }

    .empty-state a {
      color: var(--primary-color);
      text-decoration: underline;
      cursor: pointer;
      font-weight: 500;
    }

    .empty-state a:hover {
      opacity: 0.8;
    }

    .table-wrap {
      overflow-x: auto;
    }

    .maintenance-table {
      width: 100%;
      border-collapse: collapse;
    }

    .maintenance-table th {
      padding: 0.5rem 0.75rem;
      text-align: left;
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--text-secondary);
      border-bottom: 1px solid var(--border-color);
    }

    .maintenance-table td {
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      border-bottom: 1px solid var(--border-color);
      color: var(--text-primary);
    }

    .maintenance-table tbody tr:hover {
      background: var(--bg-light);
    }

    .notes-cell {
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: var(--text-secondary);
    }

    .actions-cell {
      white-space: nowrap;
    }

    .btn-secondary {
      padding: 0.3rem 0.6rem;
      background: transparent;
      color: var(--text-primary);
      border: 1px solid var(--border-color);
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.8rem;
      margin-right: 0.25rem;
      transition: background 0.15s, border-color 0.3s, color 0.3s;
    }

    .btn-secondary:hover {
      background: var(--bg-light);
    }

    .btn-danger {
      padding: 0.3rem 0.6rem;
      background: transparent;
      color: #dc2626;
      border: 1px solid #dc2626;
      border-radius: 5px;
      cursor: pointer;
      font-size: 0.8rem;
      transition: background 0.15s;
    }

    .btn-danger:hover {
      background: rgba(220, 38, 38, 0.08);
    }

    @media (max-width: 640px) {
      .maintenance-header {
        flex-direction: column;
        gap: 1rem;
        align-items: stretch;
      }

      .btn-add {
        width: 100%;
      }

      .maintenance-table th,
      .maintenance-table td {
        padding: 0.4rem 0.5rem;
        font-size: 0.75rem;
      }
    }
  `]
})
export class MaintenanceComponent {
  autosService = inject(AutosService);
  maintenanceService = inject(MaintenanceService);
  private toastService = inject(ToastService);

  showModal = signal(false);
  modalMode = signal<'add' | 'edit'>('add');
  selectedRecord = signal<MaintenanceRecord | null>(null);

  constructor() {
    effect(async () => {
      const autoId = this.autosService.currentAutoId();
      if (autoId) {
        try {
          await this.maintenanceService.loadRecords(autoId);
        } catch (err) {
          this.toastService.show('Failed to load maintenance records', 'error');
        }
      }
    });
  }

  openAddModal() {
    this.modalMode.set('add');
    this.selectedRecord.set(null);
    this.showModal.set(true);
  }

  openEditModal(record: MaintenanceRecord) {
    this.modalMode.set('edit');
    this.selectedRecord.set(record);
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.selectedRecord.set(null);
  }

  async onSave(data: any) {
    const autoId = this.autosService.currentAutoId();
    if (!autoId) return;

    try {
      if (this.modalMode() === 'add') {
        await this.maintenanceService.createRecord(autoId, data);
        this.toastService.show('Maintenance record added', 'success');
      } else if (this.selectedRecord()) {
        await this.maintenanceService.updateRecord(autoId, this.selectedRecord()!.id, data);
        this.toastService.show('Maintenance record updated', 'success');
      }
      this.closeModal();
    } catch (err) {
      this.toastService.show('Failed to save maintenance record', 'error');
    }
  }

  async deleteRecord(id: number) {
    const autoId = this.autosService.currentAutoId();
    if (!autoId) return;

    if (confirm('Delete this maintenance record?')) {
      try {
        await this.maintenanceService.deleteRecord(autoId, id);
        this.toastService.show('Maintenance record deleted', 'success');
      } catch (err) {
        this.toastService.show('Failed to delete maintenance record', 'error');
      }
    }
  }
}
