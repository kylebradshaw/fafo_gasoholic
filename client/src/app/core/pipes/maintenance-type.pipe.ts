import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'maintenanceType',
  standalone: true
})
export class MaintenanceTypePipe implements PipeTransform {
  private labels: Record<string, string> = {
    'OilChange': 'Oil Change',
    'TireRotation': 'Tire Rotation',
    'BrakeInspection': 'Brake Inspection',
    'AirFilter': 'Air Filter',
    'CabinFilter': 'Cabin Filter',
    'WiperBlades': 'Wiper Blades',
    'TireReplacement': 'Tire Replacement',
    'BatteryReplacement': 'Battery Replacement',
    'Other': 'Other',
  };

  transform(value: string | null | undefined): string {
    if (!value) return 'Unknown';
    return this.labels[value] ?? value;
  }
}
