#!/bin/bash
# Runs all SQL init scripts in order after SQL Server starts.
# Call this from the host after 'docker-compose up -d'.
#
# Requires sqlcmd on the host Mac:
#   brew install sqlcmd
#
# By default we apply the first five init scripts so learners can complete the
# hands-on lab (the bad stored procedures are left in place).  To run every
# script, including the fix, invoke the script with `--all`.

SA_PASSWORD="${SA_PASSWORD:-InterviewDemo@2026}"
SERVER="localhost,1433"

RUN_ALL=false
if [ "$1" = "--all" ]; then
  RUN_ALL=true
fi

# Verify sqlcmd is available on the host
if ! command -v sqlcmd &> /dev/null; then
  echo "❌ sqlcmd not found. Install it with:"
  echo "   brew install sqlcmd"
  exit 1
fi

# go-sqlcmd (brew install sqlcmd) defaults to encrypted connections, but
# azure-sql-edge ships a self-signed cert with a negative serial number that
# TLS rejects.  Use --encrypt disable to match VS Code's "Encrypt=False".
SQLCMD_OPTS="-N disable -b"

echo "⏳ Waiting for SQL Server to be ready..."
until sqlcmd -S "$SERVER" -U sa -P "$SA_PASSWORD" -Q "SELECT 1" $SQLCMD_OPTS > /dev/null 2>&1; do
  sleep 3
done

echo "✅ SQL Server is up. Running init scripts..."

# Resolve script dir so this works whether called from repo root or docker/
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for script in $(ls "$SCRIPT_DIR/init/"*.sql | sort); do
  # unless --all was specified, skip the fixed stored procedures so students
  # can do the hands-on exercise first
  if ! $RUN_ALL && [[ "$(basename "$script")" = "06-fixed-stored-procs.sql" ]]; then
    echo "✋  Skipping $script (run after the hands-on lab or with --all)"
    continue
  fi

  echo "▶️  Running $script..."
  sqlcmd -S "$SERVER" -U sa -P "$SA_PASSWORD" -i "$script" $SQLCMD_OPTS
  if [ $? -eq 0 ]; then
    echo "   ✅ Done"
  else
    echo "   ❌ FAILED — check the script for errors"
    exit 1
  fi
done

echo ""
echo "🎉 Database initialized! Connect with:"
echo "   Server:   $SERVER"
echo "   Login:    sa"
echo "   Password: $SA_PASSWORD"
echo "   Database: InterviewDemoDB"
