#!/bin/bash

# Gasoholic - Test Runner
# Builds the main project and runs .NET integration tests against a live SQL Server.

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Load SA_PASSWORD from .env
if [ -f .env ]; then
  set -a
  # shellcheck source=.env
  source .env
  set +a
else
  echo -e "${RED}✗ .env file not found${NC}"
  echo "  Create one with: echo 'SA_PASSWORD=<your-password>' > .env"
  exit 1
fi

if [ -z "$SA_PASSWORD" ]; then
  echo -e "${RED}✗ SA_PASSWORD is not set in .env${NC}"
  exit 1
fi

# Verify SQL Server is running
echo -e "${BLUE}Checking SQL Server...${NC}"
STATUS=$(docker inspect --format='{{.State.Health.Status}}' gasoholic-sqlserver 2>/dev/null || echo "missing")
if [ "$STATUS" != "healthy" ]; then
  echo -e "${RED}✗ SQL Server is not running or not healthy (status: $STATUS)${NC}"
  echo "  Start it with: docker compose up -d"
  echo "  Then wait for it to become healthy and retry."
  exit 1
fi
echo -e "${GREEN}✓ SQL Server is healthy${NC}"

# Build the test project (also rebuilds gasoholic.csproj as a dependency)
echo -e "${BLUE}Building...${NC}"
dotnet build Tests/Tests.csproj -c Debug --nologo -v q
echo -e "${GREEN}✓ Build succeeded${NC}"

# Run tests
echo -e "${BLUE}Running tests...${NC}"
dotnet test Tests/ --no-build --logger "console;verbosity=normal" --nologo
