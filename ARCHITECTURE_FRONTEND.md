# TeleBot Management - Frontend Architecture (Management.Frontend)

## Scope

This document describes the current frontend structure, routing, HTTP calling conventions, and the 24-hour login session handling.

## Project Location

Frontend root:

- `d:\CSharpProject\GitHub\TeleBot\Management.Frontend\Management.Frontend`

Tech stack:

- Vue 3 + TypeScript
- Vite
- vue-router
- Pinia
- Axios

## Structure

`src/` 주요 구조 (feature/module oriented):

- `src/main.ts`
  - Creates Vue app
  - Registers Pinia
  - Registers router
- `src/App.vue`
  - `<router-view />`

### `src/app/` (application shell)

- `src/app/router/index.ts`
  - Defines routes
  - Defines auth guard
- `src/app/layouts/MainLayout.vue`
  - Main layout wrapper for authenticated area

### `src/modules/` (feature modules)

- `src/modules/auth/pages/LoginPage.vue`
- `src/modules/auth/pages/RegisterPage.vue`
- `src/modules/dashboard/pages/DashboardPage.vue`

### `src/infrastructure/` (technical layer)

- `src/infrastructure/http/apiClient.ts`
  - Shared base HTTP calling service (axios wrapper)
  - Adds Authorization header
  - Normalizes errors
  - Clears session on 401

Legacy files (should avoid using going forward if standardizing on `apiClient.ts`):

- `src/infrastructure/http/httpClient.ts`
- `src/infrastructure/http/interceptor.ts`

### `src/shared/` (shared utilities)

- `src/shared/auth/jwt.ts`
  - Parse JWT payload
  - Check expiry (`exp`)
  - Clear auth session

### TypeScript declaration

- `src/shims-vue.d.ts`
  - Declares module `*.vue` for TypeScript

## Routing

File: `src/app/router/index.ts`

Current routes:

- `/login`
  - Login page
- `/register`
  - Register page
- `/` (MainLayout, protected)
  - `/dashboard`

### Auth guard

- Protected area uses `meta: { requiresAuth: true }`.
- Guard logic:
  - reads `localStorage.token`
  - if token is missing OR expired -> clears session and redirects to `/login`

## HTTP Calling Convention (Base Call Service)

File: `src/infrastructure/http/apiClient.ts`

### Goals

- All modules call API in a single consistent style
- Centralize baseURL, timeout, token attach, and error normalization

### Base URL / Environment

`apiClient.ts` uses:

- `VITE_API_BASE_URL` if present
- otherwise defaults to: `https://localhost:7179/api`

Recommended: create `.env.local`:

```env
VITE_API_BASE_URL=https://localhost:7179/api
```

### API Client API

`api` provides:

- `api.get<T>(url, config?)`
- `api.post<T>(url, data?, config?)`
- `api.put<T>(url, data?, config?)`
- `api.delete<T>(url, config?)`

All methods return `Promise<T>` and return the **response body** (`res.data`) directly.

### Error handling

`ApiClient` throws a normalized error shape:

- `ApiError { status?: number; message: string; details?: unknown }`

Pages should catch and display `err.message`.

## Authentication / Session (24 hours)

### Storage

- Token is stored in `localStorage` under key: `token`

### Expiry enforcement

Two layers:

1) **Before route navigation**
   - router guard checks `isTokenExpired(token)` (reads `exp` claim)

2) **Before each API request / on 401**
   - `apiClient.ts` request interceptor:
     - attaches Authorization header only if token exists and is not expired
     - if expired -> clears auth session
   - response interceptor:
     - on 401 -> clears auth session

Backend also enforces expiry strictly (ClockSkew=0).

## Auth Pages

### Login

File: `src/modules/auth/pages/LoginPage.vue`

- POST `/auth/login` with `{ login, password }`
- Expects `{ token }`
- Saves token, navigates to `/dashboard`

### Register

File: `src/modules/auth/pages/RegisterPage.vue`

- POST `/auth/register` with `{ username, email?, phone?, fullName?, password }`
- Expects `{ token }`
- Saves token, navigates to `/dashboard`

## Known Issues / TODO

- If standardizing on `apiClient.ts`, consider removing usage/import of legacy `httpClient.ts` and `interceptor.ts`.
- Consider adding a dedicated logout button and redirect logic on 401 (currently it only clears token).
