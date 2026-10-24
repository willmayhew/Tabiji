import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSuggestion, SavedPlace, SavedPlaceDraft } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { PlaceEditor } from './place-editor';

const kyoto: PlaceSuggestion = {
  name: 'Kyoto',
  destinationName: 'Kyoto, Japan',
  label: 'Kyoto, Japan',
  latitude: 35.0116,
  longitude: 135.7681,
};

const fushimi: SavedPlace = {
  id: 'p1',
  destinationId: 'b2',
  name: 'Fushimi Inari',
  latitude: 34.9671,
  longitude: 135.7727,
  category: 'Sight',
  address: '68 Fukakusa Yabunouchicho, Kyoto',
  notes: 'Go early.',
  createdAt: '2026-10-20T09:00:00+00:00',
};

const destinations: TripDestination[] = [
  {
    id: 'a1',
    name: 'Kyoto',
    sortOrder: 0,
    startsOn: null,
    endsOn: null,
    latitude: null,
    longitude: null,
  },
  {
    id: 'b2',
    name: 'Nara',
    sortOrder: 1,
    startsOn: null,
    endsOn: null,
    latitude: null,
    longitude: null,
  },
];

class FakeGeocodingService {
  search = vi.fn<(query: string) => Observable<PlaceSuggestion[]>>(() => of([kyoto]));
  reverse = vi.fn(() => of(null));
}

describe('PlaceEditor', () => {
  let fixture: ComponentFixture<PlaceEditor>;
  let geocoding: FakeGeocodingService;
  let saved: SavedPlaceDraft[];
  let dismissed: number;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    geocoding = new FakeGeocodingService();
    saved = [];
    dismissed = 0;

    await TestBed.configureTestingModule({
      imports: [PlaceEditor],
      providers: [{ provide: GeocodingService, useValue: geocoding }],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceEditor);
    fixture.componentRef.setInput('destinations', destinations);
    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));
    fixture.componentInstance.dismissed.subscribe(() => (dismissed += 1));
    fixture.detectChanges();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function dialog(): HTMLDialogElement {
    return element().querySelector('dialog') as HTMLDialogElement;
  }

  function input(selector: string): HTMLInputElement {
    return element().querySelector(selector) as HTMLInputElement;
  }

  function button(text: string): HTMLButtonElement {
    const found = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    );

    return found as HTMLButtonElement;
  }

  async function settle(milliseconds = 0): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, milliseconds));
    fixture.detectChanges();
  }

  async function open(options: { place?: SavedPlace | null } = {}): Promise<void> {
    fixture.componentRef.setInput('place', options.place ?? null);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    await settle();
  }

  async function search(term: string): Promise<void> {
    const box = input('.editor__search-input');

    box.value = term;
    box.dispatchEvent(new Event('input'));
    await settle(350);
  }

  function submit(): void {
    const form = element().querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  function text(selector: string): string {
    return element().querySelector(selector)?.textContent?.trim() ?? '';
  }

  it('should stay closed until it is opened', () => {
    expect(dialog().hasAttribute('open')).toBe(false);
  });

  it('should open as a dialog and put the focus in the search box', async () => {
    await open();

    expect(dialog().hasAttribute('open')).toBe(true);
    expect(document.activeElement).toBe(input('.editor__search-input'));
  });

  it('should start an empty draft when a place is being added', async () => {
    await open();

    expect(text('.editor__title')).toContain('Add a place');
    expect(input('#place-name').value).toBe('');
    expect(element().textContent).toContain('Choose a result above to set its position');
  });

  it('should show what the search finds', async () => {
    await open();
    await search('ky');

    const results = [...element().querySelectorAll('.editor__result')].map((node) =>
      node.textContent?.trim(),
    );

    expect(geocoding.search).toHaveBeenCalledWith('ky');
    expect(results).toHaveLength(1);
    expect(results[0]).toContain('Kyoto');
  });

  it('should wait for the reader to settle before asking the geocoder', async () => {
    await open();

    const box = input('.editor__search-input');
    box.value = 'k';
    box.dispatchEvent(new Event('input'));

    expect(geocoding.search).not.toHaveBeenCalled();

    await settle(350);

    expect(geocoding.search).toHaveBeenCalledTimes(1);
  });

  it('should not ask the geocoder for an empty query', async () => {
    await open();
    await search('   ');

    expect(geocoding.search).not.toHaveBeenCalled();
  });

  it('should say when nothing matches', async () => {
    geocoding.search.mockReturnValue(of([]));

    await open();
    await search('zzz');

    expect(element().textContent).toContain('No places found');
  });

  it('should fill the draft from a result, and persist nothing', async () => {
    await open();
    await search('ky');

    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(input('#place-name').value).toBe('Kyoto');
    expect(text('.editor__chosen-address')).toContain('Kyoto, Japan');
    expect(text('.editor__chosen-coords')).toContain('35.0116');
    expect(text('.editor__chosen-coords')).toContain('135.7681');

    // Choosing is not saving: the draft exists, the trip does not yet.
    expect(saved).toHaveLength(0);
  });

  it('should refuse to save without a position', async () => {
    await open();

    input('#place-name').value = 'Somewhere';
    input('#place-name').dispatchEvent(new Event('input'));
    submit();

    expect(saved).toHaveLength(0);
    expect(element().textContent).toContain('Choose a place from the search first.');
  });

  it('should refuse to save without a name', async () => {
    await open();
    await search('ky');
    (element().querySelector('.editor__result') as HTMLButtonElement).click();

    input('#place-name').value = '   ';
    input('#place-name').dispatchEvent(new Event('input'));
    submit();

    expect(saved).toHaveLength(0);
    expect(element().textContent).toContain('This field is required.');
  });

  it('should hand back the draft the reader confirmed', async () => {
    await open();
    await search('ky');
    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();

    const category = element().querySelector('#place-category') as HTMLSelectElement;
    category.value = 'Food';
    category.dispatchEvent(new Event('change'));

    const stop = element().querySelector('#place-destination') as HTMLSelectElement;
    stop.value = 'b2';
    stop.dispatchEvent(new Event('change'));

    const notes = element().querySelector('#place-notes') as HTMLTextAreaElement;
    notes.value = '  Bring cash.  ';
    notes.dispatchEvent(new Event('input'));

    submit();

    expect(saved).toEqual([
      {
        name: 'Kyoto',
        latitude: 35.0116,
        longitude: 135.7681,
        category: 'Food',
        address: 'Kyoto, Japan',
        notes: 'Bring cash.',
        destinationId: 'b2',
      },
    ]);
  });

  it('should send an untouched category and stop as nothing at all', async () => {
    await open();
    await search('ky');
    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();

    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0].category).toBeNull();
    expect(saved[0].destinationId).toBeNull();
    expect(saved[0].notes).toBeNull();
  });

  it('should load a saved place into the draft', async () => {
    await open({ place: fushimi });

    expect(text('.editor__title')).toContain('Edit place');
    expect(input('#place-name').value).toBe('Fushimi Inari');
    expect((element().querySelector('#place-category') as HTMLSelectElement).value).toBe('Sight');
    expect((element().querySelector('#place-destination') as HTMLSelectElement).value).toBe('b2');
    expect((element().querySelector('#place-notes') as HTMLTextAreaElement).value).toBe('Go early.');
    expect(text('.editor__chosen-coords')).toContain('34.9671');
  });

  it('should keep the position when only the name is changed', async () => {
    await open({ place: fushimi });

    input('#place-name').value = 'Fushimi Inari Taisha';
    input('#place-name').dispatchEvent(new Event('input'));
    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0].name).toBe('Fushimi Inari Taisha');
    expect(saved[0].latitude).toBe(34.9671);
    expect(saved[0].longitude).toBe(135.7727);
    expect(saved[0].address).toBe('68 Fukakusa Yabunouchicho, Kyoto');
  });

  it('should take a new position only when a different result is chosen', async () => {
    await open({ place: fushimi });
    await search('ky');
    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();

    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0].latitude).toBe(35.0116);
    expect(saved[0].name).toBe('Kyoto');
  });

  it('should offer a stop the trip no longer has as unassigned', async () => {
    await open({ place: { ...fushimi, destinationId: 'gone' } });

    expect((element().querySelector('#place-destination') as HTMLSelectElement).value).toBe('');
  });

  it('should change nothing when the reader cancels', async () => {
    await open({ place: fushimi });

    input('#place-name').value = 'Something else';
    input('#place-name').dispatchEvent(new Event('input'));

    button('Cancel').click();
    fixture.detectChanges();

    expect(dismissed).toBe(1);
    expect(saved).toHaveLength(0);
  });

  it('should close on Escape without saving', async () => {
    await open();

    dialog().dispatchEvent(new Event('cancel'));
    fixture.detectChanges();

    expect(dismissed).toBe(1);
    expect(saved).toHaveLength(0);
  });

  it('should leave the draft usable when the search fails', async () => {
    geocoding.search.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 503,
            statusText: 'Service Unavailable',
            error: { title: 'Search is unavailable' },
          }),
      ),
    );

    await open({ place: fushimi });
    await search('kyoto');

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Search is unavailable',
    );

    // The place the reader already had is untouched, and can still be saved.
    expect(input('#place-name').value).toBe('Fushimi Inari');

    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0].longitude).toBe(135.7727);
  });

  it('should pick the first result when Enter is pressed in the search box', async () => {
    await open();
    await search('ky');

    const box = input('.editor__search-input');
    box.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    // Enter chooses a place rather than submitting the draft beneath it.
    expect(saved).toHaveLength(0);
    expect(input('#place-name').value).toBe('Kyoto');
  });

  it('should give the focus back to where it came from when it closes', async () => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();

    await open();

    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();
    await settle();

    expect(document.activeElement).toBe(trigger);

    trigger.remove();
  });

  it('should show a failure from the screen above and stay open', async () => {
    await open();
    await search('ky');
    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();

    fixture.componentRef.setInput('error', 'The destination does not belong to this trip.');
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'The destination does not belong to this trip.',
    );
  });
});
