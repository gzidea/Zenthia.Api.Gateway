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
COPY --from=builder /out .

# Exponer el puerto
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["dotnet", "Zenthia.Api.Gateway.dll"]


## ATENCION, ERRORES QUE ARRASTRABA EN ESTE DOCKERFILE: EN LA LINEA 2 LLAME  BUILDER,
## PERO EN LA LINEA 20 HICE REFERENCIA A BUILD, LO CORRIGI PARA QUE SEA BUILDER EN AMBOS LUGARES.