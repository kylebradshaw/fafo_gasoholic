import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'fuelType',
  standalone: true
})
export class FuelTypePipe implements PipeTransform {
  private fuelTypes = [
    'Regular',
    'Mid-grade',
    'Premium',
    'Diesel',
    'E85'
  ];

  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || value < 0 || value >= this.fuelTypes.length) {
      return 'Unknown';
    }
    return this.fuelTypes[value];
  }
}
