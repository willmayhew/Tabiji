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
    expect(element.querySelector('.trip-card__dates')?.textContent).toContain('1 Apr – 14 Apr 2027');
  });

  it('should keep the machine readable dates on the time elements', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const times = element.querySelectorAll('.trip-card__dates time');

    expect(times.length).toBe(2);
    expect(times[0].getAttribute('datetime')).toBe('2027-04-01');
    expect(times[1].getAttribute('datetime')).toBe('2027-04-14');
  });

  it('should show the length of the trip', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.trip-card__duration')?.textContent).toContain('14 days');
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

  it('should not render an image', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('img')).toBeNull();
  });

  it('should draw the plate from the trip id', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const route = element.querySelector('.plate__route')?.getAttribute('d');

    expect(route).toContain('M ');

    fixture.componentRef.setInput('trip', { ...confirmed, id: 'a-different-trip-id' });
    fixture.detectChanges();

    expect(element.querySelector('.plate__route')?.getAttribute('d')).not.toBe(route);
  });

  it('should show the index on the plate', () => {
    fixture.componentRef.setInput('trip', confirmed);
    fixture.componentRef.setInput('index', 2);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.plate__index')?.textContent).toContain('No. 02');
  });
});
