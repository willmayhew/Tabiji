import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Home);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectHealthRequests(): void {
    http.expectOne('/health').flush({ status: 'Healthy', totalDurationMilliseconds: 1, checks: [] });
    http.expectOne('/health/ready').flush({
      status: 'Healthy',
      totalDurationMilliseconds: 1,
      checks: [],
    });
  }

  it('should load the health reports', () => {
    fixture.detectChanges();

    expectHealthRequests();
    fixture.detectChanges();

    expect(element().textContent).toContain('Healthy');
  });

  it('should reload the health reports when the refresh button is clicked', () => {
    fixture.detectChanges();

    expectHealthRequests();
    fixture.detectChanges();

    const refresh = element().querySelector('button') as HTMLButtonElement;
    refresh.click();
    fixture.detectChanges();

    expectHealthRequests();
    fixture.detectChanges();

    expect(element().textContent).toContain('Healthy');
  });

  it('should report when the API cannot be reached', () => {
    fixture.detectChanges();

    http.expectOne('/health').error(new ProgressEvent('error'), { status: 0 });
    http.expectOne('/health/ready').error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(element().textContent).toContain('The Tabiji API is not reachable');
  });
});
