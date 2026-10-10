import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ProductsPage } from './products.page';

const PRODUCTS_URL = '/api/v1/products';
const EMPTY_PAGE = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };

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

  async function open(): Promise<ComponentFixture<ProductsPage>> {
    const fixture = TestBed.createComponent(ProductsPage);
    // La liste se charge à l'affichage ; whenStable attendrait la réponse, que le test fournit.
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url: PRODUCTS_URL }).flush(EMPTY_PAGE);
    await fixture.whenStable();
    return fixture;
  }

  async function submit(fixture: ComponentFixture<ProductsPage>, name: string, price: string) {
    const element = fixture.nativeElement as HTMLElement;
    const nameInput = element.querySelector<HTMLInputElement>('input:not([type=number])')!;
    const priceInput = element.querySelector<HTMLInputElement>('input[type=number]')!;
    nameInput.value = name;
    nameInput.dispatchEvent(new Event('input'));
    priceInput.value = price;
    priceInput.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  // L'API accepte un prix nul (GreaterThanOrEqualTo(0)) : un produit gratuit se crée aussi ici.
  it('creates a product with a price of 0', async () => {
    const fixture = await open();

    await submit(fixture, 'Échantillon', '0');

    const create = httpMock.expectOne({ method: 'POST', url: PRODUCTS_URL });
    expect(create.request.body).toEqual({ name: 'Échantillon', price: 0 });
    create.flush(
      { id: '0199c9a0-0000-7000-8000-000000000002' },
      { status: 201, statusText: 'Created' },
    );
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url: PRODUCTS_URL }).flush(EMPTY_PAGE);
  });

  it.each([
    ['a negative price', '-1'],
    ['an empty price field', ''],
  ])('does not send a product with %s', async (_case, price) => {
    const fixture = await open();

    await submit(fixture, 'Stylo', price);

    httpMock.expectNone({ method: 'POST', url: PRODUCTS_URL });
  });
});
