# web-angular

Angular 22 front end of the starter: standalone components, zoneless change detection, signals and Angular Material.

## Run

Start the API first (see the Quickstart in the root `README.md`), then:

```bash
npm ci
npm start   # http://localhost:4200
```

`src/proxy.conf.json` forwards `/api` and `/openapi` to the API on port 5123, so the front end and the API share one origin. The refresh-token cookie relies on it (`docs/adr/0004`).

## Layout

| Folder                         | Content                                                                                                                                                                             |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/core/auth`            | Auth service (access token in memory, refresh through the HttpOnly cookie), route guard, interceptor that refreshes the token on a 401 (one shared refresh for concurrent requests) |
| `src/app/core/error`           | Global error handler and HTTP error interceptor                                                                                                                                     |
| `src/app/features/auth`        | Login, registration, e-mail confirmation, forgotten and reset password pages                                                                                                        |
| `src/app/features/products`    | Products page, the sample tenant-scoped feature                                                                                                                                     |
| `src/app/features/invitations` | Invitation acceptance page, opened from the link of the invitation e-mail                                                                                                           |
| `src/app/api/generated`        | TypeScript types generated from `openapi.json` (not versioned)                                                                                                                      |

## API types

`openapi.json` is the versioned API contract, written by the API build (`dotnet build src/Api`); CI fails when it is out of date. `npm run api:gen` generates the TypeScript types from it with Hey API; `npm start`, `npm test`, `npm run test:coverage`, `npm run watch`, `npm run check` and `npm run build` run it first; the other scripts expect the types to be generated already.

## Commands

| Command         | Purpose                                                                                                                           |
| --------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `npm run check` | CI gate: type generation, Prettier, ESLint, Stylelint, type check                                                                 |
| `npm run fix`   | Prettier, ESLint and Stylelint fixes                                                                                              |
| `npm test`      | Unit tests (Vitest)                                                                                                               |
| `npm run e2e`   | Playwright tests of the login and invitation acceptance pages with axe accessibility checks; the API is mocked, no backend needed |
| `npm run build` | Production build in `dist/`                                                                                                       |
