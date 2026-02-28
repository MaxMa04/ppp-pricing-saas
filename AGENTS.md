# AGENTS.md

This file defines default guidance for AI/code agents working in this repository.

## Scope

- Applies to the whole repository (`/Users/maxmannstein/Coding/PricingSaaS`).
- If a deeper `AGENTS.md` exists in a subfolder, the deeper file has precedence for that subtree.

## Project Summary

- Product: PPP pricing SaaS for App Store and Google Play.
- Frontend: Next.js 16, TypeScript, Tailwind, shadcn/ui.
- Backend: ASP.NET Core 10, Entity Framework Core, SQLite.
- Auth: Firebase (frontend SDK + backend token verification).

## Tech Stack

- Web: Next.js 16 + TypeScript.
- API: ASP.NET Core 10 + EF Core + SQLite.
- UI: Tailwind + shadcn/ui.
- Integrations: Firebase Auth, Google Play Developer APIs, App Store APIs.

## Project Structure

- `frontend/`: Next.js app.
- `backend/`: .NET API (`PppPricing.API`).
- Root-level deployment and compose configuration.

## Working Style

- Keep changes minimal and targeted to the user request.
- Preserve existing patterns and folder structure.
- Do not refactor unrelated code unless explicitly requested.
- Prefer clear, maintainable code over clever shortcuts.

## Setup And Commands

- Root services (Docker): `docker compose up -d`
- Frontend dev:
  - `cd frontend`
  - `npm install`
  - `npm run dev` (default: `http://localhost:3009`)
- Backend dev:
  - `cd backend`
  - `dotnet restore`
  - `dotnet run --project PppPricing.API` (default: `http://localhost:5004`)

## Validation Before Handover

Run relevant checks for touched areas:

- Frontend:
  - `cd frontend && npm run lint`
  - `cd frontend && npm run build` (for production-impacting changes)
- Backend:
  - `cd backend && dotnet build`
  - `cd backend && dotnet test` (if tests exist / are affected)

If a command cannot be run, state it explicitly in the handover.

## Code Conventions

- TypeScript/React:
  - Follow existing App Router patterns under `frontend/app`.
  - Use existing UI primitives in `frontend/components/ui` before adding new ones.
- C#/.NET:
  - Keep controller/service/data layering intact.
  - Keep DTOs/contracts in shared/domain projects consistent with existing conventions.
- Database:
  - For schema changes, add EF migrations under backend conventions.

## Security And Secrets

- Never commit secrets or private keys.
- Keep credential files (for example Firebase Admin JSON) out of git-tracked changes.
- Use `.env.example` as template; do not hardcode credentials.

## Out-Of-Scope Guardrails

- Do not modify deployment/infra defaults (ports, docker compose, auth flow) unless requested.
- Do not rename public API routes or core data models without explicit instruction.

## Done Criteria

- Requested change implemented.
- Relevant lint/build/tests executed (or documented why not).
- No unrelated file churn.
- Brief summary of changed files and behavior impact.
