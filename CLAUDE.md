# PPP Pricing SaaS - Claude Code Documentation

## Context Maintenance

- Der operative Projektkontext wird in der zentralisierten `AGENTS.md`-Datei des jeweiligen Repositories gepflegt.
- Wenn sich Architektur, Commands, Tech-Stack oder Agent-Regeln ändern, pflege diese Anpassungen primär in `AGENTS.md`.
- `CLAUDE.md`/`Cloud.md` kann ergänzende Details enthalten, aber `AGENTS.md` ist die führende Quelle für Agenten-Kontext.

## Project Overview

A SaaS tool that allows app developers to automatically adjust their subscription prices based on Purchasing Power Parity (PPP) / Big Mac Index for both Apple App Store and Google Play Store.

**GitHub:** https://github.com/MaxMa04/ppp-pricing-saas

## Tech Stack

### Frontend
- **Framework:** Next.js 16 (App Router)
- **Language:** TypeScript
- **Styling:** Tailwind CSS + shadcn/ui components
- **Auth:** Firebase Authentication
- **UI Libraries:** lucide-react, sonner (toasts), next-themes
- **State:** React hooks + context

### Backend
- **Framework:** ASP.NET Core 10
- **Language:** C#
- **Database:** SQLite with Entity Framework Core 10
- **Auth:** Firebase Admin SDK (token verification)
- **APIs:** Google Play Developer API, App Store Connect API

### Design
- **Primary Color:** HSL(217, 91%, 60%) - Clean blue accent
- **Dark Mode:** Fully supported via next-themes

## Project Structure

```
/PricingSaaS
├── frontend/                    # Next.js application
│   ├── app/                     # App Router pages
│   │   ├── (auth)/             # Auth routes (login, register)
│   │   ├── (dashboard)/        # Protected dashboard routes
│   │   │   └── dashboard/      # Dashboard pages
│   │   │       ├── page.tsx    # Overview
│   │   │       ├── connections/# Store connections
│   │   │       ├── apps/       # Apps list & detail
│   │   │       ├── ppp/        # PPP multipliers
│   │   │       └── settings/   # User settings
│   │   └── page.tsx            # Landing page
│   ├── components/             # React components
│   │   ├── ui/                 # shadcn/ui components
│   │   ├── auth/               # Login/Register forms
│   │   └── providers/          # Context providers
│   └── lib/                    # Utilities
│       ├── api/                # API client
│       ├── firebase/           # Auth config & context
│       └── utils.ts            # Helpers
│
├── backend/                     # .NET Solution
│   ├── PppPricing.API/         # Web API
│   │   ├── Controllers/        # 7 controllers
│   │   ├── Middleware/         # Firebase auth
│   │   ├── Services/           # Business logic
│   │   ├── Data/               # DbContext
│   │   └── Migrations/         # EF migrations
│   ├── PppPricing.Domain/      # Entity models
│   └── PppPricing.Shared/      # DTOs
│
├── CLAUDE.md                   # This file
└── .gitignore
```

## Ports

| Component | Port | Config Location |
|-----------|------|-----------------|
| Frontend | 3009 | `frontend/package.json` |
| Backend | 5004 | `backend/PppPricing.API/appsettings.json` |

## Quick Start

### Frontend
```bash
cd frontend
npm install
npm run dev          # http://localhost:3009
```

### Backend
```bash
cd backend
dotnet restore
dotnet run --project PppPricing.API   # http://localhost:5004
```

## Environment Variables

### Frontend (.env.local)
```env
NEXT_PUBLIC_API_URL=http://localhost:5004
NEXT_PUBLIC_FIREBASE_API_KEY=<your-firebase-api-key>
NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN=<your-project-id>.firebaseapp.com
NEXT_PUBLIC_FIREBASE_PROJECT_ID=<your-project-id>
NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET=<your-project-id>.firebasestorage.app
NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID=<your-sender-id>
NEXT_PUBLIC_FIREBASE_APP_ID=<your-app-id>
```

### Backend (appsettings.json)
```json
{
  "DatabasePath": "ppppricing.db",
  "Firebase": {
    "ProjectId": "<your-project-id>",
    "CredentialPath": ""  // Set via GOOGLE_APPLICATION_CREDENTIALS env var
  }
}
```

## API Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/verify` | Verify Firebase token, sync user |

### Connections
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/connections` | List store connections |
| DELETE | `/api/connections/{id}` | Remove connection |

### Google Play
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/google-play/auth/url` | Get OAuth URL |
| POST | `/api/google-play/auth/callback` | OAuth callback |
| GET | `/api/google-play/apps` | List apps |
| POST | `/api/google-play/apps/{pkg}/sync` | Sync app |

### App Store
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app-store/connect` | Save API credentials |
| GET | `/api/app-store/apps` | List apps |
| POST | `/api/app-store/apps/{id}/sync` | Sync app |

### Apps & Subscriptions
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/apps` | List all apps |
| GET | `/api/apps/{id}` | Get app details |
| GET | `/api/apps/{id}/subscriptions` | List subscriptions |
| GET | `/api/subscriptions/{id}` | Get subscription |
| GET | `/api/subscriptions/{id}/prices` | Get regional prices |
| POST | `/api/subscriptions/{id}/prices/preview` | Preview PPP |
| GET | `/api/subscriptions/{id}/prices/history` | Price history |

### PPP Data
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/ppp/multipliers` | Get all multipliers |
| GET | `/api/ppp/multipliers/{region}` | Get one |
| PUT | `/api/ppp/multipliers/{region}` | Update |
| POST | `/api/ppp/multipliers/import` | Bulk import |

## Database Schema

### Tables
- **Users** - Firebase UID, email, display name
- **StoreConnections** - OAuth tokens (Google) / API keys (Apple)
- **Apps** - Package name, bundle ID, store type
- **Subscriptions** - Product ID, base plan, billing period
- **SubscriptionPrices** - Current price, PPP suggested, region
- **PppMultipliers** - Region code, multiplier, source
- **PriceChanges** - Audit log of all price changes

## Key Files

### Frontend
- `lib/firebase/context.tsx` - Auth state provider
- `lib/api/client.ts` - API client with all endpoints
- `app/(dashboard)/layout.tsx` - Dashboard sidebar
- `components/ui/*.tsx` - Reusable UI components

### Backend
- `Program.cs` - DI, CORS, Firebase init
- `Middleware/FirebaseAuthMiddleware.cs` - Token verification
- `Data/ApplicationDbContext.cs` - EF Core setup
- `Services/PppCalculationService.cs` - Price calculation

## Development Notes

1. **Firebase Auth Flow:**
   - Frontend signs in via Firebase SDK
   - Sends ID token to backend in Authorization header
   - Backend verifies token, creates/updates user

2. **Store Integration:**
   - Google Play: OAuth 2.0 with `androidpublisher` scope
   - App Store: JWT with ES256 algorithm (20 min expiry)

3. **PPP Calculation:**
   - Uses Big Mac Index / World Bank data
   - Multiplier × Exchange Rate × Base Price
   - Apple: Find closest price point from ~900 tiers
   - Google: Set arbitrary prices

4. **Rate Limits:**
   - Google Play: 3,000 queries/minute
   - App Store: ~300 requests/minute (add 200ms delay)

## Useful Commands

```bash
# Frontend
npm run dev              # Dev server
npm run build            # Production build
npm run lint             # ESLint

# Backend
dotnet build             # Build solution
dotnet run --project PppPricing.API
dotnet ef migrations add <Name> --project PppPricing.API
dotnet ef database update --project PppPricing.API

# Git
git status
git add .
git commit -m "message"
git push
```
