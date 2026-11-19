import { Pipe, PipeTransform } from '@angular/core';

const dollars = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

// Amounts travel as cents. Empty means no balance import has filled them yet.
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(cents: number | null): string {
    return cents === null ? 'Not imported' : dollars.format(cents / 100);
  }
}
