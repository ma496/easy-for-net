#!/bin/sh
# The server half of `npm run deploy:vps`. scripts/deploy-vps.mjs uploads this file over SSH and runs
# it as root, one phase per connection:
#
#   sh remote.sh check   [KEY=value ...]   # report the prerequisites, change nothing
#   sh remote.sh prepare [KEY=value ...]   # prerequisites, swap, firewall, Coolify, API token,
#                                          # and a deploy key when the repository is private
#   sh remote.sh deploy  [KEY=value ...]   # project, application, domain, env vars, deployment
#
# POSIX sh on purpose: it runs on whatever the VPS ships (dash, bash, busybox ash). Every
# step looks before it acts, so running a phase again changes only what is missing.
#
# Coolify's API is called from here, on http://localhost:8000. The token never leaves the
# server, and port 8000 does not have to be reachable from the machine running the deploy.
# Lines starting with EFN_RESULT are JSON for deploy-vps.mjs; everything else is for the person
# watching.
set -eu

PHASE=${1:-}
if [ $# -gt 0 ]; then shift; fi
# KEY=value arguments become ARG_KEY variables. deploy-vps.mjs quotes each one, so values arrive
# intact; the key is checked before it reaches eval.
for arg in "$@"; do
  key=${arg%%=*}
  case $key in
    "$arg" | "" | *[!A-Za-z0-9_]*) printf 'error: bad argument %s\n' "$key"; exit 1 ;;
  esac
  eval "ARG_$key=\${arg#*=}"
done

STATE_DIR=/root/.efn-deploy
COOLIFY=http://localhost:8000
TOKEN_FILE=$STATE_DIR/coolify-token
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT INT TERM

say() { printf '%s\n' "$*"; }
step() { printf '\n==> %s\n' "$*"; }
warn() { printf 'warning: %s\n' "$*"; }
die() { printf 'error: %s\n' "$*"; exit 1; }
result() { printf 'EFN_RESULT %s\n' "$1"; }
has() { command -v "$1" >/dev/null 2>&1; }
arg() { eval "printf '%s' \"\${ARG_$1:-}\""; }

# Random letters and digits only: safe in a connection string, a Redis password= option and
# an env file without any quoting.
rand_alnum() { LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c "$1"; }

[ "$(id -u)" -eq 0 ] || die "this script must run as root (deploy-vps.mjs runs it through sudo)"
umask 077
mkdir -p "$STATE_DIR"

# --- the operating system ------------------------------------------------------------------

# Which package manager this distribution uses, from /etc/os-release. ID_LIKE lets a derivative
# (Pop!_OS, Linux Mint, Rocky, …) resolve to its parent. Anything unlisted is refused rather
# than guessed at: Coolify's own installer supports the same families.
detect_os() {
  [ -r /etc/os-release ] || die "/etc/os-release not found: cannot tell which Linux distribution this is"
  # shellcheck disable=SC1091
  . /etc/os-release
  OS_NAME=${PRETTY_NAME:-${ID:-unknown}}
  PKG=""
  for id in ${ID:-} ${ID_LIKE:-}; do
    case $id in
      ubuntu | debian | raspbian | pop | linuxmint) PKG=apt ;;
      fedora | rhel | centos | rocky | almalinux | ol | amzn) if has dnf; then PKG=dnf; else PKG=yum; fi ;;
      sles | suse | opensuse | opensuse-leap | opensuse-tumbleweed) PKG=zypper ;;
      arch | manjaro | endeavouros) PKG=pacman ;;
      alpine) PKG=apk ;;
    esac
    if [ -n "$PKG" ]; then break; fi
  done
}

# The package that provides a command, per package manager.
package_for() {
  case "$1:$PKG" in
    ssh-keygen:apt) echo openssh-client ;;
    ssh-keygen:dnf | ssh-keygen:yum) echo openssh-clients ;;
    ssh-keygen:apk) echo openssh-keygen ;;
    ssh-keygen:*) echo openssh ;;
    ca-certificates:*) echo ca-certificates ;;
    *) echo "$1" ;;
  esac
}

pkg_install() {
  case $PKG in
    apt) DEBIAN_FRONTEND=noninteractive apt-get update -y -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends "$@" ;;
    dnf) dnf install -y -q "$@" ;;
    yum) yum install -y -q "$@" ;;
    zypper) zypper --non-interactive --quiet install "$@" ;;
    pacman) pacman -Sy --noconfirm --needed "$@" ;;
    apk) apk add --no-cache "$@" ;;
  esac
}

has_ca_bundle() {
  for f in /etc/ssl/certs/ca-certificates.crt /etc/pki/tls/certs/ca-bundle.crt /etc/ssl/ca-bundle.pem /etc/ssl/cert.pem; do
    if [ -s "$f" ]; then return 0; fi
  done
  return 1
}

# Commands the phases need. Coolify's installer needs bash and curl; the API calls need jq;
# a private repository needs git (to tell) and ssh-keygen (to make its deploy key).
TOOLS="curl bash git jq ssh-keygen"

missing_packages() {
  missing=""
  for tool in $TOOLS; do has "$tool" || missing="$missing $(package_for "$tool")"; done
  has_ca_bundle || missing="$missing ca-certificates"
  printf '%s' "$missing"
}

# Whether something listens on a TCP port, read from /proc so it needs no ss or netstat.
port_in_use() {
  hex=$(printf ':%04X' "$1")
  awk -v p="$hex" 'NR > 1 && $4 == "0A" && substr($2, length($2) - 4) == p { found = 1 } END { exit !found }' \
    /proc/net/tcp /proc/net/tcp6 2>/dev/null
}

container_running() {
  has docker && [ "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null || true)" = "true" ]
}

# --- preflight -----------------------------------------------------------------------------

FAILS=0
WARNS=0
ok() { printf '  ok    %s\n' "$*"; }
note() { printf '  warn  %s\n' "$*"; WARNS=$((WARNS + 1)); }
fail() { printf '  FAIL  %s\n' "$*"; FAILS=$((FAILS + 1)); }

preflight() {
  step "Checking prerequisites"
  detect_os
  if [ -n "$PKG" ]; then ok "OS: $OS_NAME ($PKG)"; else fail "OS: $OS_NAME is not a distribution this script (or Coolify) supports"; fi

  case $(uname -m) in
    x86_64 | amd64 | aarch64 | arm64) ok "CPU architecture: $(uname -m)" ;;
    *) fail "CPU architecture: $(uname -m) (Coolify needs x86_64 or arm64)" ;;
  esac

  cpus=$(getconf _NPROCESSORS_ONLN 2>/dev/null || nproc 2>/dev/null || echo 1)
  if [ "$cpus" -ge 2 ]; then ok "CPU cores: $cpus"; else fail "CPU cores: $cpus (Coolify needs at least 2)"; fi

  # MemTotal reads a little under the installed amount, hence 1.8 GB for a 2 GB machine.
  mem_kb=$(awk '/^MemTotal:/ { print $2 }' /proc/meminfo)
  swap_kb=$(awk '/^SwapTotal:/ { print $2 }' /proc/meminfo)
  mem_mb=$((mem_kb / 1024))
  if [ "$mem_kb" -lt 1800000 ]; then
    fail "memory: ${mem_mb} MB (Coolify needs at least 2 GB)"
  elif [ "$mem_kb" -lt 3800000 ] && [ "$swap_kb" -eq 0 ]; then
    note "memory: ${mem_mb} MB and no swap; a 4 GB swap file will be added, because the API and web builds run on this server"
    NEED_SWAP=1
  else
    ok "memory: ${mem_mb} MB, swap $((swap_kb / 1024)) MB"
  fi

  free_gb=$(df -Pk / | awk 'NR == 2 { print int($4 / 1048576) }')
  if [ "$free_gb" -lt 20 ]; then
    fail "disk: ${free_gb} GB free on / (at least 20 GB needed; Coolify recommends 30)"
  elif [ "$free_gb" -lt 30 ]; then
    note "disk: ${free_gb} GB free on / (Coolify recommends 30 GB; images and builds add up)"
  else
    ok "disk: ${free_gb} GB free on /"
  fi

  if has docker; then
    if has snap && snap list docker >/dev/null 2>&1; then
      fail "Docker is the snap package, which Coolify does not support (snap remove docker, then run again)"
    else
      docker_version=$(docker version --format '{{.Server.Version}}' 2>/dev/null || echo 0)
      if [ "${docker_version%%.*}" -ge 24 ] 2>/dev/null; then ok "Docker: $docker_version"; else fail "Docker: '$docker_version' is too old or not running (Coolify needs 24 or newer)"; fi
    fi
  else
    ok "Docker: not installed (Coolify's installer will install it)"
  fi

  if container_running coolify; then
    COOLIFY_INSTALLED=1
    ok "Coolify: already installed and running"
  else
    COOLIFY_INSTALLED=0
    ok "Coolify: not installed yet"
  fi

  # 80/443 belong to Coolify's proxy and 8000 to its dashboard; anything else there would
  # stop it from starting.
  for port in 80 443 8000; do
    owner=coolify
    [ "$port" = 8000 ] || owner=coolify-proxy
    if ! port_in_use "$port"; then
      ok "port $port: free"
    elif container_running "$owner"; then
      ok "port $port: held by $owner"
    else
      fail "port $port: already in use by another program (stop it, then run again)"
    fi
  done

  packages=$(missing_packages)
  if [ -z "$packages" ]; then ok "tools: $TOOLS"; else ok "tools to install:$packages"; fi

  if has ufw && ufw status 2>/dev/null | grep -q "Status: active"; then
    ok "firewall: ufw is active; the needed ports will be opened"
  elif has firewall-cmd && firewall-cmd --state >/dev/null 2>&1; then
    ok "firewall: firewalld is running; the needed ports will be opened"
  else
    ok "firewall: none active on this server (check your provider's firewall allows 80 and 443)"
  fi

  say ""
  say "  $FAILS failed, $WARNS warning(s)"
  [ "$FAILS" -eq 0 ] || die "fix the failed checks above, then run the deploy again"
}

# --- prepare -------------------------------------------------------------------------------

install_packages() {
  packages=$(missing_packages)
  if [ -z "$packages" ]; then return 0; fi
  step "Installing:$packages"
  # shellcheck disable=SC2086
  pkg_install $packages
}

add_swap() {
  [ "${NEED_SWAP:-0}" = 1 ] || return 0
  if [ -e /swapfile ]; then
    warn "/swapfile exists but is not active; leaving it alone"
    return 0
  fi
  step "Adding a 4 GB swap file"
  fallocate -l 4G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=4096 status=none
  chmod 600 /swapfile
  mkswap /swapfile >/dev/null
  swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >>/etc/fstab
}

# 80/443 for the apps, 8000 for the dashboard and 6001/6002 for its live updates and terminal.
open_firewall() {
  ssh_port=$(arg SSH_PORT)
  ssh_port=${ssh_port:-22}
  if has ufw && ufw status 2>/dev/null | grep -q "Status: active"; then
    step "Opening ports in ufw"
    for rule in "$ssh_port/tcp" 80/tcp 443/tcp 443/udp 8000/tcp 6001/tcp 6002/tcp; do ufw allow "$rule" >/dev/null; done
  elif has firewall-cmd && firewall-cmd --state >/dev/null 2>&1; then
    step "Opening ports in firewalld"
    for rule in "$ssh_port/tcp" 80/tcp 443/tcp 443/udp 8000/tcp 6001/tcp 6002/tcp; do firewall-cmd --quiet --permanent --add-port="$rule"; done
    firewall-cmd --quiet --reload
  fi
}

wait_for_coolify() {
  say "Waiting for Coolify to answer..."
  i=0
  until curl -fsS "$COOLIFY/api/health" >/dev/null 2>&1; do
    i=$((i + 1))
    [ "$i" -le 90 ] || die "Coolify did not come up within 7 minutes (docker logs coolify)"
    sleep 5
  done
}

# Whether Coolify has its root account (id 0), which the API token belongs to.
root_user_exists() {
  docker exec coolify php artisan tinker --execute='echo App\Models\User::where("id", 0)->exists() ? "EFN_YES" : "EFN_NO";' 2>/dev/null | grep -q EFN_YES
}

install_coolify() {
  if [ "$COOLIFY_INSTALLED" = 1 ]; then
    wait_for_coolify
  else
    step "Installing Coolify"
    curl -fsSL https://cdn.coollabs.io/coolify/install.sh -o "$WORK/coolify-install.sh"
    # The installer names the SSH user Coolify reaches this host as after $USER, which a
    # non-login root shell may not set.
    env USER=root HOME=/root bash "$WORK/coolify-install.sh"
    wait_for_coolify
  fi
  # Someone may already have registered in the dashboard: that account is the root one.
  if root_user_exists; then return 0; fi
  create_root_user
}

# The dashboard's administrator account, made by Coolify's own RootUserSeeder: the same
# validation (a real email domain, a strong password) and the same team membership as the
# installer's ROOT_USER_* variables give, and it closes public registration. Run directly rather
# than through the installer, which never replaces a ROOT_USER_* value it saved before.
create_root_user() {
  email=$(arg EMAIL)
  [ -n "$email" ] || die "EMAIL is required to create Coolify's administrator account"
  step "Creating Coolify's administrator account"
  # Coolify's password rules want an upper- and lower-case letter, a digit and a symbol.
  password="$(rand_alnum 20)_Aa1"
  seeded=$(docker exec -e ROOT_USERNAME=admin -e ROOT_USER_EMAIL="$email" -e ROOT_USER_PASSWORD="$password" \
    coolify php artisan db:seed --class=RootUserSeeder --force 2>&1 || true)
  if ! root_user_exists; then
    printf '%s\n' "$seeded" | grep -E 'ERROR|→' || true
    die "Coolify refused the administrator account for $email (it checks that the email's domain exists): pass a real address with --email and run again"
  fi
  printf 'Coolify dashboard: email %s\npassword: %s\n' "$email" "$password" >"$STATE_DIR/coolify-admin"
  say ""
  say "Coolify's dashboard account (also saved in $STATE_DIR/coolify-admin):"
  say "  email    $email"
  say "  password $password"
}

# --- the Coolify API -----------------------------------------------------------------------

# api METHOD PATH [JSON]: sets API_STATUS and API_BODY. Never exits by itself.
api() {
  if [ $# -ge 3 ]; then
    API_STATUS=$(curl -sS -o "$WORK/body" -w '%{http_code}' -X "$1" \
      -H "Authorization: Bearer $TOKEN" -H 'Accept: application/json' -H 'Content-Type: application/json' \
      --data "$3" "$COOLIFY/api/v1$2") || true
  else
    API_STATUS=$(curl -sS -o "$WORK/body" -w '%{http_code}' -X "$1" \
      -H "Authorization: Bearer $TOKEN" -H 'Accept: application/json' \
      "$COOLIFY/api/v1$2") || true
  fi
  API_STATUS=${API_STATUS:-000}
  API_BODY=$(cat "$WORK/body" 2>/dev/null || true)
}

api_ok() { case $API_STATUS in 2??) return 0 ;; *) return 1 ;; esac; }

# api_must METHOD PATH [JSON]: as api, and stops with Coolify's own answer when it fails.
api_must() {
  api "$@"
  api_ok || die "Coolify API $1 $2 answered $API_STATUS: $API_BODY"
}

token_works() {
  [ -n "${TOKEN:-}" ] || return 1
  api GET /version
  api_ok
}

# A token for the root user (id 0) on the root team (id 0), made inside the Coolify container.
# Coolify's own createToken() reads the team from the web session, which tinker does not have,
# so the row is created directly in the shape Sanctum checks: "<id>|<plain>", sha256(plain).
create_token() {
  php='
    $plain = Illuminate\Support\Str::random(40);
    $user = App\Models\User::find(0);
    $token = $user->tokens()->create(["name" => "efn-deploy", "token" => hash("sha256", $plain), "abilities" => ["root"], "team_id" => "0"]);
    instanceSettings()->update(["is_api_enabled" => true]);
    echo "EFN_TOKEN=" . $token->id . "|" . $plain . PHP_EOL;
  '
  docker exec coolify php artisan tinker --execute="$php" 2>/dev/null | sed -n 's/^EFN_TOKEN=//p' | tr -d '\r' | head -n 1
}

ensure_token() {
  step "Connecting to Coolify's API"
  TOKEN=$(cat "$TOKEN_FILE" 2>/dev/null || true)
  if token_works; then
    ok "reusing the saved API token"
    return 0
  fi
  TOKEN=$(create_token || true)
  if ! token_works; then
    # The tinker route depends on Coolify's internals; this is the route its documentation gives.
    host=$(arg HOST)
    say "Could not create an API token automatically. Create one by hand:"
    say "  1. Open http://${host:-<server-ip>}:8000 and sign in (credentials in $STATE_DIR/coolify-admin if this script installed Coolify)"
    say "  2. Settings > Advanced: turn on 'API Access'"
    say "  3. Keys & Tokens > API tokens: create a token with the 'root' permission"
    printf 'Paste the token here: '
    read -r TOKEN </dev/tty
    token_works || die "Coolify refused that token ($API_STATUS)"
  fi
  printf '%s' "$TOKEN" >"$TOKEN_FILE"
  ok "API token saved in $TOKEN_FILE"
}

load_token() {
  TOKEN=$(cat "$TOKEN_FILE" 2>/dev/null || true)
  token_works || die "no working Coolify API token on this server; run the deploy without skipping prepare"
}

# Public repositories are cloned over HTTPS. A private one gets an SSH deploy key, registered
# in Coolify; deploy-vps.mjs adds its public half to the repository.
probe_source() {
  step "Checking the repository"
  repo_https=$(arg REPO_HTTPS)
  app=$(arg APP_NAME)
  if GIT_TERMINAL_PROMPT=0 git ls-remote "$repo_https" HEAD >/dev/null 2>&1; then
    ok "$repo_https is public"
    result '{"visibility":"public"}'
    return 0
  fi
  ok "$repo_https is private (or does not exist yet): using a deploy key"
  key_name="efn-$app"
  key_file="$STATE_DIR/$key_name"
  [ -f "$key_file" ] || ssh-keygen -q -t ed25519 -N '' -C "$key_name" -f "$key_file"
  api_must GET /security/keys
  key_uuid=$(printf '%s' "$API_BODY" | jq -r --arg n "$key_name" '[.[] | select(.name == $n)][0].uuid // empty')
  if [ -z "$key_uuid" ]; then
    body=$(jq -n --arg n "$key_name" --rawfile k "$key_file" '{name: $n, description: "Deploy key for \($n)", private_key: $k}')
    api_must POST /security/keys "$body"
    key_uuid=$(printf '%s' "$API_BODY" | jq -r '.uuid')
  fi
  result "$(jq -cn --arg k "$(cat "$key_file.pub")" --arg u "$key_uuid" --arg n "$key_name" '{visibility: "private", publicKey: $k, keyUuid: $u, keyName: $n}')"
}

# --- deploy --------------------------------------------------------------------------------

deploy() {
  app=$(arg APP_NAME)
  domain=$(arg DOMAIN)
  repo=$(arg REPO)
  branch=$(arg BRANCH)
  key_uuid=$(arg KEY_UUID)
  compose=$(arg COMPOSE)
  compose=${compose:-/docker-compose.coolify.yml}
  [ -n "$app" ] && [ -n "$domain" ] && [ -n "$repo" ] && [ -n "$branch" ] || die "APP_NAME, DOMAIN, REPO and BRANCH are required"
  url="https://$domain"
  load_token

  step "Coolify project and application '$app'"
  api_must GET /servers
  server_uuid=$(printf '%s' "$API_BODY" | jq -r '[.[] | select(.name == "localhost" or .ip == "host.docker.internal")][0].uuid // .[0].uuid // empty')
  [ -n "$server_uuid" ] || die "Coolify has no server registered"
  # A fresh Coolify checks its own host on a timer; ask now rather than fail the deployment.
  server_usable() {
    api GET "/servers/$server_uuid"
    api_ok && [ "$(printf '%s' "$API_BODY" | jq -r '(.settings.is_reachable and .settings.is_usable) // false')" = true ]
  }
  if ! server_usable; then
    say "Asking Coolify to validate this server..."
    # POST in current Coolify releases, GET in older ones.
    api POST "/servers/$server_uuid/validate" '{}'
    case $API_STATUS in 404 | 405) api GET "/servers/$server_uuid/validate" ;; esac
    i=0
    until server_usable; do
      i=$((i + 1))
      [ "$i" -le 24 ] || die "Coolify cannot use this server yet (Servers > localhost > Validate in the dashboard shows why)"
      sleep 5
    done
  fi
  ok "server localhost is usable"

  api_must GET /projects
  project_uuid=$(printf '%s' "$API_BODY" | jq -r --arg n "$app" '[.[] | select(.name == $n)][0].uuid // empty')
  if [ -z "$project_uuid" ]; then
    api_must POST /projects "$(jq -cn --arg n "$app" '{name: $n, description: "Deployed by npm run deploy:vps"}')"
    project_uuid=$(printf '%s' "$API_BODY" | jq -r '.uuid')
    ok "created project $app"
  else
    ok "project $app exists"
  fi

  # Coolify routes the domain to the proxy service's port 80; its Traefik holds the certificate.
  domains=$(jq -cn --arg d "$url:80" '[{name: "proxy", domain: $d}]')

  api_must GET /applications
  app_uuid=$(printf '%s' "$API_BODY" | jq -r --arg n "$app" '[.[] | select(.name == $n)][0].uuid // empty')
  if [ -z "$app_uuid" ]; then
    body=$(jq -cn --arg p "$project_uuid" --arg s "$server_uuid" --arg r "$repo" --arg b "$branch" \
      --arg c "$compose" --arg n "$app" --arg k "$key_uuid" --argjson d "$domains" \
      '{project_uuid: $p, server_uuid: $s, environment_name: "production", git_repository: $r, git_branch: $b,
        build_pack: "dockercompose", ports_exposes: "80", docker_compose_location: $c, name: $n,
        docker_compose_domains: $d, instant_deploy: false}
       + (if $k == "" then {} else {private_key_uuid: $k} end)')
    endpoint=/applications/public
    if [ -n "$key_uuid" ]; then endpoint=/applications/private-deploy-key; fi
    api POST "$endpoint" "$body"
    if ! api_ok && printf '%s' "$API_BODY" | grep -q docker_compose_domains; then
      # Some Coolify versions take the domains only once the application exists.
      api_must POST "$endpoint" "$(printf '%s' "$body" | jq -c 'del(.docker_compose_domains)')"
    fi
    api_ok || die "Coolify API POST $endpoint answered $API_STATUS: $API_BODY"
    app_uuid=$(printf '%s' "$API_BODY" | jq -r '.uuid')
    ok "created application $app"
  else
    ok "application $app exists"
  fi

  # Only the branch: Coolify splits the repository URL into a source and a path when the
  # application is created, and a full URL sent again would be stored inside that path.
  api_must PATCH "/applications/$app_uuid" "$(jq -cn --arg b "$branch" '{git_branch: $b}')"
  # The domain is set when the application is created. Coolify accepts a change to it only once
  # it has read the compose file, at the first deployment; until then the creation's value stands.
  api PATCH "/applications/$app_uuid" "$(jq -cn --argjson d "$domains" '{docker_compose_domains: $d}')"
  if api_ok; then
    ok "domain $url -> proxy:80"
  elif printf '%s' "$API_BODY" | grep -q docker_compose_raw; then
    ok "domain $url -> proxy:80 (set when the application was created)"
  else
    die "could not attach $domain ($API_STATUS: $API_BODY). In the dashboard, open the application and set the 'proxy' service's domain to $url:80"
  fi

  step "Environment variables"
  api_must GET "/applications/$app_uuid/envs"
  printf '%s' "$API_BODY" | jq -r '.[] | select(.is_preview != true) | "\(.key)=\(.value // "")"' >"$WORK/envs"
  env_value() { sed -n "s/^$1=//p" "$WORK/envs" | head -n 1; }
  changes='[]'
  set_env() { changes=$(printf '%s' "$changes" | jq -c --arg k "$1" --arg v "$2" '. + [{key: $k, value: $v, is_preview: false}]'); }
  # Replaced every run: what the domain decides.
  set_env PUBLIC_URL "$url"
  # Filled once and never overwritten: changing a password here would lock the app out of its own
  # database, and a new JWT key would sign every user out.
  [ -n "$(env_value POSTGRES_DB)" ] || set_env POSTGRES_DB app
  [ -n "$(env_value POSTGRES_USER)" ] || set_env POSTGRES_USER app
  [ -n "$(env_value REDIS_INSTANCE_NAME)" ] || set_env REDIS_INSTANCE_NAME "$app:"
  for key in POSTGRES_PASSWORD REDIS_PASSWORD; do
    [ -n "$(env_value "$key")" ] || set_env "$key" "$(rand_alnum 32)"
  done
  [ -n "$(env_value JWT_KEY)" ] || set_env JWT_KEY "$(rand_alnum 64)"
  seed_password=""
  if [ -z "$(env_value SEED_ADMIN_PASSWORD)" ]; then
    seed_password="$(rand_alnum 16)_Aa1"
    set_env SEED_ADMIN_PASSWORD "$seed_password"
  fi
  api_must PATCH "/applications/$app_uuid/envs/bulk" "$(jq -cn --argjson d "$changes" '{data: $d}')"
  ok "$(printf '%s' "$changes" | jq -r 'map(.key) | join(", ")') set"
  if [ -n "$seed_password" ]; then
    printf 'App %s (%s)\nadmin and tenantadmin password: %s\n' "$app" "$url" "$seed_password" >"$STATE_DIR/$app.txt"
  fi

  step "Deploying (the first build takes several minutes)"
  # POST in current Coolify releases, GET in older ones.
  api POST "/deploy?uuid=$app_uuid&force=false" '{}'
  case $API_STATUS in 404 | 405) api_must GET "/deploy?uuid=$app_uuid&force=false" ;; esac
  api_ok || die "Coolify API POST /deploy answered $API_STATUS: $API_BODY"
  deployment=$(printf '%s' "$API_BODY" | jq -r '.deployments[0].deployment_uuid // empty')
  [ -n "$deployment" ] || die "Coolify did not start a deployment: $API_BODY"
  last=""
  i=0
  while :; do
    api GET "/deployments/$deployment"
    status=$(printf '%s' "$API_BODY" | jq -r '.status // "unknown"' 2>/dev/null || echo unknown)
    [ "$status" = "$last" ] || say "  $status"
    last=$status
    case $status in
      finished) break ;;
      failed | cancelled*)
        say "Last lines of the deployment log:"
        printf '%s' "$API_BODY" | jq -r '.logs | fromjson | map(select(.hidden != true)) | .[-40:][] | .output' 2>/dev/null || true
        die "deployment $status (full log in the dashboard)"
        ;;
    esac
    i=$((i + 1))
    [ "$i" -le 360 ] || die "deployment still '$status' after an hour"
    sleep 10
  done

  step "Waiting for $url"
  # Through this server's own proxy, so a DNS record that has not spread yet does not matter.
  i=0
  until curl -fsS --max-time 10 --resolve "$domain:443:127.0.0.1" "$url/health" >/dev/null 2>&1; do
    i=$((i + 1))
    if [ "$i" -gt 30 ]; then
      if curl -fsSk --max-time 10 --resolve "$domain:443:127.0.0.1" "$url/health" >/dev/null 2>&1; then
        warn "the app answers, but its certificate is not valid yet: check that $domain points at this server"
        break
      fi
      die "$url/health does not answer; check the application's logs in the dashboard"
    fi
    sleep 10
  done

  say ""
  say "Deployed: $url"
  if [ -f "$STATE_DIR/$app.txt" ]; then
    say "Sign in as admin (platform) or tenantadmin; the password is in $STATE_DIR/$app.txt"
    if [ -n "$seed_password" ]; then say "  password: $seed_password"; fi
  fi
  result "$(jq -cn --arg u "$url" '{url: $u}')"
}

# --- phases --------------------------------------------------------------------------------

case $PHASE in
  check) preflight ;;
  prepare)
    preflight
    install_packages
    add_swap
    open_firewall
    install_coolify
    ensure_token
    probe_source
    ;;
  deploy) deploy ;;
  *) die "usage: remote.sh check|prepare|deploy [KEY=value ...]" ;;
esac

# deploy-vps.mjs uploads this file for one run; the last phase removes it.
if [ "$(arg CLEANUP)" = 1 ]; then rm -f "$0"; fi
exit 0
