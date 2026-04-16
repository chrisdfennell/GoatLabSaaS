FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# curl is used by the Docker healthcheck to probe /health.
# Retries guard against transient Ubuntu mirror sync races.
RUN apt-get -o Acquire::Retries=5 update \
    && apt-get -o Acquire::Retries=5 install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# NuGet restore is forky — disable parallelism to work around QNAP/Synology
# Container Station seccomp profiles that reject certain clone() flags.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

# Copy project files and restore
COPY src/GoatLab.Shared/GoatLab.Shared.csproj GoatLab.Shared/
COPY src/GoatLab.Client/GoatLab.Client.csproj GoatLab.Client/
COPY src/GoatLab.Server/GoatLab.Server.csproj GoatLab.Server/
RUN dotnet restore GoatLab.Server/GoatLab.Server.csproj --disable-parallel

# Copy everything and build
COPY src/ .
RUN dotnet publish GoatLab.Server/GoatLab.Server.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create media directory
RUN mkdir -p /app/media

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "GoatLab.Server.dll"]
