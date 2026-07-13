# BatoBuzz Accounting API - Multi-stage Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

# Copy every MSBuild input needed for a deterministic restore before source.
COPY Directory.Build.props global.json ./
COPY src/BatoBuzz.Domain/BatoBuzz.Domain.csproj src/BatoBuzz.Domain/
COPY src/BatoBuzz.Contracts/BatoBuzz.Contracts.csproj src/BatoBuzz.Contracts/
COPY src/BatoBuzz.Application/BatoBuzz.Application.csproj src/BatoBuzz.Application/
COPY src/BatoBuzz.Infrastructure/BatoBuzz.Infrastructure.csproj src/BatoBuzz.Infrastructure/
COPY src/BatoBuzz.Api/BatoBuzz.Api.csproj src/BatoBuzz.Api/
RUN dotnet restore src/BatoBuzz.Api/BatoBuzz.Api.csproj --nologo

# Copy source and build
COPY src/ src/
RUN dotnet publish src/BatoBuzz.Api/BatoBuzz.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --nologo

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
WORKDIR /app

# curl is the only additional runtime package and is used by the container health check.
# A fixed high UID/GID makes bind-mounted data/log directory ownership predictable.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 appuser \
    && useradd --uid 10001 --gid 10001 --no-create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /app/logs /app/data \
    && chown -R appuser:appuser /app

# Copy published output
COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "BatoBuzz.Api.dll"]
