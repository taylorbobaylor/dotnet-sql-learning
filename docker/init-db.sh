#!/bin/bash
# Runs all SQL init scripts in order after SQL Server starts.
# Call this from the host after 'docker-compose up -d'.

SA_PASSWORD="${SA_PASSWORD:-InterviewDemo@2024}"
HOST="localhost,14330"

echo "⏳ Waiting for SQL Server to be ready..."
until docker exec sql-interview-demo /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" -C -b > /dev/null 2>&1; do
  sleep 3
done

echo "✅ SQL Server is up. Running init scripts..."

for script in $(ls docker/init/*.sql | sort); do
  echo "▶️  Running $script..."
  docker exec -i sql-interview-demo /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -b < "$script"
  if [ $? -eq 0 ]; then
    echo "   ✅ Done"
  else
    echo "   ❌ FAILED — check the script for errors"
    exit 1
  fi
done

echo ""
echo "🎉 Database initialized! Connect with:"
echo "   Server:   localhost,1433"
echo "   Login:    sa"
echo "   Password: $SA_PASSWORD"
echo "   Database: InterviewDemoDB"
