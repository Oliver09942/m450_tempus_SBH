# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Tempus.sln ./
COPY Tempus.Core/Tempus.Core.csproj Tempus.Core/
COPY Tempus.Web/Tempus.Web.csproj Tempus.Web/
RUN dotnet restore Tempus.Web/Tempus.Web.csproj
COPY Tempus.Core Tempus.Core
COPY Tempus.Web Tempus.Web
RUN dotnet publish Tempus.Web/Tempus.Web.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY Demodaten /seed
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh && mkdir /daten && chown app:app /daten
ENV TEMPUS_PORT=5184
USER app
VOLUME /daten
EXPOSE 5184
ENTRYPOINT ["/entrypoint.sh"]

# Builds the Windows package (replaces Verteilung/Paket-bauen.ps1): docker compose run --rm paket
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS paket
RUN apt-get update && apt-get install -y --no-install-recommends git zip && rm -rf /var/lib/apt/lists/*
WORKDIR /repo
ENTRYPOINT ["bash", "/repo/Verteilung/paket-bauen.sh"]
