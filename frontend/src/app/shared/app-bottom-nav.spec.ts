import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { AppBottomNav } from './app-bottom-nav';

describe('AppBottomNav', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppBottomNav, RouterTestingModule],
    }).compileComponents();
  });

  it('opens the adult settings dropup with only working destinations', () => {
    const fixture = TestBed.createComponent(AppBottomNav);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector(
      '#adult-settings-menu-trigger',
    ) as HTMLButtonElement | null;
    trigger?.click();
    fixture.detectChanges();

    const menu = element.querySelector('#adult-settings-menu');
    expect(menu).toBeTruthy();
    expect(menu?.textContent).toContain('Barn och konton');
    expect(menu?.textContent).toContain('Bjud in vuxen');
    expect(menu?.textContent).not.toContain('Hantera vuxna');
    expect(menu?.textContent).not.toContain('Historik');
  });

  it('closes the adult settings dropup with Escape', () => {
    const fixture = TestBed.createComponent(AppBottomNav);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    (element.querySelector('#adult-settings-menu-trigger') as HTMLButtonElement | null)?.click();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(element.querySelector('#adult-settings-menu')).toBeNull();
  });
});
