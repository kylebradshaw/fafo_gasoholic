#!/bin/bash

# Gasoholic - Development Start Script
# Builds Angular and starts .NET backend for local development
# Supports hot-reload for both Angular (via dev server) and .NET

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

show_help() {
  cat << EOF
${BLUE}Gasoholic - Development Startup Script${NC}

Usage: ./start.sh [OPTIONS]

OPTIONS:
  --prod          Build Angular once, run .NET server (production-like, slower)
  --quick         Quick test: just run .NET (assumes Angular already built)
  --skip-install  Skip npm install (use if dependencies already installed)
  --help          Show this help message

MODES (default: --dev for fastest iteration):

  ${GREEN}--dev (default)${NC}
    - Installs Angular dependencies (if needed)
    - Starts Angular dev server on http://localhost:4200 (with live reload)
    - Starts .NET API on http://localhost:5082
    - Angular dev server proxies /api/ requests to .NET
    - Perfect for development: changes rebuild instantly
    - Stop with: Ctrl+C

  ${YELLOW}--prod${NC}
    - Builds Angular production bundle to wwwroot/browser/
    - Starts .NET server on http://localhost:5082
    - Serves Angular from static files (like production)
    - Good for: testing production setup, single-window workflow
    - Stop with: Ctrl+C

  ${BLUE}--quick${NC}
    - Skips Angular build, assumes wwwroot/browser/ already exists
    - Just starts .NET server on http://localhost:5082
    - Good for: quick testing if Angular already built
    - Stop with: Ctrl+C

EXAMPLES:

  # Default development (fastest iteration)
  ./start.sh

  # Production-like setup
  ./start.sh --prod

  # Quick test (Angular already built)
  ./start.sh --quick

  # Skip installing dependencies
  ./start.sh --skip-install

EOF
}

# Default mode
MODE="dev"
SKIP_INSTALL=false

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --prod)
      MODE="prod"
      shift
      ;;
    --quick)
      MODE="quick"
      shift
      ;;
    --skip-install)
      SKIP_INSTALL=true
      shift
      ;;
    --help)
      show_help
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      show_help
      exit 1
      ;;
  esac
done

check_prerequisites() {
  echo -e "${BLUE}Checking prerequisites...${NC}"

  # Check Node.js
  if ! command -v node &> /dev/null; then
    echo -e "${RED}✗ Node.js not found${NC}"
    echo "  Install from: https://nodejs.org/"
    exit 1
  fi
  echo -e "${GREEN}✓ Node.js $(node --version)${NC}"

  # Check npm
  if ! command -v npm &> /dev/null; then
    echo -e "${RED}✗ npm not found${NC}"
    echo "  Install from: https://www.npmjs.com/"
    exit 1
  fi
  echo -e "${GREEN}✓ npm $(npm --version)${NC}"

  # Check .NET
  if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found${NC}"
    echo "  Install from: https://dotnet.microsoft.com/download"
    exit 1
  fi
  echo -e "${GREEN}✓ .NET $(dotnet --version)${NC}"

  echo ""
}


cleanup() {
  echo ""
  echo -e "${YELLOW}Shutting down...${NC}"

  # Kill all background processes in this script
  jobs -p | xargs -r kill 2>/dev/null || true

  echo -e "${GREEN}Goodbye!${NC}"
  exit 0
}

# Set trap to cleanup on exit
trap cleanup EXIT INT TERM

start_dev_mode() {
  echo -e "${GREEN}Starting in DEVELOPMENT mode (fastest iteration)${NC}"
  echo ""

  # Install dependencies if not skipped
  if [ "$SKIP_INSTALL" = false ]; then
    echo -e "${BLUE}Installing Angular dependencies...${NC}"
    cd client
    npm install
    cd ..
    echo ""
  fi

  echo -e "${BLUE}Starting servers...${NC}"
  echo -e "  ${GREEN}Angular dev server: http://localhost:4200${NC} (with live reload)"
  echo -e "  ${GREEN}.NET API: http://localhost:5082${NC}"
  echo -e "  ${YELLOW}Angular proxies /api/ requests to .NET${NC}"
  echo ""
  echo -e "${YELLOW}Open http://localhost:4200 in your browser${NC}"
  echo -e "${YELLOW}Press Ctrl+C to stop both servers${NC}"
  echo ""

  # Start .NET in background
  echo -e "${BLUE}Starting .NET backend...${NC}"
  dotnet run --project gasoholic.csproj &
  DOTNET_PID=$!

  # Give .NET a moment to start
  sleep 3

  # Start Angular dev server in foreground (so Ctrl+C works naturally)
  echo -e "${BLUE}Starting Angular dev server...${NC}"
  cd client
  npm start

  # If we get here, Angular exited
  kill $DOTNET_PID 2>/dev/null || true
}

start_prod_mode() {
  echo -e "${GREEN}Starting in PRODUCTION mode (single process, static files)${NC}"
  echo ""

  echo -e "${BLUE}Building Angular...${NC}"
  cd client
  npm install --silent
  npm run build
  cd ..
  echo -e "${GREEN}✓ Angular built${NC}"
  echo ""

  echo -e "${BLUE}Starting .NET server...${NC}"
  echo -e "  ${GREEN}http://localhost:5082${NC}"
  echo -e "  ${YELLOW}Serves Angular from static files (wwwroot/browser/)${NC}"
  echo ""
  echo -e "${YELLOW}Open http://localhost:5082 in your browser${NC}"
  echo -e "${YELLOW}Press Ctrl+C to stop${NC}"
  echo ""

  dotnet run --project gasoholic.csproj
}

start_quick_mode() {
  echo -e "${GREEN}Starting in QUICK mode (assumes Angular already built)${NC}"
  echo ""

  # Check if Angular dist exists
  if [ ! -f "wwwroot/browser/index.html" ]; then
    echo -e "${RED}✗ Angular not built yet${NC}"
    echo "  Run: ./start.sh --prod"
    echo "  Or: cd client && npm run build && cd .."
    exit 1
  fi

  echo -e "${BLUE}Starting .NET server...${NC}"
  echo -e "  ${GREEN}http://localhost:5082${NC}"
  echo -e "  ${YELLOW}Press Ctrl+C to stop${NC}"
  echo ""

  dotnet run --project gasoholic.csproj
}

main() {
  clear
  echo -e "${BLUE}╔════════════════════════════════════════╗${NC}"
  echo -e "${BLUE}║          Gasoholic Development         ║${NC}"
  echo -e "${BLUE}╚════════════════════════════════════════╝${NC}"
  echo ""

  check_prerequisites

  case $MODE in
    dev)
      start_dev_mode
      ;;
    prod)
      start_prod_mode
      ;;
    quick)
      start_quick_mode
      ;;
    *)
      echo "Unknown mode: $MODE"
      exit 1
      ;;
  esac
}

main
