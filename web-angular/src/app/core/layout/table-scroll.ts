import {
  DestroyRef,
  Directive,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';

/**
 * Le conteneur d'un tableau plus large que sa carte : le tableau y défile, pas la page entière
 * (classe table-scroll, styles.scss).
 *
 * Tant que le tableau déborde, le conteneur s'atteint au clavier, pour le faire défiler avec les
 * flèches même quand le tableau n'a ni bouton ni liste : tabindex 0, rôle group et le nom donné.
 * Sans débordement, il n'a rien de tout cela, et l'ordre du clavier reste celui de la page. S'il a
 * le focus quand le tableau cesse de déborder (l'écran tourne, la fenêtre s'élargit), il le garde
 * jusqu'à ce qu'on le quitte.
 */
@Directive({
  selector: '[appTableScroll]',
  host: {
    class: 'table-scroll',
    '[attr.tabindex]': 'reachable() ? 0 : null',
    '[attr.role]': "reachable() ? 'group' : null",
    '[attr.aria-label]': 'reachable() ? label() : null',
    // focus et blur ne remontent pas : seul le focus du conteneur lui-même compte.
    '(focus)': 'focused.set(true)',
    '(blur)': 'focused.set(false)',
  },
})
export class TableScroll {
  /** Le nom du conteneur, annoncé quand il reçoit le focus. */
  readonly label = input.required<string>({ alias: 'appTableScroll' });

  private readonly overflowing = signal(false);
  protected readonly focused = signal(false);
  protected readonly reachable = computed(() => this.overflowing() || this.focused());

  constructor() {
    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    // jsdom, où tournent les tests unitaires, n'a pas de ResizeObserver : le conteneur y reste tel
    // quel.
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(() => {
      this.overflowing.set(element.scrollWidth > element.clientWidth);
    });
    // Le conteneur change de largeur avec l'écran ; le tableau, avec ses lignes.
    observer.observe(element);
    afterNextRender(() => {
      const table = element.firstElementChild;
      if (table) observer.observe(table);
    });
    inject(DestroyRef).onDestroy(() => {
      observer.disconnect();
    });
  }
}
