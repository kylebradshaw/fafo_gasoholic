import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AutosService } from '../../core/services/autos.service';
import { FillupsService, Fillup } from '../../core/services/fillups.service';
import { ToastService } from '../../core/services/toast.service';
import { FuelTypePipe } from '../../core/pipes/fuel-type.pipe';
import { FillupModalComponent } from './fillup-modal/fillup-modal.component';

@Component({
  selector: 'app-fillups',
  standalone: true,
  imports: [CommonModule, FuelTypePipe, FillupModalComponent],
  template: `
    <div class="fillups-container">
      @if (autosService.autos().length === 0) {
        <p class="empty-state">Select an auto to view fillup history.</p>
      } @else if (!autosService.currentAuto()) {
        <p class="empty-state">Select an auto to view fillup history.</p>
      } @else {
        <div class="fillups-header">
          <h2>{{ autosService.currentAuto()?.brand }} {{ autosService.currentAuto()?.model }} - Fillup Log</h2>
          <button (click)="openAddModal()" class="btn-add">+ Add Fillup</button>
        </div>

        @if (fillupsService.fillups().length === 0) {
          <p class="empty-state">No fillups recorded yet. Add one to get started.</p>
        } @else {
          <div class="fillups-table-container">
            <table class="fillups-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Fuel Type</th>
                  <th>Gallons</th>
                  <th>Price/Gal</th>
                  <th>Cost</th>
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
                    <td>{{ fillup.gallons | number:'1.1-2' }}</td>
                    <td>\${{ fillup.pricePerGallon | number:'1.2-2' }}</td>
                    <td>\${{ (fillup.gallons * fillup.pricePerGallon) | number:'1.2-2' }}</td>
                    <td>{{ fillup.odometer | number }}</td>
                    <td>{{ fillup.mpg || 'N/A' }}</td>
                    <td>
                      <button (click)="openEditModal(fillup)" class="btn-icon" title="Edit">✏️</button>
                      <button (click)="deleteFillup(fillup.id)" class="btn-icon btn-danger" title="Delete">🗑️</button>
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
      padding: 1rem;
    }

    .fillups-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .fillups-header h2 {
      margin: 0;
      font-size: 1.5rem;
      color: #111;
    }

    .btn-add {
      padding: 0.6rem 1rem;
      background: #ec7004;
      color: #fff;
      border: none;
      border-radius: 5px;
      cursor: pointer;
      font-weight: 500;
      transition: background 0.15s;
    }

    .btn-add:hover {
      background: #d86500;
    }

    .empty-state {
      text-align: center;
      color: #666;
      padding: 2rem;
    }

    .fillups-table-container {
      overflow-x: auto;
      background: #fff;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
    }

    .fillups-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }

    .fillups-table thead {
      background: #f5f5f5;
      border-bottom: 1px solid #e0e0e0;
    }

    .fillups-table th {
      padding: 0.75rem;
      text-align: left;
      font-weight: 600;
      color: #333;
    }

    .fillups-table td {
      padding: 0.75rem;
      border-bottom: 1px solid #e0e0e0;
    }

    .fillups-table tbody tr:hover {
      background: #fafafa;
    }

    .btn-icon {
      padding: 0.4rem;
      background: #f0f0f0;
      border: 1px solid #ddd;
      border-radius: 5px;
      cursor: pointer;
      font-size: 1rem;
      transition: background 0.15s;
      margin-right: 0.25rem;
    }

    .btn-icon:hover {
      background: #e0e0e0;
    }

    .btn-icon.btn-danger:hover {
      background: #ffcccc;
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

      .fillups-table {
        font-size: 0.8rem;
      }

      .fillups-table th,
      .fillups-table td {
        padding: 0.5rem;
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
