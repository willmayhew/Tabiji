import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the brand', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand-name')?.textContent).toContain('Tabiji');
  });

  it('should offer a skip link as the first focusable element', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;

    const skipLink = compiled.querySelector('.skip-link') as HTMLAnchorElement;

    expect(skipLink.getAttribute('href')).toBe('#main-content');
    expect(compiled.querySelector('main')?.getAttribute('id')).toBe('main-content');
  });

  it('should mark the active navigation link for assistive technology', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/trips');
    fixture.detectChanges();

    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.app-nav a')];

    expect(links.length).toBe(2);

    const trips = links.find((link) => link.textContent?.trim() === 'Trips') as HTMLAnchorElement;

    expect(trips.getAttribute('aria-current')).toBe('page');
    expect(trips.classList.contains('is-active')).toBe(true);
  });
});
