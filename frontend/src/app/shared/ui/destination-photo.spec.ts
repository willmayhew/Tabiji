import { TestBed } from '@angular/core/testing';
import { DestinationPhoto } from './destination-photo';

describe('DestinationPhoto', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DestinationPhoto],
    }).compileComponents();
  });

  function render(destination: string): HTMLElement {
    const fixture = TestBed.createComponent(DestinationPhoto);
    fixture.componentRef.setInput('destination', destination);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('should render the photograph for a known destination', () => {
    const image = render('Kyoto').querySelector('img');

    expect(image).not.toBeNull();
    expect(image?.getAttribute('src')).toBe('/photos/kyoto-1280.webp');
    expect(image?.getAttribute('srcset')).toContain('/photos/kyoto-640.webp 640w');
    expect(image?.getAttribute('alt')).toContain('Kyoto');
  });

  it('should give the image its intrinsic size so nothing shifts', () => {
    const image = render('Tokyo').querySelector('img');

    expect(image?.getAttribute('width')).toBe('1280');
    expect(image?.getAttribute('height')).toBe('853');
  });

  it('should load the image lazily by default', () => {
    expect(render('Osaka').querySelector('img')?.getAttribute('loading')).toBe('lazy');
  });

  it('should render the monogram fallback for a destination with no photograph', () => {
    const element = render('Kanto');

    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('.destination-photo__fallback')?.textContent).toContain('旅');
  });

  it('should hide the monogram fallback from assistive technology', () => {
    const fallback = render('Kanto').querySelector('.destination-photo__fallback');

    expect(fallback?.getAttribute('aria-hidden')).toBe('true');
  });
});
