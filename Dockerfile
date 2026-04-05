# syntax=docker/dockerfile:1
# Multi-stage build:
#   Stage 1 (ng-build)  — Node 22, compiles Angular → /wwwroot
#   Stage 2 (build)     — .NET 10 SDK, publishes the app
#   Stage 3 (runtime)   — .NET 10 ASP.NET runtime image
#
# Building Angular inside the image means CI always ships fresh JS/CSS
# regardless of what's committed under wwwroot/.
ARG BUILDPLATFORM
ARG TARGETPLATFORM

# ── Stage 1: Angular build ────────────────────────────────────────────────────
FROM --platform=${BUILDPLATFORM} node:22-slim AS ng-build
WORKDIR /client
# Install deps first for layer-cache efficiency
COPY client/package*.json ./
RUN npm ci
# Copy source, then build (outputPath "../wwwroot" → /wwwroot)
COPY client/ ./
RUN npx ng build --configuration production

# ── Stage 2: .NET build/publish ───────────────────────────────────────────────
FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src
COPY gasoholic.csproj .
RUN dotnet restore
COPY . .
# Overlay freshly built Angular output (overwrites any stale committed wwwroot/)
COPY --from=ng-build /wwwroot ./wwwroot
RUN dotnet publish gasoholic.csproj -c Release -o /app/publish

# ── Stage 3: Runtime ─────────────────────────────────────────────────────────
FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:10.0.5-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
# Run as root so the app can write to the Azure Files SMB volume mounted at /data.
# The aspnet base image defaults to non-root (uid 1654); Azure Files SMB mounts
# default to root ownership which blocks writes from unprivileged users.
USER root
ENTRYPOINT ["dotnet", "gasoholic.dll"]
