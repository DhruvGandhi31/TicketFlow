#!/bin/bash
set -e

# postgres image only lets you set ONE default db via POSTGRES_DB, so the second one gets created
# here instead - this runs once, only when the data volume is empty (first container start)
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE events;
    CREATE DATABASE seats;
EOSQL
