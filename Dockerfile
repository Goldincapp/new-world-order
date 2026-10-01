# The game server, which also hosts the phone client, so the whole game is one address.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY server/NWO.Server/NWO.Server.csproj server/NWO.Server/
RUN dotnet restore server/NWO.Server/NWO.Server.csproj
COPY server/NWO.Server/ server/NWO.Server/
RUN dotnet publish server/NWO.Server/NWO.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
COPY client/ /app/client/
# Game data lives on a mounted volume so it survives redeploys.
ENV NWO_DB=/data/nwo.db \
    NWO_CLIENT=/app/client \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN mkdir -p /data
CMD ["sh", "-c", "dotnet NWO.Server.dll --urls http://0.0.0.0:${PORT:-8080}"]
