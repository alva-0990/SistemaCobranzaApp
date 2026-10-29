# =====================================================================
# Dockerfile de la API (.NET 10). Multi-stage: build + runtime.
# Construir desde la raíz del repositorio.
# =====================================================================

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# 1) Copiar los archivos de proyecto para aprovechar la caché de restore.
COPY SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj SistemaCobranzaApp.Api/
COPY SistemaCobranzaApp.Application/SistemaCobranzaApp.Application.csproj SistemaCobranzaApp.Application/
COPY SistemaCobranzaApp.Infrastructure/SistemaCobranzaApp.Infrastructure.csproj SistemaCobranzaApp.Infrastructure/
COPY SistemaCobranzaApp.Shared/SistemaCobranzaApp.Shared.csproj SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/SistemaCobranzaApp.Domain.csproj SistemaCobranzaApp.Domain/

# 2) Restaurar dependencias.
RUN dotnet restore SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj

# 3) Copiar el código fuente.
COPY SistemaCobranzaApp.Api/ SistemaCobranzaApp.Api/
COPY SistemaCobranzaApp.Application/ SistemaCobranzaApp.Application/
COPY SistemaCobranzaApp.Infrastructure/ SistemaCobranzaApp.Infrastructure/
COPY SistemaCobranzaApp.Shared/ SistemaCobranzaApp.Shared/
COPY SistemaCobranzaApp.Domain/ SistemaCobranzaApp.Domain/

# 4) Compilar y publicar.
RUN dotnet publish SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj \
    -c Release \
    -o /app \
    --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Dependencia necesaria para evitar errores de biblioteca GSSAPI en Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app ./

# Puerto interno de la API.
ENV ASPNETCORE_URLS=http://+:8081

EXPOSE 8081

ENTRYPOINT ["dotnet", "SistemaCobranzaApp.Api.dll"]
