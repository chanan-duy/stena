#!/bin/sh
set -e

for db in identity canvas history render; do
    psql -v ON_ERROR_STOP=1 -v password="$DB_PASSWORD" -U "$POSTGRES_USER" <<SQL
CREATE USER $db PASSWORD :'password';
CREATE DATABASE $db OWNER $db;
REVOKE CONNECT ON DATABASE $db FROM PUBLIC;
SQL
done
