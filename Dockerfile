# MarketForge API - Dockerfile para Render/Koyeb/Fly
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia projetos primeiro para aproveitar cache de restore
COPY src/AlbionMarket.Domain/AlbionMarket.Domain.csproj src/AlbionMarket.Domain/
COPY src/AlbionMarket.Application/AlbionMarket.Application.csproj src/AlbionMarket.Application/
COPY src/AlbionMarket.Infrastructure/AlbionMarket.Infrastructure.csproj src/AlbionMarket.Infrastructure/
COPY src/AlbionMarket.Api/AlbionMarket.Api.csproj src/AlbionMarket.Api/

RUN dotnet restore src/AlbionMarket.Api/AlbionMarket.Api.csproj

COPY . .
RUN dotnet publish src/AlbionMarket.Api/AlbionMarket.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Render/Koyeb fornecem PORT. Fallback local: 8080.
CMD ["sh", "-c", "dotnet AlbionMarket.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
