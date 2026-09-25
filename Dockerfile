# ==============================================================================
# Etapa 1: Compilación y publicación del proyecto ASP.NET Core (.NET 10)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar paquetes NuGet
COPY ["creditos.csproj", "./"]
RUN dotnet restore "creditos.csproj"

# Copiar el resto del código y generar la publicación Release
COPY . .
RUN dotnet publish "creditos.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# Etapa 2: Entorno de ejecución optimizado ASP.NET Core (.NET 10)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Crear directorio para persistencia de la base de datos SQLite en disco persistente
RUN mkdir -p /var/data && chmod 777 /var/data

COPY --from=build /app/publish .

# Puerto por defecto (Render asignará la variable $PORT dinámicamente)
EXPOSE 8080

# Iniciar la aplicación expandiendo de forma explícita la variable $PORT provista por Render
CMD ["sh", "-c", "dotnet creditos.dll --urls http://0.0.0.0:${PORT:-8080}"]
