# =====================================================================
# Dockerfile de la API (.NET 10). Multi-stage: build + runtime.
# Construir desde la RAÍZ del repo (el contexto incluye todos los proyectos):
#   docker build -f SistemaCobranzaApp.Api/Dockerfile -t cobranza-api .
# (docker-compose ya lo hace con context: . y dockerfile: ...)
# =====================================================================

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Copiamos solo los .csproj (de la API y sus dependencias) para cachear el restore.
COPY SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj            SistemaCobranzaApp.Api/
COPY SistemaCobranzaApp.Application/SistemaCobranzaApp.Application.csproj SistemaCobranzaApp.Application/
COPY SistemaCobranzaApp.Infrastructure/SistemaCobranzaApp.Infrastructure.csproj SistemaCobranzaApp.Infrastructure/
COPY SistemaCobranzaApp.Shared/SistemaCobranzaApp.Shared.csproj      SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/SistemaCobranzaApp.Domain.csproj      SistemaCobranzaApp.Domain/

RUN dotnet restore SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj

# 2) Copiamos el resto del código de esos proyectos y publicamos.
COPY SistemaCobranzaApp.Api/            SistemaCobranzaApp.Api/
COPY SistemaCobranzaApp.Application/    SistemaCobranzaApp.Application/
COPY SistemaCobranzaApp.Infrastructure/ SistemaCobranzaApp.Infrastructure/
COPY SistemaCobranzaApp.Shared/         SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/         SistemaCobranzaApp.Domain/

RUN dotnet publish SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj \
    -c Release -o /app --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Kestrel escucha en el puerto interno 8081 (solo accesible dentro de la red de Docker).
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081

ENTRYPOINT ["dotnet", "SistemaCobranzaApp.Api.dll"]
