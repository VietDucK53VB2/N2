# N2.Circulation

Library circulation service with a separated ASP.NET Core backend and Vue frontends.

## Structure

- `backend/`: ASP.NET Core API, EF Core models, controllers, migrations, Dockerfile, and served static files in `backend/wwwroot`.
- `frontend/reader/`: Vue 3 + Vite reader UI source code.
- `frontend/librarian/`: Vue 3 + Vite librarian UI source code.
- `docs/`: shared project/API documentation.

## Run Backend

```bash
cd backend
dotnet run --project N2.Circulation.Api.csproj --urls "http://localhost:5089"
```

## Run Frontend

Reader UI:

```bash
cd frontend/reader
npm install
npm run dev
```

Librarian UI:

```bash
cd frontend/librarian
npm install
npm run dev
```

## Build Frontend Into Backend Static Files

Reader UI:

```bash
cd frontend/reader
npm run build
```

Librarian UI:

```bash
cd frontend/librarian
npm run build
```

The frontend build outputs are written to `backend/wwwroot/ui/reader` and `backend/wwwroot/ui/librarian`.
