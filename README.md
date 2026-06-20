# PrintScan AI

An AI-powered assistant for diagnosing 3D-print defects. Send a description and photos of a failed print; get back an explanation and suggested fixes.

## What it does

- Upload print photos plus a text description and receive an AI diagnosis (vision-capable).
- Accounts with chat history, pinned chats, and a printer profile (printer, filament, slicer) that tailors advice.
- Try-before-signup anonymous mode with a small free quota.

## Stack

- **Backend:** ASP.NET Core minimal API (.NET), PostgreSQL via Npgsql.
- **Frontend:** static HTML/CSS/JS served from `wwwroot/`.
- **Auth:** email/password + Google OAuth, JWT-based.
- **Deploy:** Docker on Railway (`Dockerfile`, `railway.toml`; healthcheck at `/api/health`).

## Configuration

Set these environment variables (locally via `.env`, on Railway via service variables):

- `DATABASE_URL` — PostgreSQL connection string
- AI provider API key
- JWT secret
- Google OAuth client ID + secret
- `PORT` (default `5171`)

## Running

```bash
# local
dotnet run --project LoginDB

# container
docker build -t printscan-ai . && docker run -p 5171:5171 printscan-ai
```

Health check: `GET /api/health`.
