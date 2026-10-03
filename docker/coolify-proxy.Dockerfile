# Caddy with its configuration built in, for docker-compose.coolify.yml: an image carries the file
# wherever Coolify runs the stack, with no bind mount from the repository to depend on.
FROM caddy:2-alpine
COPY Caddyfile.coolify /etc/caddy/Caddyfile
