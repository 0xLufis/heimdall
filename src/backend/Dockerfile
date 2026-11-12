# Multi-stage production build for the Heimdall backend: REST API, SignalR hub, embedded MQTT broker (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy Directory.Build.props and project files for layer caching
COPY Directory.Build.props ./
COPY src/shared/App.Contracts/App.Contracts.csproj ./src/shared/App.Contracts/
COPY src/shared/App.Shared/App.Shared.csproj ./src/shared/App.Shared/
COPY src/backend/App.Infrastructure/App.Infrastructure.csproj ./src/backend/App.Infrastructure/
COPY src/backend/App.Backend.Api/App.Backend.Api.csproj ./src/backend/App.Backend.Api/

# Restore dependencies
RUN dotnet restore src/backend/App.Backend.Api/App.Backend.Api.csproj

# Copy application source code
COPY src/shared/ ./src/shared/
COPY src/backend/ ./src/backend/

# Publish optimized Release output
RUN dotnet publish src/backend/App.Backend.Api/App.Backend.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Production runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# Create unprivileged system user for zero-trust security
RUN groupadd -g 10001 heimdall && \
    useradd -u 10001 -g heimdall -m -s /bin/false heimdall

COPY --from=build --chown=heimdall:heimdall /app/publish .

USER heimdall

EXPOSE 5001 5099 1883

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001;http://+:5099

ENTRYPOINT ["dotnet", "App.Backend.Api.dll"]
