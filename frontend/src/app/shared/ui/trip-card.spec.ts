import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Trip } from '../../core/models/trip';
import { TripCard } from './trip-card';

const confirmed: Trip = {
  id: '5f2b1c4e-0000-0000-0000-000000000000',
  name: 'Tokyo & Kyoto',
  destination: 'Japan',
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
  status: 'Confirmed',
};

describe('TripCard', () => {
  let fixture: ComponentFixture<TripCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TripCard],
    }).compileComponents();

    fixture = TestBed.createComponent(TripCard);
  });

  it('should render the trip', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.trip-card__name')?.textContent).toContain('Tokyo & Kyoto');
    expect(element.querySelector('.trip-card__destination')?.textContent).toContain('Japan');
    expect(element.querySelector('.trip-card__dates')?.textContent).toContain('2027-04-01');
    expect(element.querySelector('.trip-card__dates')?.textContent).toContain('2027-04-14');
  });

  it('should show the status as a badge', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');

    expect(badge?.textContent).toContain('Confirmed');
    expect(badge?.classList.contains('badge--healthy')).toBe(true);
  });

  it('should use the danger tone for a cancelled trip', () => {
    fixture.componentRef.setInput('trip', { ...confirmed, status: 'Cancelled' });
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');

    expect(badge?.classList.contains('badge--danger')).toBe(true);
  });

  it('should use the neutral tone for a planning trip', () => {
    fixture.componentRef.setInput('trip', { ...confirmed, status: 'Planning' });
    fixture.detectChanges();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');

    expect(badge?.classList.contains('badge--neutral')).toBe(true);
  });
});
