# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /app

# Copy proyecto files
COPY Zenthia.Api.Gateway.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .
RUN dotnet publish -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copiar la aplicación publicada
COPY --from=build /out .

# Exponer el puerto
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["dotnet", "Zenthia.Api.Gateway.dll"]
