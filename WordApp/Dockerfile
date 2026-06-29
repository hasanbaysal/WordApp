# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore bağımlılıkları önce kopyala (layer cache için)
COPY ["WordApp.csproj", "./"]
RUN dotnet restore "WordApp.csproj"

# Tüm kaynak kodunu kopyala ve yayına hazırla
COPY . .
RUN dotnet publish "WordApp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Güvenlik: root olmayan kullanıcı
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# ASP.NET Core iç port (Nginx reverse proxy'ye açılacak)
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "WordApp.dll"]
