
# If need postgres for local
docker run --name ces-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=cesdb \
  -p 5432:5432 \
  -d postgres