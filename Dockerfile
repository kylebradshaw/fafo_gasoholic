# syntax=docker/dockerfile:1
# BuildKit automatically sets BUILDPLATFORM (host) and TARGETPLATFORM (build target).
# Production / x64 CI (GitHub Actions ubuntu-latest):
#   docker build -t gasoholic .
# Apple Silicon (Docker Desktop runs an x86 VM by default — use explicit arm64):
#   docker buildx build --platform linux/arm64 -t gasoholic --load .
#   docker run --platform linux/arm64 ...
ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0.103-noble AS build
WORKDIR /src
COPY gasoholic.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM --platform=${TARGETPLATFORM} mcr.microsoft.com/dotnet/aspnet:10.0.5-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "gasoholic.dll"]
