# WebAngular

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.0.0.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

> **Tests note:** Playwright e2e (`npm run e2e`) is local-only and not run in CI (needs the dev server / backend).

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.

## Dépendances — note temporaire

Le fichier `.npmrc` active `legacy-peer-deps=true` temporairement : Angular 22 est sorti le 2026-06-03 et `angular-eslint` n'a pas encore publié de version compatible (`>= 22`). Le flag n'est pas nécessaire pour `@hey-api/openapi-ts` (déjà compatible TS 6).

**Condition de retrait :** supprimer la ligne `legacy-peer-deps=true` dès que `angular-eslint@22` est publié. Vérifier avec `npm outdated angular-eslint`.
