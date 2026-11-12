#!/usr/bin/env bash
# Wrapper around the stock postgres entrypoint.
#
# PostgreSQL refuses to start unless the TLS private key is owned by the database user (uid 999 in
# the image) or root. The certs are generated on the host and bind-mounted read-only, so they are
# owned by whoever ran openssl (typically uid 1000). Instead of requiring a `sudo chown` on the
# host, copy them to a container-local directory with the ownership PostgreSQL insists on.
# The same applies to the bind-mounted log directory, which the logging collector must write to.
set -euo pipefail

SRC=/certs
DST=/etc/postgresql-tls

if [ "$(id -u)" = "0" ]; then
    for f in server.crt server.key; do
        if [ ! -r "$SRC/$f" ]; then
            echo "pg-entrypoint: missing $SRC/$f - generate infra/database/certs first (see README)." >&2
            exit 1
        fi
    done
    install -d -o postgres -g postgres -m 700 "$DST"
    install -o postgres -g postgres -m 644 "$SRC/server.crt" "$DST/server.crt"
    install -o postgres -g postgres -m 600 "$SRC/server.key" "$DST/server.key"

    if [ -d /var/log/postgresql ]; then
        chown postgres:postgres /var/log/postgresql
    fi
fi

exec docker-entrypoint.sh "$@"
