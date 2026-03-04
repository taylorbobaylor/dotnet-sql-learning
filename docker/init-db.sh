#!/bin/bash
# Runs all 6 SQL init scripts in order after SQL Server starts.
# Call this from the host after 'docker-compose up -d'.
#
# Requires sqlcmd on the host Mac:
#   brew install sqlcmd

SA_PASSWORD="${SA_PASSWORD:-InterviewDemo@2026}"
SERVER="localhost,1433"

# Verify sqlcmd is available on the host
if ! command -v sqlcmd &> /dev/null; then
  echo "❌ sqlcmd not found. Install it with:"
  echo "   brew install sqlcmd"
  exit 1
fi

# go-sqlcmd (brew install sqlcmd) defaults to encrypted connections, but
# azure-sql-edge ships a self-signed cert with a negative serial number that
# TLS rejects.  Use -N disable to match VS Code's "Encrypt=False".
SQLCMD_OPTS="-N disable -b"

echo "⏳ Waiting for SQL Server to be ready..."
until sqlcmd -S "$SERVER" -U sa -P "$SA_PASSWORD" -Q "SELECT 1" $SQLCMD_OPTS > /dev/null 2>&1; do
  sleep 3
done

echo "✅ SQL Server is up. Running init scripts..."

# Resolve script dir so this works whether called from repo root or docker/
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for script in $(ls "$SCRIPT_DIR/init/"*.sql | sort); do
  echo "▶️  Running $(basename $script)..."
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
echo ""
echo "   dotnet run — interactive menu"
echo "   dotnet run -- all — run all 6 benchmark scenarios"
