# Nginx Reverse Proxy

This nginx reverse proxy routes all services through a single port (9080) on the host.

## Routes

- **`http://localhost:9080/api`** → API service
- **`http://localhost:9080/swagger`** → API Swagger documentation
- **`http://localhost:9080/`** → Vue/Vite web application

## Configuration

All services run on port 8080 inside their containers and communicate with each other through the `app-network` Docker network. The nginx proxy is the only service exposed to the host on port 9080.

## Benefits

- Single host port for all services
- Easy to run multiple projects concurrently with different port numbers
- Centralized routing and CORS handling
- Production-like architecture even in development
