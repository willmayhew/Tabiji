import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSuggestion } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { DestinationEditor, NewPlaceDraft } from './destination-editor';

const kyotoSuggestion: PlaceSuggestion = {
  name: 'Kyoto',
  destinationName: 'Kyoto, Japan',
  label: 'Kyoto, Kyoto, Japan',
  latitude: 35.0116,
  longitude: 135.7681,
};

class FakeGeocodingService {
  search = vi.fn<(query: string) => Observable<PlaceSuggestion[]>>(() => of([kyotoSuggestion]));
}

const kyoto: TripDestination = {
  id: 'a1',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: null,
  longitude: null,
};

describe('DestinationEditor', () => {
  let fixture: ComponentFixture<DestinationEditor>;
  let geocoding: FakeGeocodingService;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    geocoding = new FakeGeocodingService();

    await TestBed.configureTestingModule({
      imports: [DestinationEditor],
      providers: [{ provide: GeocodingService, useValue: geocoding }],
    }).compileComponents();

    fixture = TestBed.createComponent(DestinationEditor);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function render(options: {
    open?: boolean;
    mode?: 'destination' | 'stop';
    parent?: TripDestination | null;
    tripStartsOn?: string;
    tripEndsOn?: string;
  } = {}): void {
    fixture.componentRef.setInput('open', options.open ?? true);
    fixture.componentRef.setInput('mode', options.mode ?? 'destination');
    fixture.componentRef.setInput('parent', options.parent ?? null);
    fixture.componentRef.setInput('tripStartsOn', options.tripStartsOn ?? '2027-04-01');
    fixture.componentRef.setInput('tripEndsOn', options.tripEndsOn ?? '2027-04-14');
    fixture.detectChanges();
  }

  function nameInput(): HTMLInputElement {
    return element().querySelector('#new-place-name') as HTMLInputElement;
  }

  function searchInput(): HTMLInputElement {
    return element().querySelector('#new-place-search') as HTMLInputElement;
  }

  async function settle(milliseconds = 0): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, milliseconds));
    fixture.detectChanges();
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submit(): void {
    (element().querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('should stay closed until it is opened', () => {
    render({ open: false });

    expect(element().querySelector('dialog')?.hasAttribute('open')).toBe(false);
  });

  it('should name a destination and a stop differently', () => {
    render({ mode: 'destination' });
    expect(element().textContent).toContain('Add a destination');

    render({ mode: 'stop', parent: kyoto });
    expect(element().textContent).toContain('Add a stop at Kyoto');
  });

  it('should put the focus in the name field when it opens', async () => {
    render();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(document.activeElement).toBe(nameInput());
  });

  it('should refuse a name of nothing', () => {
    render();

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('This field is required.');
  });

  it('should hand back the draft the reader confirmed', () => {
    render({ tripStartsOn: '2027-04-01', tripEndsOn: '2027-04-14' });

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    type(nameInput(), '  Osaka  ');
    type(element().querySelector('#new-place-starts-on') as HTMLInputElement, '2027-04-02');
    type(element().querySelector('#new-place-ends-on') as HTMLInputElement, '2027-04-05');

    submit();

    expect(saved).toEqual([
      {
        name: 'Osaka',
        startsOn: '2027-04-02',
        endsOn: '2027-04-05',
        latitude: null,
        longitude: null,
      },
    ]);
  });

  it('should find a place and carry its location', async () => {
    render();

    type(searchInput(), 'kyo');
    await settle(350);

    expect(geocoding.search).toHaveBeenCalledWith('kyo');
    expect(element().querySelector('.destination-editor__results')).not.toBeNull();

    element().querySelector<HTMLButtonElement>('.destination-editor__result')?.click();
    fixture.detectChanges();

    expect(nameInput().value).toBe('Kyoto, Japan');

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    submit();

    expect(saved[0]).toMatchObject({
      name: 'Kyoto, Japan',
      latitude: 35.0116,
      longitude: 135.7681,
    });
  });

  it('should drop the found location when the name is typed by hand', async () => {
    render();

    type(searchInput(), 'kyo');
    await settle(350);

    element().querySelector<HTMLButtonElement>('.destination-editor__result')?.click();
    fixture.detectChanges();

    type(nameInput(), 'Nara');

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    submit();

    expect(saved[0]).toMatchObject({
      name: 'Nara',
      latitude: null,
      longitude: null,
    });
  });

  it('should pick the first result on Enter without submitting the dialog', async () => {
    render();

    type(searchInput(), 'kyo');
    await settle(350);

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    searchInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(saved).toEqual([]);
    expect(nameInput().value).toBe('Kyoto, Japan');
  });

  it('should refuse a stop that falls outside its destination', () => {
    render({ mode: 'stop', parent: kyoto });

    const saved: NewPlaceDraft[] = [];
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));

    type(nameInput(), 'Namba');
    type(element().querySelector('#new-place-starts-on') as HTMLInputElement, '2027-04-05');

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('after the destination ends');
  });

  it('should dismiss without saving', () => {
    render();

    const dismissed: boolean[] = [];
    fixture.componentInstance.dismissed.subscribe(() => dismissed.push(true));

    const cancel = [...element().querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;

    cancel.click();

    expect(dismissed).toEqual([true]);
  });
});
