# Multi-stage production build for Heimdall Industrial Edge Agent (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy Directory.Build.props and project files for layer caching
COPY Directory.Build.props ./
COPY shared/App.Contracts/App.Contracts.csproj ./shared/App.Contracts/
COPY agent/App.Agent.Daemon/App.Agent.Daemon.csproj ./agent/App.Agent.Daemon/

# Restore dependencies
RUN dotnet restore agent/App.Agent.Daemon/App.Agent.Daemon.csproj

# Copy application source code
COPY shared/ ./shared/
COPY agent/ ./agent/

# Publish optimized Release output
RUN dotnet publish agent/App.Agent.Daemon/App.Agent.Daemon.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Production runtime container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# Create unprivileged system user for edge security
RUN groupadd -g 10002 heimdall-agent && \
    useradd -u 10002 -g heimdall-agent -m -s /bin/false heimdall-agent

COPY --from=build --chown=heimdall-agent:heimdall-agent /app/publish .

USER heimdall-agent

EXPOSE 5998

ENV DOTNET_ENVIRONMENT=Production
ENV AGENT_URLS=http://0.0.0.0:5998

ENTRYPOINT ["dotnet", "App.Agent.Daemon.dll"]
