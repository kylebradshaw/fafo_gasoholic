import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AutosService } from '../../core/services/autos.service';
import { FillupsService, Fillup } from '../../core/services/fillups.service';
import { ToastService } from '../../core/services/toast.service';
import { FuelTypePipe } from '../../core/pipes/fuel-type.pipe';
import { FillupModalComponent } from './fillup-modal/fillup-modal.component';

@Component({
  selector: 'app-fillups',
  standalone: true,
  imports: [CommonModule, RouterLink, FuelTypePipe, FillupModalComponent],
  template: `
    <div class="fillups-container">
      @if (autosService.autos().length === 0) {
        <p class="empty-state">Create an <a routerLink="/app/autos">Auto</a>.</p>
      } @else if (!autosService.currentAuto()) {
        <p class="empty-state">Create an <a routerLink="/app/autos">Auto</a>.</p>
      } @else {
        <div class="fillups-header">
          <h2>Fillup Log</h2>
          <button (click)="openAddModal()" class="btn-add">+ Add Fillup</button>
        </div>

        @if (fillupsService.fillups().length === 0) {
          <p class="empty-state">No fillups recorded yet. Add one to get started.</p>
        } @else {
          <div class="table-wrap">
            <table class="fillups-table">
              <thead>
                <tr>
                  <th>Date/Time</th>
                  <th>Fuel</th>
                  <th>$/gal</th>
                  <th>Gal</th>
                  <th>Odometer</th>
                  <th>MPG</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (fillup of fillupsService.fillups(); track fillup.id) {
                  <tr>
                    <td>{{ fillup.filledAt | date:'short' }}</td>
                    <td>{{ fillup.fuelType | fuelType }}</td>
                    <td>\${{ fillup.pricePerGallon | number:'1.2-2' }}</td>
                    <td>{{ fillup.gallons | number:'1.1-2' }}</td>
                    <td>{{ fillup.odometer | number }}</td>
                    <td>{{ fillup.mpg || '&mdash;' }}</td>
                    <td class="actions-cell">
                      <button (click)="openEditModal(fillup)" class="btn-secondary">Edit</button>
                      <button (click)="deleteFillup(fillup.id)" class="btn-danger">Del</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <app-fillup-modal
          [isOpen]="showModal()"
          [mode]="modalMode()"
          [fillup]="selectedFillup()"
          [autoId]="autosService.currentAutoId()"
          (close)="closeModal()"
          (save)="onSave($event)"
        ></app-fillup-modal>
      }
    </div>
  `,
  styles: [`
    .fillups-container {
      padding: 0;
    }

    .fillups-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }

    .fillups-header h2 {
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

    .fillups-table {
      width: 100%;
      border-collapse: collapse;
    }

    .fillups-table th {
      padding: 0.5rem 0.75rem;
      text-align: left;
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--text-secondary);
      border-bottom: 1px solid var(--border-color);
    }

    .fillups-table td {
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
      border-bottom: 1px solid var(--border-color);
      color: var(--text-primary);
    }

    .fillups-table tbody tr:hover {
      background: var(--bg-light);
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
      .fillups-header {
        flex-direction: column;
        gap: 1rem;
        align-items: stretch;
      }

      .btn-add {
        width: 100%;
      }

      .fillups-table th,
      .fillups-table td {
        padding: 0.4rem 0.5rem;
        font-size: 0.75rem;
      }
    }
  `]
})
export class FillupsComponent {
  autosService = inject(AutosService);
  fillupsService = inject(FillupsService);
  private toastService = inject(ToastService);

  showModal = signal(false);
  modalMode = signal<'add' | 'edit'>('add');
  selectedFillup = signal<Fillup | null>(null);

  constructor() {
    effect(async () => {
      const autoId = this.autosService.currentAutoId();
      if (autoId) {
        try {
          await this.fillupsService.loadFillups(autoId);
        } catch (err) {
          this.toastService.show('Failed to load fillups', 'error');
        }
      }
    });
  }

  openAddModal() {
    this.modalMode.set('add');
    this.selectedFillup.set(null);
    this.showModal.set(true);
  }

  openEditModal(fillup: Fillup) {
    this.modalMode.set('edit');
    this.selectedFillup.set(fillup);
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.selectedFillup.set(null);
  }

  async onSave(data: any) {
    const autoId = this.autosService.currentAutoId();
    if (!autoId) return;

    try {
      if (this.modalMode() === 'add') {
        await this.fillupsService.createFillup(autoId, data);
        this.toastService.show('Fillup added successfully', 'success');
      } else if (this.selectedFillup()) {
        await this.fillupsService.updateFillup(autoId, this.selectedFillup()!.id, data);
        this.toastService.show('Fillup updated successfully', 'success');
      }
      this.closeModal();
    } catch (err) {
      this.toastService.show('Failed to save fillup', 'error');
    }
  }

  async deleteFillup(id: number) {
    const autoId = this.autosService.currentAutoId();
    if (!autoId) return;

    if (confirm('Are you sure you want to delete this fillup?')) {
      try {
        await this.fillupsService.deleteFillup(autoId, id);
        this.toastService.show('Fillup deleted successfully', 'success');
      } catch (err) {
        this.toastService.show('Failed to delete fillup', 'error');
      }
    }
  }
}
