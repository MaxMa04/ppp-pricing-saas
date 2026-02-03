# syntax=docker/dockerfile:1

# ===== Stage 1: Restore =====
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY PppPricing.API/*.csproj ./PppPricing.API/
COPY PppPricing.Domain/*.csproj ./PppPricing.Domain/
COPY PppPricing.Shared/*.csproj ./PppPricing.Shared/

RUN dotnet restore

# ===== Stage 2: Build =====
FROM restore AS build
WORKDIR /src

# Copy all source files
COPY . .

RUN dotnet build -c Release --no-restore

# ===== Stage 3: Publish =====
FROM build AS publish

RUN dotnet publish PppPricing.API/PppPricing.API.csproj -c Release -o /app/publish --no-build

# ===== Stage 4: Final =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Install curl for healthcheck
RUN apk add --no-cache curl

# Create non-root user
RUN addgroup --system --gid 1001 dotnet
RUN adduser --system --uid 1001 appuser

# Create data directory for SQLite and keys
RUN mkdir -p /app/data /app/keys && chown -R appuser:dotnet /app/data /app/keys

# Copy published app
COPY --from=publish --chown=appuser:dotnet /app/publish .

USER appuser

EXPOSE 5004

ENV ASPNETCORE_URLS=http://+:5004
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DatabasePath=/app/data/ppppricing.db

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5004/health || exit 1

ENTRYPOINT ["dotnet", "PppPricing.API.dll"]
