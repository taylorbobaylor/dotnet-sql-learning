#!/bin/bash
# Runs inside the sql-init container after SQL Server is healthy.
# Connects to the sqlserver service over the compose network and runs all init scripts.

SA_PASSWORD="${SA_PASSWORD:-InterviewDemo@2026}"
SERVER="sqlserver,1433"

# azure-sql-edge ships sqlcmd at /opt/mssql-tools/bin/sqlcmd
SQLCMD=$(command -v sqlcmd 2>/dev/null || echo "/opt/mssql-tools/bin/sqlcmd")

if [ ! -x "$SQLCMD" ]; then
  echo "❌ sqlcmd not found at $SQLCMD"
  exit 1
fi

echo "✅ SQL Server is ready. Running init scripts..."

for script in $(ls /init/*.sql | sort); do
  echo "▶️  Running $(basename "$script")..."
  "$SQLCMD" -S "$SERVER" -U sa -P "$SA_PASSWORD" -i "$script" -N disable -b
  if [ $? -eq 0 ]; then
    echo "   ✅ Done"
  else
    echo "   ❌ FAILED — check the script for errors"
    exit 1
  fi
done

echo ""
echo "🎉 Database initialized!"
