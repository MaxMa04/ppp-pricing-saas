# PriceParity

[![License](https://img.shields.io/badge/License-BSD_3--Clause-blue.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](docker-compose.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-16-000000?logo=nextdotjs)](https://nextjs.org/)

> Automatically adjust your app subscription prices based on Purchasing Power Parity (PPP) for Apple App Store and Google Play Store.

PriceParity helps app developers optimize their global pricing strategy by suggesting region-specific prices based on the Big Mac Index and World Bank PPP data. Instead of charging the same USD-equivalent price worldwide, you can adjust prices to match local purchasing power - increasing conversions in lower-income regions while maintaining revenue.

## Features

- **Apple App Store Integration** - Connect via App Store Connect API to fetch and manage subscription prices
- **Google Play Integration** - OAuth-based connection to Google Play Developer Console
- **PPP-Based Pricing** - Automatic price suggestions using Big Mac Index / World Bank data
- **Dashboard** - Modern web interface to manage apps, subscriptions, and regional prices
- **Price History** - Audit log of all price changes with timestamps
- **Self-Hostable** - Run on your own infrastructure with Docker

## Quick Start

### Using Docker (Recommended)

1. **Clone the repository**
   ```bash
   git clone https://github.com/MaxMa04/priceparity.git
   cd priceparity
   ```

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   # Edit .env with your Firebase and API credentials
   ```

3. **Add Firebase credentials**

   Download your Firebase Admin SDK JSON from [Firebase Console](https://console.firebase.google.com/) > Project Settings > Service Accounts, and save it as `firebase-credentials.json` in the project root.

4. **Start the containers**
   ```bash
   docker compose up -d
   ```

5. **Access the application**
   - Frontend: http://localhost:3009
   - Backend API: http://localhost:5004
   - Health Check: http://localhost:5004/health

### Manual Installation

#### Prerequisites
- Node.js 22+
- .NET SDK 10+
- Firebase project with Authentication enabled

#### Frontend
```bash
cd frontend
npm install
cp .env.example .env.local
# Edit .env.local with your Firebase config
npm run dev
```

#### Backend
```bash
cd backend
dotnet restore
# Configure appsettings.json with your settings
dotnet run --project PppPricing.API
```

## Configuration

### Firebase Setup

1. Create a new project in [Firebase Console](https://console.firebase.google.com/)
2. Enable **Authentication** with Email/Password provider
3. Go to Project Settings > General > Your apps > Add web app
4. Copy the configuration values to your `.env` file
5. Go to Project Settings > Service Accounts > Generate new private key
6. Save the JSON file as `firebase-credentials.json`

### Google Play API (Optional)

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the **Google Play Android Developer API**
3. Create OAuth 2.0 credentials (Web application)
4. Add `http://localhost:5004/api/google-play/auth/callback` as an authorized redirect URI
5. Copy Client ID and Client Secret to your `.env` file

### App Store Connect API (Optional)

App Store Connect credentials are configured through the dashboard after logging in:

1. Generate an API key in [App Store Connect](https://appstoreconnect.apple.com/) > Users and Access > Integrations > App Store Connect API
2. Note your Issuer ID and Key ID
3. Download the `.p8` private key file
4. Enter these in the dashboard under Settings > App Store Connection

## Architecture

```
┌─────────────────────────────────────────────────┐
│              Docker Compose Stack               │
├─────────────────┬───────────────────────────────┤
│    Frontend     │           Backend             │
│   (Next.js 16)  │         (.NET 10)             │
│   Port: 3009    │         Port: 5004            │
├─────────────────┴───────────────────────────────┤
│               Shared Volume                     │
│     SQLite Database + Firebase Credentials      │
└─────────────────────────────────────────────────┘
```

### Tech Stack

| Layer    | Technology |
|----------|------------|
| Frontend | Next.js 16, TypeScript, Tailwind CSS, shadcn/ui |
| Backend  | ASP.NET Core 10, C#, Entity Framework Core |
| Database | SQLite |
| Auth     | Firebase Authentication |
| Container| Docker, Docker Compose |

## API Documentation

### Authentication
All API endpoints (except `/health`) require a Firebase ID token in the `Authorization` header:
```
Authorization: Bearer <firebase-id-token>
```

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/health` | Health check (no auth required) |
| `POST` | `/api/auth/verify` | Verify Firebase token and sync user |
| `GET` | `/api/apps` | List all connected apps |
| `GET` | `/api/apps/{id}` | Get app details |
| `GET` | `/api/apps/{id}/subscriptions` | List app subscriptions |
| `GET` | `/api/subscriptions/{id}/prices` | Get regional prices |
| `POST` | `/api/subscriptions/{id}/prices/preview` | Preview PPP adjustments |
| `GET` | `/api/ppp/multipliers` | Get all PPP multipliers |
| `PUT` | `/api/ppp/multipliers/{region}` | Update a multiplier |

See [CLAUDE.md](CLAUDE.md) for complete API documentation.

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `NEXT_PUBLIC_FIREBASE_API_KEY` | Firebase Web API Key | Yes |
| `NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN` | Firebase Auth Domain | Yes |
| `NEXT_PUBLIC_FIREBASE_PROJECT_ID` | Firebase Project ID | Yes |
| `NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET` | Firebase Storage Bucket | Yes |
| `NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID` | Firebase Sender ID | Yes |
| `NEXT_PUBLIC_FIREBASE_APP_ID` | Firebase App ID | Yes |
| `NEXT_PUBLIC_API_URL` | Backend API URL | Yes |
| `FIREBASE_PROJECT_ID` | Firebase Project ID (backend) | Yes |
| `FIREBASE_CREDENTIALS_PATH` | Path to service account JSON | Yes |
| `GOOGLE_CLIENT_ID` | Google OAuth Client ID (Docker Compose friendly) | No |
| `GOOGLE_CLIENT_SECRET` | Google OAuth Client Secret (Docker Compose friendly) | No |
| `Google__ClientId` | Google OAuth Client ID (direct `dotnet run`) | No |
| `Google__ClientSecret` | Google OAuth Client Secret (direct `dotnet run`) | No |

## Development

### Project Structure

```
/priceparity
├── frontend/                    # Next.js application
│   ├── app/                     # App Router pages
│   │   ├── (auth)/             # Login, Register
│   │   └── (dashboard)/        # Protected routes
│   ├── components/             # React components
│   └── lib/                    # Utilities, API client
│
├── backend/                     # .NET Solution
│   ├── PppPricing.API/         # Web API project
│   ├── PppPricing.Domain/      # Entity models
│   └── PppPricing.Shared/      # DTOs
│
├── docker-compose.yml          # Container orchestration
├── .env.example                # Environment template
└── CLAUDE.md                   # Development docs
```

### Running Tests

```bash
# Frontend
cd frontend && npm test

# Backend
cd backend && dotnet test
```

### Database Migrations

```bash
cd backend
dotnet ef migrations add <MigrationName> --project PppPricing.API
dotnet ef database update --project PppPricing.API
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the BSD 3-Clause License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Big Mac Index](https://www.economist.com/big-mac-index) by The Economist
- [World Bank PPP Data](https://data.worldbank.org/indicator/PA.NUS.PPP)
- [shadcn/ui](https://ui.shadcn.com/) for the beautiful UI components
