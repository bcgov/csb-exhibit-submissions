## Using the Application

- The web application is available at: http://localhost:9080/
- The Swagger API and documentation is available at: http://localhost:9080/swagger
- The API is also proxied at: http://localhost:9080/api

# Running the Application on Docker

## Prerequisites

Copy `.env.template` to `.env` and fill in any required values:

```bash
cp .env.template .env
```

The default `.env` is pre-configured for local development with sensible defaults.

## Management Script

The `manage` script wraps the Docker process in easy to use commands.

To get full usage information on the script, run:

```
./manage -h
```

### Build all containers

```bash
./manage build
```

### Build specific container

```bash
./manage build api
./manage build api-dev
./manage build web
./manage build web-dev
./manage build nginx
```

### Start services

```bash
./manage start
```

`start` runs the release image services: `api`, `web`, `nginx`, and `db`.

### Start in debug mode (with hot reload)

```bash
./manage debug
```

`debug` runs the development image services: `api-dev`, `web-dev`, `nginx`, and `db`.

### Stop services

```bash
./manage stop
```

### Remove containers and volumes

```bash
./manage down
# or
./manage rm
```

## Services

| Service | Internal Port | External Access       |
| ------- | ------------- | --------------------- |
| nginx   | 8080          | http://localhost:9080 |
| api     | 8080          | via nginx proxy (release) |
| api-dev | 8080          | via nginx proxy (debug) |
| web     | 8080          | via nginx proxy (release) |
| web-dev | 8080          | via nginx proxy (debug) |
| db      | 5432          | localhost:5432        |
