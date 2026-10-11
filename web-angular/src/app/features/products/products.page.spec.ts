import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import type { PagedResultOfProductDto, ProductDto } from '../../api/generated';
import { ProductsPage } from './products.page';

const PRODUCTS_URL = '/api/v1/products';
const PEN: ProductDto = { id: '0199c9a0-0000-7000-8000-000000000011', name: 'Stylo', price: 2.5 };
const INK: ProductDto = { id: '0199c9a0-0000-7000-8000-000000000012', name: 'Encre', price: 4 };
const NO_CONTENT = { status: 204, statusText: 'No Content' };

function page(items: ProductDto[]): PagedResultOfProductDto {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

describe('ProductsPage', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ProductsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  async function open(items: ProductDto[] = []): Promise<ComponentFixture<ProductsPage>> {
    const fixture = TestBed.createComponent(ProductsPage);
    // La liste se charge à l'affichage ; whenStable attendrait la réponse, que le test fournit.
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url: PRODUCTS_URL }).flush(page(items));
    await fixture.whenStable();
    return fixture;
  }

  // Après une action, la page relit la liste.
  async function reload(fixture: ComponentFixture<ProductsPage>, items: ProductDto[]) {
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url: PRODUCTS_URL }).flush(page(items));
    await fixture.whenStable();
  }

  const element = (fixture: ComponentFixture<ProductsPage>) => fixture.nativeElement as HTMLElement;
  // Les deux champs du formulaire, dans l'ordre : le nom, puis le prix.
  const nameInput = (fixture: ComponentFixture<ProductsPage>) =>
    element(fixture).querySelectorAll<HTMLInputElement>('form input')[0]!;
  const priceInput = (fixture: ComponentFixture<ProductsPage>) =>
    element(fixture).querySelectorAll<HTMLInputElement>('form input')[1]!;
  const form = (fixture: ComponentFixture<ProductsPage>) => element(fixture).querySelector('form')!;
  const button = (fixture: ComponentFixture<ProductsPage>, label: string) =>
    Array.from(element(fixture).querySelectorAll('button')).find(
      (b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label,
    );
  const status = (fixture: ComponentFixture<ProductsPage>) =>
    element(fixture).querySelector('[role=status]')?.textContent?.trim();
  const cells = (fixture: ComponentFixture<ProductsPage>) =>
    Array.from(element(fixture).querySelectorAll('tr.mat-mdc-row')).map((row) =>
      Array.from(row.querySelectorAll('td'))
        .slice(0, 2)
        .map((cell) => cell.textContent?.trim()),
    );

  async function fill(fixture: ComponentFixture<ProductsPage>, name: string, price: string) {
    nameInput(fixture).value = name;
    nameInput(fixture).dispatchEvent(new Event('input'));
    priceInput(fixture).value = price;
    priceInput(fixture).dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function submit(fixture: ComponentFixture<ProductsPage>) {
    form(fixture).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  // Comme un clic : le bouton « Enregistrer » a le focus quand le formulaire part.
  async function clickSave(fixture: ComponentFixture<ProductsPage>) {
    const save = button(fixture, 'Enregistrer')!;
    save.focus();
    save.click();
    await settle(fixture);
  }

  // Laisse passer les réponses déjà fournies, sans attendre les requêtes encore ouvertes.
  async function settle(fixture: ComponentFixture<ProductsPage>) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function edit(fixture: ComponentFixture<ProductsPage>, product: ProductDto) {
    button(fixture, `Modifier ${product.name}`)!.click();
    await fixture.whenStable();
  }

  describe('adding', () => {
    // L'API accepte un prix nul (GreaterThanOrEqualTo(0)) : un produit gratuit se crée aussi ici.
    it('creates a product with a price of 0', async () => {
      const fixture = await open();

      await fill(fixture, 'Échantillon', '0');
      await submit(fixture);

      const create = httpMock.expectOne({ method: 'POST', url: PRODUCTS_URL });
      expect(create.request.body).toEqual({ name: 'Échantillon', price: 0 });
      create.flush(
        { id: '0199c9a0-0000-7000-8000-000000000002' },
        { status: 201, statusText: 'Created' },
      );
      await reload(fixture, []);
      expect(nameInput(fixture).value).toBe('');
      // « Ajouter », désactivé pendant l'envoi, a perdu le focus : il va au nom.
      expect(document.activeElement).toBe(nameInput(fixture));
    });

    // Pendant un ajout, « Modifier » et « Supprimer » attendent : la réponse de l'ajout viderait
    // le formulaire d'une modification ouverte entre-temps.
    it('keeps the row actions waiting while a product is added', async () => {
      const fixture = await open([PEN]);
      await fill(fixture, 'Encre', '4');

      await submit(fixture);
      await settle(fixture);
      expect(button(fixture, 'Modifier Stylo')!.disabled).toBe(true);
      expect(button(fixture, 'Supprimer Stylo')!.disabled).toBe(true);
      button(fixture, 'Modifier Stylo')!.click();
      httpMock
        .expectOne({ method: 'POST', url: PRODUCTS_URL })
        .flush({ id: INK.id }, { status: 201, statusText: 'Created' });
      await reload(fixture, [INK, PEN]);

      expect(button(fixture, 'Ajouter')).toBeDefined();
      expect(button(fixture, 'Modifier Stylo')!.disabled).toBe(false);
    });

    it('moves the focus to the name after a refused add', async () => {
      const fixture = await open();
      await fill(fixture, 'Stylo', '2');

      const add = button(fixture, 'Ajouter')!;
      add.focus();
      add.click();
      await settle(fixture);
      httpMock
        .expectOne({ method: 'POST', url: PRODUCTS_URL })
        .flush({ status: 400, title: 'Bad Request' }, { status: 400, statusText: 'Bad Request' });
      await fixture.whenStable();

      expect(nameInput(fixture).value).toBe('Stylo');
      expect(document.activeElement).toBe(nameInput(fixture));
    });

    it.each([
      ['a negative price', 'Stylo', '-1'],
      ['an empty price field', 'Stylo', ''],
      ['a price that is not a number', 'Stylo', '2a'],
      ['a price with three decimals', 'Stylo', '2.555'],
      // Au-delà de 13 chiffres, le nombre envoyé (un double) perdrait des centimes.
      ['a price of 14 digits', 'Stylo', '12345678901234'],
      ['a blank name', '   ', '2'],
      ['a name over 200 characters', 'x'.repeat(201), '2'],
    ])('does not send a product with %s', async (_case, name, price) => {
      const fixture = await open();

      await fill(fixture, name, price);
      await submit(fixture);

      httpMock.expectNone({ method: 'POST', url: PRODUCTS_URL });
    });
  });

  describe('editing', () => {
    it('names the actions of each row after its product', async () => {
      const fixture = await open([PEN, INK]);

      expect(button(fixture, 'Modifier Stylo')).toBeDefined();
      expect(button(fixture, 'Supprimer Stylo')).toBeDefined();
      expect(button(fixture, 'Modifier Encre')).toBeDefined();
      expect(button(fixture, 'Supprimer Encre')).toBeDefined();
    });

    // Les règles du formulaire posent aussi les attributs natifs : le navigateur et les lecteurs
    // d'écran savent les deux champs obligatoires. Le prix est un champ texte, avec le clavier
    // décimal sur mobile.
    it('marks both fields required, the name limited and the price decimal', async () => {
      const fixture = await open([PEN]);

      expect(nameInput(fixture).required).toBe(true);
      expect(nameInput(fixture).maxLength).toBe(200);
      expect(priceInput(fixture).required).toBe(true);
      expect(priceInput(fixture).type).toBe('text');
      expect(priceInput(fixture).getAttribute('inputmode')).toBe('decimal');
    });

    it('accepts a comma as the decimal separator', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);

      await fill(fixture, 'Stylo', '3,20');
      await submit(fixture);

      const put = httpMock.expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` });
      expect(put.request.body).toEqual({ name: 'Stylo', price: 3.2 });
      put.flush(null, NO_CONTENT);
      await reload(fixture, [{ ...PEN, price: 3.2 }]);
    });

    const errorsOf = (input: HTMLInputElement) =>
      Array.from(input.closest('mat-form-field')!.querySelectorAll('mat-error'), (e) =>
        e.textContent?.trim(),
      );
    const markedInvalid = (input: HTMLInputElement) =>
      input.closest('mat-form-field')!.classList.contains('mat-form-field-invalid');

    // Après Échap, la page déplace le focus : le champ vidé est quitté. Il n'est pas en erreur
    // pour autant, l'utilisateur ne l'ayant pas modifié depuis.
    it('shows no error on the emptied form when the focus leaves it', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');
      nameInput(fixture).dispatchEvent(
        new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
      );
      await fixture.whenStable();

      nameInput(fixture).dispatchEvent(new Event('blur'));
      priceInput(fixture).dispatchEvent(new Event('blur'));
      await fixture.whenStable();

      expect(errorsOf(nameInput(fixture))).toEqual([]);
      expect(markedInvalid(nameInput(fixture))).toBe(false);
      expect(markedInvalid(priceInput(fixture))).toBe(false);
    });

    it('shows the error of a field the user emptied and left', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);

      await fill(fixture, '', '2.50');
      nameInput(fixture).dispatchEvent(new Event('blur'));
      await fixture.whenStable();

      expect(errorsOf(nameInput(fixture))).toEqual(['Nom requis']);
      expect(markedInvalid(nameInput(fixture))).toBe(true);
      expect(markedInvalid(priceInput(fixture))).toBe(false);
    });

    // Un envoi refusé montre toutes les erreurs ; la remise à zéro suivante les efface.
    it('shows every error of an empty form submitted, until it is reset', async () => {
      const fixture = await open([PEN]);

      await submit(fixture);

      httpMock.expectNone({ method: 'POST' });
      expect(errorsOf(nameInput(fixture))).toEqual(['Nom requis']);
      expect(errorsOf(priceInput(fixture))).toEqual(['Prix requis']);
      expect(markedInvalid(priceInput(fixture))).toBe(true);
      await edit(fixture, PEN);
      expect(errorsOf(nameInput(fixture))).toEqual([]);
      // De retour au formulaire vide (Échap), les erreurs du premier envoi ne reviennent pas.
      nameInput(fixture).dispatchEvent(
        new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
      );
      await fixture.whenStable();
      expect(errorsOf(nameInput(fixture))).toEqual([]);
      expect(errorsOf(priceInput(fixture))).toEqual([]);
      expect(markedInvalid(priceInput(fixture))).toBe(false);
    });

    // Entrée dans le champ : le formulaire part sans que le champ ait été quitté ; l'erreur
    // s'affiche quand même, sous le champ concerné.
    it.each([
      ['name', '   ', '3', 'Nom requis'],
      ['price', 'Stylo', '-1', 'Un prix positif, avec deux décimales au plus (2,50)'],
    ])('shows why a change with an invalid %s is not sent', async (field, name, price, message) => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);

      await fill(fixture, name, price);
      await submit(fixture);

      httpMock.expectNone({ method: 'PUT' });
      const input = field === 'name' ? nameInput(fixture) : priceInput(fixture);
      const errors = Array.from(
        input.closest('mat-form-field')!.querySelectorAll('mat-error'),
        (e) => e.textContent?.trim(),
      );
      expect(errors).toEqual([message]);
    });

    it('fills the form with the product and moves the focus to its name', async () => {
      const fixture = await open([PEN, INK]);

      await edit(fixture, PEN);

      expect(nameInput(fixture).value).toBe('Stylo');
      expect(priceInput(fixture).value).toBe('2.50');
      expect(element(fixture).textContent).toContain('Modification de « Stylo »');
      expect(form(fixture).getAttribute('aria-labelledby')).toBe('editing-caption');
      expect(button(fixture, 'Enregistrer')).toBeDefined();
      expect(button(fixture, 'Ajouter')).toBeUndefined();
      expect(document.activeElement).toBe(nameInput(fixture));
    });

    // Le prix d'un decimal .NET peut arriver en texte (« 2.50 ») selon la sérialisation.
    it('fills the price of a product whose price comes as text', async () => {
      const fixture = await open([{ ...PEN, price: '2.50' }]);

      await edit(fixture, PEN);

      expect(priceInput(fixture).value).toBe('2.50');
    });

    it('saves the change, reloads the list and gives the focus back to the product', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);

      await fill(fixture, 'Stylo bleu', '3');
      await submit(fixture);
      const put = httpMock.expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` });
      expect(put.request.body).toEqual({ name: 'Stylo bleu', price: 3 });
      put.flush(null, NO_CONTENT);
      await reload(fixture, [{ ...PEN, name: 'Stylo bleu', price: 3 }, INK]);

      expect(cells(fixture)).toEqual([
        ['Stylo bleu', '€3.00'],
        ['Encre', '€4.00'],
      ]);
      expect(status(fixture)).toBe('« Stylo bleu » est enregistré.');
      expect(nameInput(fixture).value).toBe('');
      expect(button(fixture, 'Ajouter')).toBeDefined();
      expect(document.activeElement).toBe(button(fixture, 'Modifier Stylo bleu'));
    });

    // Renommé, le produit a pu quitter la page relue (tri par nom, 20 par page).
    it('gives the focus to the name when the saved product is no longer listed', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);

      await fill(fixture, 'Zzz', '3');
      await clickSave(fixture);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [INK]);

      expect(document.activeElement).toBe(nameInput(fixture));
    });

    // Pendant la relecture, une action ou un focus déplacé l'emportent : une autre modification,
    // ouverte après un enregistrement au clic ou par Entrée (un champ a alors le focus), une
    // saisie, ou le focus mis sur un autre bouton.
    it.each([
      ['another edit, after a save by click', 'click', 'edit'],
      ['another edit, after a save by Enter', 'enter', 'edit'],
      ['typing a name, after a save by Enter', 'enter', 'name'],
      ['typing a price, after a save by Enter in the price', 'enter-price', 'price'],
      ['moving the focus, after a save by click', 'click', 'focus'],
    ])('leaves the focus where it is after %s during the reload', async (_case, save, action) => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');
      if (save === 'click') {
        await clickSave(fixture);
      } else {
        // Entrée dans un champ : ce champ a le focus quand le formulaire part.
        (save === 'enter-price' ? priceInput(fixture) : nameInput(fixture)).focus();
        form(fixture).dispatchEvent(new Event('submit'));
        await settle(fixture);
      }
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await settle(fixture);

      const field =
        action === 'focus'
          ? button(fixture, 'Supprimer Encre')!
          : action === 'price'
            ? priceInput(fixture)
            : nameInput(fixture);
      if (action === 'edit') {
        button(fixture, 'Modifier Encre')!.click();
      } else if (action === 'focus') {
        field.focus();
      } else {
        const input = field as HTMLInputElement;
        input.focus();
        input.value = action === 'price' ? '5' : 'Gomme';
        input.dispatchEvent(new Event('input'));
      }
      await settle(fixture);
      await reload(fixture, [{ ...PEN, name: 'Stylo bleu', price: 3 }, INK]);

      if (action === 'edit') expect(nameInput(fixture).value).toBe('Encre');
      expect(document.activeElement).toBe(field);
    });

    // Pendant la relecture, la liste reste affichée au lieu de « Chargement… ».
    it('keeps the list on screen while it is read again', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);

      await submit(fixture);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await settle(fixture);

      expect(cells(fixture)).toEqual([
        ['Stylo', '€2.50'],
        ['Encre', '€4.00'],
      ]);
      expect(element(fixture).textContent).not.toContain('Chargement');
      await reload(fixture, [PEN, INK]);
    });

    it('keeps the row actions waiting while a change is sent', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');

      await submit(fixture);
      await settle(fixture);

      expect(button(fixture, 'Supprimer Stylo')!.disabled).toBe(true);
      expect(button(fixture, 'Modifier Encre')!.disabled).toBe(true);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [{ ...PEN, name: 'Stylo bleu', price: 3 }, INK]);
      expect(button(fixture, 'Supprimer Stylo bleu')!.disabled).toBe(false);
    });

    it.each([
      ['an empty name', '', '3'],
      ['a blank name', '   ', '3'],
      ['a name over 200 characters', 'x'.repeat(201), '3'],
      ['a negative price', 'Stylo', '-1'],
      ['an empty price field', 'Stylo', ''],
      ['a price that is not a number', 'Stylo', 'abc'],
    ])('does not send a change with %s', async (_case, name, price) => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);

      await fill(fixture, name, price);
      await submit(fixture);

      httpMock.expectNone({ method: 'PUT' });
    });

    it('sends a single change when the form is submitted twice', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');

      form(fixture).dispatchEvent(new Event('submit'));
      form(fixture).dispatchEvent(new Event('submit'));

      const puts = httpMock.match({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` });
      expect(puts).toHaveLength(1);
      puts[0]!.flush(null, NO_CONTENT);
      await reload(fixture, [PEN]);
    });

    it.each([
      ['the Annuler button', 'button'],
      ['the Escape key', 'escape'],
    ])('cancels with %s, sends nothing and gives the focus back', async (_case, how) => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');

      if (how === 'button') {
        button(fixture, 'Annuler')!.click();
      } else {
        nameInput(fixture).dispatchEvent(
          new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
        );
      }
      await fixture.whenStable();

      httpMock.expectNone({ method: 'PUT' });
      expect(nameInput(fixture).value).toBe('');
      expect(button(fixture, 'Ajouter')).toBeDefined();
      expect(element(fixture).textContent).not.toContain('Modification de');
      expect(document.activeElement).toBe(button(fixture, 'Modifier Stylo'));
    });

    // Même sans aucun rendu entre « Modifier », la saisie et Échap (ce qu'un navigateur chargé
    // peut faire), Échap vide les champs : la remise à zéro écrit le DOM elle-même.
    it('empties the fields on Escape even when nothing was rendered since « Modifier »', async () => {
      const fixture = await open([PEN, INK]);

      button(fixture, 'Modifier Encre')!.click();
      nameInput(fixture).value = 'Encre noire';
      nameInput(fixture).dispatchEvent(new Event('input'));
      priceInput(fixture).value = '5';
      priceInput(fixture).dispatchEvent(new Event('input'));
      nameInput(fixture).dispatchEvent(
        new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
      );
      await fixture.whenStable();

      expect(button(fixture, 'Ajouter')).toBeDefined();
      expect(nameInput(fixture).value).toBe('');
      expect(priceInput(fixture).value).toBe('');
      httpMock.expectNone({ method: 'PUT' });
    });

    // Le toast global (httpErrorInterceptor, absent de ce test) affiche le refus.
    it('stays in edit mode after a refusal and reloads the list', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);
      await fill(fixture, 'Stylo bleu', '3');

      await clickSave(fixture);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush({ status: 409, title: 'Conflict' }, { status: 409, statusText: 'Conflict' });
      await reload(fixture, [PEN]);

      expect(nameInput(fixture).value).toBe('Stylo bleu');
      expect(button(fixture, 'Enregistrer')!.disabled).toBe(false);
      expect(status(fixture)).toBe('');
      // « Enregistrer », désactivé pendant l'envoi, a perdu le focus : il va au nom.
      expect(document.activeElement).toBe(nameInput(fixture));
    });

    it('goes back to adding when the product no longer exists', async () => {
      const fixture = await open([PEN]);
      await edit(fixture, PEN);

      await clickSave(fixture);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(
          { status: 404, title: 'Not Found', code: 'Product.NotFound' },
          { status: 404, statusText: 'Not Found' },
        );
      await reload(fixture, []);

      expect(nameInput(fixture).value).toBe('');
      expect(button(fixture, 'Ajouter')).toBeDefined();
      expect(document.activeElement).toBe(nameInput(fixture));
    });

    it('clears the saved message when a product is deleted', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);
      await submit(fixture);
      httpMock
        .expectOne({ method: 'PUT', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [PEN, INK]);
      expect(status(fixture)).toBe('« Stylo » est enregistré.');

      button(fixture, 'Supprimer Encre')!.click();
      httpMock
        .expectOne({ method: 'DELETE', url: `${PRODUCTS_URL}/${INK.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [PEN]);

      expect(status(fixture)).toBe('');
    });

    it('goes back to adding when the product being edited is deleted', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);

      button(fixture, 'Supprimer Stylo')!.click();
      httpMock
        .expectOne({ method: 'DELETE', url: `${PRODUCTS_URL}/${PEN.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [INK]);

      expect(nameInput(fixture).value).toBe('');
      expect(button(fixture, 'Ajouter')).toBeDefined();
    });

    it('keeps editing a product when another one is deleted', async () => {
      const fixture = await open([PEN, INK]);
      await edit(fixture, PEN);

      button(fixture, 'Supprimer Encre')!.click();
      httpMock
        .expectOne({ method: 'DELETE', url: `${PRODUCTS_URL}/${INK.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, [PEN]);

      expect(nameInput(fixture).value).toBe('Stylo');
      expect(button(fixture, 'Enregistrer')).toBeDefined();
    });
  });
});
