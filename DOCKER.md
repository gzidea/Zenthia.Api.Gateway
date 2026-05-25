# 🐳 Docker Setup - Zenthia.Api.Gateway

## 📋 Archivos creados

### 1. **appsettings.Production.json**

Configuración de producción con:

- Logging mínimo (Warning)
- Health checks habilitados
- Rate limiting configurado
- Variables de entorno interpoladas

### 2. **.env.example**

Archivo de referencia con todas las variables de entorno.
**⚠️ Usar:**

```bash
cp .env.example .env
# Editar .env con valores reales (contraseñas, URLs, etc.)
```

### 3. **Dockerfile**

Build de 3 etapas (multi-stage) optimizado:

- **builder**: Restaura dependencias y compila
- **publisher**: Publica la aplicación
- **runtime**: Imagen final basada en `alpine` (más pequeña y segura)

Características:

- ✅ Non-root user (dotnet:1000)
- ✅ Health checks
- ✅ Alpine Linux (imagen ~200MB vs 400MB+)
- ✅ Curl incluido para health checks

### 4. **docker-compose.yml**

Orquestación con:

- **api-gateway**: Tu servicio principal en puerto 8080
- **auth-service**: Placeholder (reemplazar con servicio real)
- **Networking**: Red bridge personalizada `zenthia-network`
- **Health checks**: Automáticos en ambos servicios
- **Environment**: Variables desde `.env`

### 5. **.dockerignore**

Excluye archivos innecesarios de la imagen (git, IDE, etc.)

---

## 🚀 Primeros pasos

### 1. Crear archivo .env

```bash
cp .env.example .env
```

Editar valores según tu entorno:

```env
ASPNETCORE_ENVIRONMENT=Production
AUTH_SERVICE_URL=http://auth-service:5110/
GATEWAY_PORT=8080
LOG_LEVEL=Information
```

### 2. Construir imagen

```bash
docker build -t zenthia-api-gateway:latest .
```

### 3. Ejecutar con Docker Compose

```bash
docker-compose up -d
```

### 4. Verificar status

```bash
# Ver containers
docker-compose ps

# Ver logs
docker-compose logs -f api-gateway

# Probar health check
curl http://localhost:8080/health
```

---

## 📊 Estructura de red Docker

```
┌──────────────────────────────────────────┐
│      zenthia-network (bridge)            │
├──────────────────────────────────────────┤
│                                          │
│  ┌──────────────────┐                    │
│  │  api-gateway:80  │                    │
│  │  port 8080       │                    │
│  └──────┬───────────┘                    │
│         │                                │
│         └──────────────────┐             │
│                            │             │
│         ┌──────────────────▼────┐        │
│         │  auth-service:5110    │        │
│         └───────────────────────┘        │
│                                          │
└──────────────────────────────────────────┘
```

---

## 🔧 Comandos útiles

```bash
# Construir sin caché
docker-compose build --no-cache

# Parar servicios
docker-compose down

# Eliminar todo (containers, networks)
docker-compose down -v

# Ver variables de entorno en container
docker-compose exec api-gateway printenv | grep -i rate

# Entrar en container (debugging)
docker-compose exec api-gateway sh

# Rebuildar solo un servicio
docker-compose build --no-cache api-gateway

# Push a registry (cuando esté lista)
docker tag zenthia-api-gateway:latest your-registry/zenthia-api-gateway:latest
docker push your-registry/zenthia-api-gateway:latest
```

---

## 🔐 Seguridad

- ✅ Non-root user en container (uid 1000)
- ✅ Archivo `.env` en `.gitignore` (nunca comitear con datos sensibles)
- ✅ Alpine Linux reduce superficie de ataque
- ✅ Health checks detectan servicios no saludables
- ⚠️ **Reemplazar `auth-service` placeholder** con servicio real en `docker-compose.yml`

---

## 📝 Próximos pasos

1. **Reemplazar auth-service** con tu imagen real en `docker-compose.yml`
2. **Testear en localhost**:
   ```bash
   docker-compose up -d
   curl http://localhost:8080/health
   ```
3. **Usar en CI/CD**: Adaptar a GitHub Actions, GitLab CI, etc.
4. **Producción**: Considerar:
   - ✅ Secrets manager (AWS Secrets, HashiCorp Vault)
   - ✅ Container registry privado (Docker Hub, ECR, etc.)
   - ✅ Orquestación (Kubernetes, Docker Swarm)
   - ✅ Reverse proxy real (Nginx, Traefik)

---

## 📖 Referencias

- [Docker Multi-stage Builds](https://docs.docker.com/build/building/multi-stage/)
- [Docker Compose Environment Variables](https://docs.docker.com/compose/environment-variables/)
- [Alpine Linux](https://www.alpinelinux.org/)
- [.NET on Docker](https://github.com/dotnet/dotnet-docker)
