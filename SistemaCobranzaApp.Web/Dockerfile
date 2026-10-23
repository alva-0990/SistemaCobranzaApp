# =====================================================================
# Dockerfile del Web (Blazor Server, .NET 10). Multi-stage.
# Construir desde la RAÍZ del repo:
#   docker build -f SistemaCobranzaApp.Web/Dockerfile -t cobranza-web .
# =====================================================================

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) .csproj del Web y sus dependencias (Shared → Domain) para cachear restore.
COPY SistemaCobranzaApp.Web/SistemaCobranzaApp.Web.csproj      SistemaCobranzaApp.Web/
COPY SistemaCobranzaApp.Shared/SistemaCobranzaApp.Shared.csproj SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/SistemaCobranzaApp.Domain.csproj SistemaCobranzaApp.Domain/

RUN dotnet restore SistemaCobranzaApp.Web/SistemaCobranzaApp.Web.csproj

# 2) Código y publicación.
COPY SistemaCobranzaApp.Web/    SistemaCobranzaApp.Web/
COPY SistemaCobranzaApp.Shared/ SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/ SistemaCobranzaApp.Domain/

RUN dotnet publish SistemaCobranzaApp.Web/SistemaCobranzaApp.Web.csproj \
    -c Release -o /app --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Kestrel escucha en el puerto interno 8080.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SistemaCobranzaApp.Web.dll"]
