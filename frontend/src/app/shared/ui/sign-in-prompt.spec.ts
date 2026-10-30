import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SignInPrompt } from './sign-in-prompt';

describe('SignInPrompt', () => {
  let fixture: ComponentFixture<SignInPrompt>;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [SignInPrompt],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(SignInPrompt);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('should invite a sign-in and say why', () => {
    fixture.componentRef.setInput('message', 'Sign in to open this trip.');
    fixture.detectChanges();

    expect(element().textContent).toContain('Sign in to carry on');
    expect(element().textContent).toContain('Sign in to open this trip.');
  });

  it('should carry the page to come back to', () => {
    fixture.componentRef.setInput('returnUrl', '/trips/123/atlas');
    fixture.detectChanges();

    const link = element().querySelector('a.button') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/login?returnUrl=%2Ftrips%2F123%2Fatlas');
  });

  it('should use a section heading by default and the page heading when asked', () => {
    fixture.detectChanges();

    expect(element().querySelector('h2')).not.toBeNull();
    expect(element().querySelector('h1')).toBeNull();

    fixture.componentRef.setInput('level', 1);
    fixture.detectChanges();

    expect(element().querySelector('h1')).not.toBeNull();
    expect(element().querySelector('h2')).toBeNull();
  });
});
