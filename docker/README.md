## Using the Application

- The web application is available at: http://localhost:9080/
- The API is proxied through web/web-dev at: http://localhost:9080/api
- The Swagger API and documentation is available directly at: http://localhost:8080/swagger

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
```

### Start services

```bash
./manage start
```

`start` runs the release image services: `api`, `web`, and `db`.

### Start in debug mode (with hot reload)

```bash
./manage debug
```

`debug` runs the development image services: `api-dev`, `web-dev`, and `db`.

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
| web     | 8080          | http://localhost:9080 (release) |
| web-dev | 8080          | http://localhost:9080 (debug) |
| api     | 8080          | proxied by web on /api (release) |
| api-dev | 8080          | proxied by web-dev on /api (debug) |
| db      | 5432          | localhost:5432        |
