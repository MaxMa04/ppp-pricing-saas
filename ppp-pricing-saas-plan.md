# PPP Pricing SaaS - Implementation Plan

> ⚠️ **WICHTIG FÜR CLAUDE CODE:**
> Die Code-Beispiele und der Tech Stack in diesem Dokument sind **Empfehlungen/Referenzen**.
> Orientiere dich primär an dem bestehenden Boilerplate/Projekt das dir gegeben wird!
> Nutze diesen Plan für:
> - API Endpoint Strukturen und Logik
> - Datenbank-Schema Design
> - Externe API Dokumentations-Links
> - Feature-Anforderungen und Flows
> 
> Passe den Code an den existierenden Stil und die Struktur des Projekts an.

## Project Overview

A SaaS tool that allows app developers to automatically adjust their subscription prices based on Purchasing Power Parity (PPP) / Big Mac Index for both Apple App Store and Google Play Store.

**Tech Stack (Empfehlung - anpassen an bestehendes Projekt):**
- Frontend: Next.js (App Router)
- Backend: .NET 8 Web API
- Auth: Firebase Authentication
- Database: SQLite
- Hosting: Coolify

**Business Model:** Source Available (public repo for trust) with paid hosted version

---

## Phase 1: Project Setup & Authentication

### 1.1 Firebase Auth Setup

**Documentation:**
- Firebase Web SDK: https://firebase.google.com/docs/auth/web/start
- Firebase Admin SDK (.NET): https://firebase.google.com/docs/admin/setup
- Firebase ID Token Verification: https://firebase.google.com/docs/auth/admin/verify-id-tokens

**Frontend (Next.js) Tasks:**
1. Install Firebase SDK: `npm install firebase`
2. Create `/lib/firebase.ts` with Firebase config
3. Implement auth context provider
4. Create login page with Google OAuth and Email/Password
5. Protect routes with middleware

**Backend (.NET) Tasks:**
1. Install `FirebaseAdmin` NuGet package
2. Create Firebase Auth middleware to verify ID tokens
3. Extract user ID from token for multi-tenancy

**Firebase Config Required:**
```env
# Frontend (.env.local)
NEXT_PUBLIC_FIREBASE_API_KEY=
NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN=
NEXT_PUBLIC_FIREBASE_PROJECT_ID=

# Backend (appsettings.json or env)
FIREBASE_PROJECT_ID=
# Store service account JSON securely
```

**Code Example - Token Verification (.NET):**
```csharp
// Middleware to verify Firebase token
public class FirebaseAuthMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            var token = authHeader.Substring(7);
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
            context.Items["UserId"] = decodedToken.Uid;
        }
    }
}
```

---

## Phase 2: Database Schema

### 2.1 SQLite Schema

**Documentation:**
- EF Core with SQLite: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/
- SQLite Data Types: https://www.sqlite.org/datatype3.html

**Hinweis:** SQLite hat keine nativen UUID/BYTEA Typen - verwende TEXT für UUIDs und BLOB für verschlüsselte Daten.

**Tables:**

```sql
-- Users (synced from Firebase)
CREATE TABLE users (
    id TEXT PRIMARY KEY, -- UUID as TEXT
    firebase_uid TEXT UNIQUE NOT NULL,
    email TEXT NOT NULL,
    display_name TEXT,
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- Connected Store Accounts
CREATE TABLE store_connections (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    store_type TEXT NOT NULL, -- 'google_play' or 'app_store'
    
    -- Google Play: OAuth tokens (encrypted)
    google_access_token_encrypted BLOB,
    google_refresh_token_encrypted BLOB,
    google_token_expiry TEXT,
    
    -- App Store: API Key details (encrypted)
    apple_key_id TEXT,
    apple_issuer_id TEXT,
    apple_private_key_encrypted BLOB,
    
    is_active INTEGER DEFAULT 1,
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now')),
    
    UNIQUE(user_id, store_type)
);

-- Apps tracked by user
CREATE TABLE apps (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    store_connection_id TEXT NOT NULL REFERENCES store_connections(id) ON DELETE CASCADE,
    store_type TEXT NOT NULL,
    
    -- Identifiers
    package_name TEXT, -- Google: com.example.app
    bundle_id TEXT,    -- Apple: com.example.app
    app_store_id TEXT, -- Apple: numeric ID
    
    app_name TEXT,
    icon_url TEXT,
    
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- Subscriptions within apps
CREATE TABLE subscriptions (
    id TEXT PRIMARY KEY,
    app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    
    -- Store identifiers
    product_id TEXT NOT NULL, -- Google: subscription ID, Apple: product ID
    base_plan_id TEXT,        -- Google only
    
    name TEXT,
    billing_period TEXT, -- 'monthly', 'yearly', etc.
    
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- Current prices per region
CREATE TABLE subscription_prices (
    id TEXT PRIMARY KEY,
    subscription_id TEXT NOT NULL REFERENCES subscriptions(id) ON DELETE CASCADE,
    
    region_code TEXT NOT NULL,  -- ISO 3166-1 alpha-2/3
    currency_code TEXT NOT NULL,
    
    current_price REAL,
    ppp_suggested_price REAL,
    ppp_multiplier REAL,
    
    last_synced_at TEXT,
    last_updated_at TEXT,
    
    UNIQUE(subscription_id, region_code)
);

-- PPP multipliers (your data)
CREATE TABLE ppp_multipliers (
    id TEXT PRIMARY KEY,
    region_code TEXT UNIQUE NOT NULL,
    country_name TEXT,
    multiplier REAL NOT NULL, -- e.g., 0.45 for 45% of base price
    source TEXT, -- 'big_mac_index', 'world_bank', 'custom'
    updated_at TEXT DEFAULT (datetime('now'))
);

-- Price change history / audit log
CREATE TABLE price_changes (
    id TEXT PRIMARY KEY,
    subscription_id TEXT NOT NULL REFERENCES subscriptions(id) ON DELETE CASCADE,
    user_id TEXT REFERENCES users(id),
    
    region_code TEXT NOT NULL,
    old_price REAL,
    new_price REAL,
    currency_code TEXT,
    
    change_type TEXT, -- 'ppp_adjustment', 'manual', 'sync'
    status TEXT, -- 'pending', 'applied', 'failed'
    error_message TEXT,
    
    created_at TEXT DEFAULT (datetime('now')),
    applied_at TEXT
);
```

---

## Phase 3: Google Play API Integration

### 3.1 OAuth Flow for Google Play Console

**Documentation:**
- Google Play Developer API: https://developers.google.com/android-publisher
- OAuth 2.0 for Web: https://developers.google.com/identity/protocols/oauth2/web-server
- Monetization API Reference: https://developers.google.com/android-publisher/api-ref/rest/v3/monetization.subscriptions
- Convert Region Prices: https://developers.google.com/android-publisher/api-ref/rest/v3/monetization/convertRegionPrices
- Migrate Prices: https://developers.google.com/android-publisher/api-ref/rest/v3/monetization.subscriptions.basePlans/migratePrices
- Quotas: https://developers.google.com/android-publisher/quotas

**Required Scopes:**
```
https://www.googleapis.com/auth/androidpublisher
```

**OAuth Flow:**
1. User clicks "Connect Google Play" in dashboard
2. Redirect to Google OAuth consent screen
3. User grants access to their Play Console
4. Receive authorization code
5. Exchange for access + refresh tokens
6. Store encrypted tokens in database

**Backend Endpoints:**
```
GET  /api/google-play/auth/url        - Generate OAuth URL
POST /api/google-play/auth/callback   - Handle OAuth callback
GET  /api/google-play/apps            - List user's apps
GET  /api/google-play/apps/{id}/subscriptions - List subscriptions
GET  /api/google-play/subscriptions/{id}/prices - Get regional prices
POST /api/google-play/subscriptions/{id}/prices - Update prices
POST /api/google-play/subscriptions/{id}/preview - Dry-run price calculation
```

**Key API Calls:**

```csharp
// List subscriptions
GET https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions

// Get subscription details with regional prices
GET https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions/{productId}

// Update subscription prices
PATCH https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions/{productId}
Content-Type: application/json

{
  "basePlans": [{
    "basePlanId": "monthly",
    "regionalConfigs": [
      {
        "regionCode": "ZA",
        "price": {
          "currencyCode": "ZAR",
          "units": "49",
          "nanos": 990000000
        }
      }
    ]
  }]
}

// Batch update (up to 100 subscriptions)
POST https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions:batchUpdate

// Convert base price to regional prices (Google's suggestion)
POST https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/pricing:convertRegionPrices
{
  "price": {
    "currencyCode": "EUR",
    "units": "9",
    "nanos": 990000000
  }
}
```

**Rate Limits:**
- 3,000 queries per minute
- 200,000 queries per day (resets midnight PT)

---

## Phase 4: Apple App Store Connect API Integration

### 4.1 API Key Authentication

**Documentation:**
- App Store Connect API: https://developer.apple.com/documentation/appstoreconnectapi
- Creating API Keys: https://developer.apple.com/documentation/appstoreconnectapi/creating_api_keys_for_app_store_connect_api
- Generating Tokens: https://developer.apple.com/documentation/appstoreconnectapi/generating_tokens_for_api_requests
- Subscription Price Points: https://developer.apple.com/documentation/appstoreconnectapi/subscriptionpricepoints
- Subscription Prices: https://developer.apple.com/documentation/appstoreconnectapi/subscriptionprices
- Price Point Reference: https://developer.apple.com/help/app-store-connect/manage-subscriptions/manage-pricing-for-auto-renewable-subscriptions

**User Provides:**
1. Issuer ID (from App Store Connect → Users and Access → Integrations)
2. Key ID (from same location)
3. Private Key (.p8 file contents)

**JWT Token Generation (.NET):**
```csharp
public string GenerateAppStoreConnectToken(string keyId, string issuerId, string privateKey)
{
    var now = DateTimeOffset.UtcNow;
    var claims = new Dictionary<string, object>
    {
        { "iss", issuerId },
        { "iat", now.ToUnixTimeSeconds() },
        { "exp", now.AddMinutes(20).ToUnixTimeSeconds() },
        { "aud", "appstoreconnect-v1" }
    };
    
    // Use ES256 algorithm with the private key
    // Library: System.IdentityModel.Tokens.Jwt or jose-jwt
    return JWT.Encode(claims, privateKey, JwsAlgorithm.ES256, 
        new Dictionary<string, object> { { "kid", keyId } });
}
```

**Backend Endpoints:**
```
POST /api/app-store/connect           - Save API credentials
GET  /api/app-store/apps              - List user's apps  
GET  /api/app-store/apps/{id}/subscriptions - List subscriptions
GET  /api/app-store/subscriptions/{id}/prices - Get regional prices
GET  /api/app-store/subscriptions/{id}/price-points - Get available price points
POST /api/app-store/subscriptions/{id}/prices - Update prices
POST /api/app-store/subscriptions/{id}/preview - Dry-run calculation
```

**Key API Calls:**

```http
# List apps
GET https://api.appstoreconnect.apple.com/v1/apps
Authorization: Bearer {jwt_token}

# List subscriptions for an app
GET https://api.appstoreconnect.apple.com/v1/apps/{appId}/subscriptionGroups
GET https://api.appstoreconnect.apple.com/v1/subscriptionGroups/{groupId}/subscriptions

# Get available price points for a subscription
GET https://api.appstoreconnect.apple.com/v1/subscriptions/{subscriptionId}/pricePoints?filter[territory]=DEU,USA,ZAF

# Get current prices
GET https://api.appstoreconnect.apple.com/v1/subscriptions/{subscriptionId}/prices

# Get equalizations (Apple's suggested equivalent prices)
GET https://api.appstoreconnect.apple.com/v1/subscriptionPricePoints/{pricePointId}/equalizations

# Set price for a territory
POST https://api.appstoreconnect.apple.com/v1/subscriptionPrices
Content-Type: application/json

{
  "data": {
    "type": "subscriptionPrices",
    "attributes": {
      "preserveCurrentPrice": false,
      "startDate": null
    },
    "relationships": {
      "subscription": {
        "data": { "type": "subscriptions", "id": "{subscriptionId}" }
      },
      "subscriptionPricePoint": {
        "data": { "type": "subscriptionPricePoints", "id": "{pricePointId}" }
      },
      "territory": {
        "data": { "type": "territories", "id": "ZAF" }
      }
    }
  }
}
```

**Rate Limits:**
- ~300 requests per minute
- ~3,600 requests per hour
- Implement 200ms delay between calls for bulk updates

**Important Notes:**
- Apple has ~900 predefined price points (not arbitrary prices)
- You must find the closest price point to your PPP-calculated price
- JWT tokens expire after 20 minutes - regenerate for long operations

---

## Phase 5: PPP Calculation Engine

### 5.1 Core Logic

**Documentation:**
- Big Mac Index Data: https://github.com/TheEconomist/big-mac-data
- World Bank PPP Data: https://data.worldbank.org/indicator/PA.NUS.PPP

**Service Interface:**
```csharp
public interface IPppCalculationService
{
    // Calculate PPP-adjusted price for a region
    decimal CalculatePppPrice(decimal basePrice, string baseCurrency, string targetRegion);
    
    // Get all regional prices from a base price
    Dictionary<string, RegionalPrice> CalculateAllRegionalPrices(
        decimal basePrice, 
        string baseCurrency,
        PppStrategy strategy = PppStrategy.BigMacIndex
    );
    
    // Find closest Apple price point
    ApplePricePoint FindClosestPricePoint(decimal targetPrice, string territory, List<ApplePricePoint> availablePoints);
    
    // Preview changes without applying
    PriceChangePreview PreviewPriceChanges(Subscription subscription);
}

public class RegionalPrice
{
    public string RegionCode { get; set; }
    public string CurrencyCode { get; set; }
    public decimal OriginalPrice { get; set; }      // Exchange rate only
    public decimal PppAdjustedPrice { get; set; }   // With PPP multiplier
    public decimal Multiplier { get; set; }
    public decimal? ApplePricePoint { get; set; }   // Rounded to Apple tier
    public decimal? GooglePrice { get; set; }       // Exact price for Google
}
```

**Algorithm:**
```csharp
public decimal CalculatePppPrice(decimal basePrice, string baseCurrency, string targetRegion)
{
    // 1. Get PPP multiplier for target region
    var multiplier = _pppMultipliers[targetRegion]; // e.g., 0.35 for South Africa
    
    // 2. Get exchange rate
    var exchangeRate = _exchangeRateService.GetRate(baseCurrency, GetCurrency(targetRegion));
    
    // 3. Calculate: BasePrice * ExchangeRate * PPP_Multiplier
    var pppPrice = basePrice * exchangeRate * multiplier;
    
    // 4. Round to local conventions (e.g., .99 ending)
    return RoundToLocalConvention(pppPrice, targetRegion);
}
```

---

## Phase 6: Frontend Dashboard

### 6.1 Pages Structure

**Documentation:**
- Next.js App Router: https://nextjs.org/docs/app
- Firebase Auth with Next.js: https://firebase.google.com/docs/auth/web/start
- shadcn/ui: https://ui.shadcn.com/

**Page Structure:**
```
/app
  /page.tsx                    - Landing page
  /login/page.tsx              - Login/Register
  /dashboard
    /page.tsx                  - Overview
    /connections/page.tsx      - Connect Google/Apple
    /apps/page.tsx             - List connected apps
    /apps/[id]/page.tsx        - App detail with subscriptions
    /apps/[id]/subscriptions/[subId]/page.tsx - Subscription pricing
    /settings/page.tsx         - Account settings
    /ppp-multipliers/page.tsx  - View/edit PPP data
```

**Key Components:**
```
/components
  /auth
    LoginForm.tsx
    AuthProvider.tsx
  /dashboard
    AppCard.tsx
    SubscriptionCard.tsx
    PriceTable.tsx              - Shows prices per region
    PricePreview.tsx            - Dry-run preview
    BulkUpdateButton.tsx
  /connections
    GooglePlayConnect.tsx       - OAuth flow trigger
    AppStoreConnect.tsx         - API key upload form
  /ui
    (shadcn components)
```

### 6.2 Key Features UI

**Price Table View:**
- Show all regions in a sortable table
- Columns: Region, Current Price, PPP Suggested, Difference %, Action
- Color coding: Green (within 10%), Yellow (10-30% off), Red (>30% off)
- Bulk select regions for update

**Dry Run / Preview:**
- Button: "Preview PPP Adjustments"
- Shows what would change before applying
- Summary: X prices will increase, Y will decrease, Z unchanged

**Update Flow:**
1. User selects subscription
2. Clicks "Apply PPP Pricing"
3. Shows preview with confirmation
4. Option: "Preserve prices for existing subscribers" (checkbox)
5. Progress indicator during API calls
6. Success/failure report

---

## Phase 7: API Endpoints Summary

### 7.1 .NET Backend Structure

```
/src
  /Api
    /Controllers
      AuthController.cs
      GooglePlayController.cs
      AppStoreController.cs
      AppsController.cs
      SubscriptionsController.cs
      PppController.cs
    /Middleware
      FirebaseAuthMiddleware.cs
      RateLimitingMiddleware.cs
  /Core
    /Entities
      User.cs
      StoreConnection.cs
      App.cs
      Subscription.cs
      SubscriptionPrice.cs
      PppMultiplier.cs
      PriceChange.cs
    /Interfaces
      IGooglePlayService.cs
      IAppStoreService.cs
      IPppCalculationService.cs
    /Services
      GooglePlayService.cs
      AppStoreService.cs
      PppCalculationService.cs
      EncryptionService.cs
  /Infrastructure
    /Data
      AppDbContext.cs  # EF Core mit SQLite Provider
      Migrations/
    /ExternalServices
      GooglePlayApiClient.cs
      AppStoreConnectApiClient.cs
      ExchangeRateService.cs
```

### 7.2 Full API Endpoint List

```yaml
# Auth
POST /api/auth/verify              # Verify Firebase token, create/update user

# Store Connections
GET  /api/connections              # List user's store connections
DELETE /api/connections/{id}       # Remove connection

# Google Play
GET  /api/google-play/auth/url     # Get OAuth URL
GET  /api/google-play/auth/callback # OAuth callback
GET  /api/google-play/apps         # List apps from Play Console
POST /api/google-play/apps/{packageName}/sync # Sync app data

# App Store
POST /api/app-store/connect        # Save API key credentials
GET  /api/app-store/apps           # List apps
POST /api/app-store/apps/{id}/sync # Sync app data

# Apps (unified)
GET  /api/apps                     # List all connected apps
GET  /api/apps/{id}                # Get app details
GET  /api/apps/{id}/subscriptions  # List subscriptions

# Subscriptions
GET  /api/subscriptions/{id}                    # Get subscription details
GET  /api/subscriptions/{id}/prices             # Get current regional prices
POST /api/subscriptions/{id}/prices/preview     # Preview PPP adjustments
POST /api/subscriptions/{id}/prices/apply       # Apply price changes
GET  /api/subscriptions/{id}/prices/history     # Price change history

# PPP Data
GET  /api/ppp/multipliers          # Get all PPP multipliers
PUT  /api/ppp/multipliers/{region} # Update multiplier (admin)
POST /api/ppp/multipliers/import   # Bulk import from CSV/JSON
```

---

## Phase 8: Security Considerations

### 8.1 Credential Encryption

**Documentation:**
- .NET Data Protection: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction
- AES Encryption: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes

**Implementation:**
```csharp
public class EncryptionService
{
    private readonly byte[] _key; // From environment variable, NOT in code
    
    public byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Prepend IV to encrypted data
        return aes.IV.Concat(encryptedBytes).ToArray();
    }
    
    public string Decrypt(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = encryptedData.Take(16).ToArray();
        
        using var decryptor = aes.CreateDecryptor();
        var encryptedBytes = encryptedData.Skip(16).ToArray();
        var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

**Environment Variables Required:**
```env
ENCRYPTION_KEY=<32-byte-base64-encoded-key>
DATABASE_PATH=/data/ppppricing.db  # SQLite file path
FIREBASE_PROJECT_ID=...
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
```

### 8.2 Security Checklist

- [ ] All API credentials encrypted at rest (AES-256)
- [ ] Encryption key stored in environment, not code
- [ ] Firebase tokens validated on every request
- [ ] User can only access their own data (tenant isolation)
- [ ] Rate limiting on all endpoints
- [ ] HTTPS enforced
- [ ] CORS configured for frontend domain only
- [ ] API keys never logged
- [ ] Audit log for all price changes

---

## Phase 9: Deployment

### 9.1 Coolify Setup

**Services to Deploy:**
1. **Frontend (Next.js)** - Static/SSR
2. **Backend (.NET)** - Docker container
3. **SQLite** - File-based, im Backend Container (Volume für Persistenz)
4. **Redis** (optional) - For caching exchange rates

**Docker Compose Example (Empfehlung):**
```yaml
version: '3.8'
services:
  frontend:
    build: ./frontend
    environment:
      - NEXT_PUBLIC_API_URL=https://api.ppppricing.com
      - NEXT_PUBLIC_FIREBASE_API_KEY=${FIREBASE_API_KEY}
    ports:
      - "3000:3000"

  backend:
    build: ./backend
    environment:
      - DATABASE_PATH=/data/ppppricing.db
      - ENCRYPTION_KEY=${ENCRYPTION_KEY}
      - FIREBASE_PROJECT_ID=${FIREBASE_PROJECT_ID}
      - GOOGLE_CLIENT_ID=${GOOGLE_CLIENT_ID}
      - GOOGLE_CLIENT_SECRET=${GOOGLE_CLIENT_SECRET}
    volumes:
      - sqlite_data:/data  # Persistenter Storage für SQLite
    ports:
      - "5000:5000"

volumes:
  sqlite_data:
```

**SQLite Hinweise:**
- Für kleine bis mittlere User-Zahlen völlig ausreichend
- Kein separater DB-Container nötig = weniger Komplexität
- Backup: Einfach die .db Datei kopieren
- Bei Bedarf später auf PostgreSQL migrieren

---

## Phase 10: Implementation Order

### Sprint 1: Foundation (Week 1)
1. [ ] Set up Next.js project with Firebase Auth
2. [ ] Set up .NET project with Firebase token verification
3. [ ] Create database schema and EF Core migrations
4. [ ] Basic auth flow working end-to-end

### Sprint 2: Google Play Integration (Week 2)
1. [ ] Google OAuth flow
2. [ ] List apps from Play Console
3. [ ] List subscriptions and current prices
4. [ ] PPP calculation service
5. [ ] Preview price changes
6. [ ] Apply price changes

### Sprint 3: Apple Integration (Week 3)
1. [ ] API key upload and storage
2. [ ] JWT token generation
3. [ ] List apps and subscriptions
4. [ ] Map prices to price points
5. [ ] Apply price changes

### Sprint 4: Polish & Launch (Week 4)
1. [ ] Dashboard UI polish
2. [ ] Price change history
3. [ ] Bulk operations
4. [ ] Error handling & retry logic
5. [ ] Documentation
6. [ ] Deploy to Coolify

---

## Quick Reference Links

### Google Play
- API Explorer: https://developers.google.com/android-publisher/api-ref/rest
- OAuth Playground: https://developers.google.com/oauthplayground/
- Subscription Docs: https://developer.android.com/google/play/billing

### Apple App Store
- API Reference: https://developer.apple.com/documentation/appstoreconnectapi
- API Keys: https://appstoreconnect.apple.com/access/integrations/api
- Subscription Management: https://developer.apple.com/help/app-store-connect/manage-subscriptions

### Firebase
- Console: https://console.firebase.google.com/
- Auth Docs: https://firebase.google.com/docs/auth
- Admin SDK: https://firebase.google.com/docs/admin/setup

### PPP Data Sources
- Big Mac Index: https://github.com/TheEconomist/big-mac-data
- World Bank: https://data.worldbank.org/indicator/PA.NUS.PPP
