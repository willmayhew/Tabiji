import { TestBed } from '@angular/core/testing';
import { DestinationPlate } from './destination-plate';

describe('DestinationPlate', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DestinationPlate],
    }).compileComponents();
  });

  function render(
    label: string | null,
    options: {
      seed?: string;
      index?: number | null;
      stops?: number;
      tone?: 'paper' | 'sage' | 'clay' | 'forest';
    } = {},
  ): HTMLElement {
    const fixture = TestBed.createComponent(DestinationPlate);
    fixture.componentRef.setInput('seed', options.seed ?? 'a-trip-id');
    fixture.componentRef.setInput('label', label);

    if (options.index !== undefined) {
      fixture.componentRef.setInput('index', options.index);
    }

    if (options.stops !== undefined) {
      fixture.componentRef.setInput('stops', options.stops);
    }

    if (options.tone !== undefined) {
      fixture.componentRef.setInput('tone', options.tone);
    }

    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  }

  it('should set the label in type', () => {
    const element = render('Kyoto');

    expect(element.querySelector('.plate__label')?.textContent).toContain('Kyoto');
  });

  it('should draw the route motif', () => {
    const element = render('Kyoto');
    const route = element.querySelector('.plate__route');

    expect(route?.getAttribute('d')).toContain('M ');
    expect(element.querySelectorAll('.plate__node').length).toBeGreaterThanOrEqual(2);
  });

  it('should draw a waypoint for each stop', () => {
    expect(render('Kyoto', { stops: 6 }).querySelectorAll('.plate__node')).toHaveLength(6);
  });

  it('should draw two waypoints for a trip with a single stop', () => {
    expect(render('Kyoto', { stops: 1 }).querySelectorAll('.plate__node')).toHaveLength(2);
  });

  it('should draw the graticule behind the route', () => {
    const element = render('Kyoto');

    expect(element.querySelector('.plate__graticule')?.getAttribute('d')).toContain('M ');
  });

  it('should hide the whole plate from assistive technology', () => {
    const element = render('Kyoto');

    expect(element.querySelector('.plate')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('should never render an image', () => {
    expect(render('Kyoto').querySelector('img')).toBeNull();
  });

  it('should show the index when there is one', () => {
    const element = render('Kyoto', { index: 3 });

    expect(element.querySelector('.plate__index')?.textContent).toContain('No. 03');
  });

  it('should pad a single digit index', () => {
    expect(render('Kyoto', { index: 1 }).querySelector('.plate__index')?.textContent).toContain(
      'No. 01',
    );
  });

  it('should leave the index out when there is none', () => {
    expect(render('Kyoto').querySelector('.plate__index')).toBeNull();
  });

  it('should apply the ground it is given', () => {
    const element = render('Kyoto', { tone: 'forest' });

    expect(element.querySelector('.plate')?.classList.contains('plate--forest')).toBe(true);
  });

  it('should apply a ground derived from the seed when none is given', () => {
    const element = render('Kyoto');

    expect(element.querySelector('.plate')?.className).toMatch(/plate--paper|plate--sage|plate--clay|plate--forest/);
  });

  it('should draw the same route for the same seed', () => {
    const first = render('Kyoto', { seed: 'trip-one' }).querySelector('.plate__route')?.getAttribute('d');
    const second = render('Kyoto', { seed: 'trip-one' }).querySelector('.plate__route')?.getAttribute('d');

    expect(first).toBe(second);
  });

  it('should draw a card or a band', () => {
    expect(render('Kyoto').querySelector('.plate')?.classList.contains('plate--card')).toBe(true);
  });

  it('should show no label when there is none', () => {
    const element = render(null);

    expect(element.querySelector('.plate__label')).toBeNull();
    expect(element.querySelector('.plate__route')).not.toBeNull();
  });

  it('should trim a very long label', () => {
    const element = render('x'.repeat(120));

    const label = element.querySelector('.plate__label')?.textContent ?? '';

    expect(label.length).toBeLessThan(50);
    expect(label.endsWith('…')).toBe(true);
  });
});
