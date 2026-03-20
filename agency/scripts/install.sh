#!/bin/bash
# Install agency agents for your preferred AI coding tool
# Usage: ./scripts/install.sh [tool]
# Supported tools: claude, cursor, copilot, all

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AGENCY_DIR="$(dirname "$SCRIPT_DIR")"

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}"
echo "  _____ _            _                            "
echo " |_   _| |__   ___  / \   __ _  ___ _ __   ___ _   _ "
echo "   | | | '_ \ / _ \/ _ \ / _\` |/ _ \ '_ \ / __| | | |"
echo "   | | | | | |  __/ ___ \ (_| |  __/ | | | (__| |_| |"
echo "   |_| |_| |_|\___/_/   \_\__, |\___|_| |_|\___|\__, |"
echo "                          |___/                  |___/ "
echo -e "${NC}"
echo "  is-a.dev Agency — AI Agent Org Structure"
echo ""

TOOL="${1:-claude}"

install_claude() {
    echo -e "${GREEN}Installing agents for Claude Code...${NC}"
    CLAUDE_DIR="$HOME/.claude/agents"
    mkdir -p "$CLAUDE_DIR"

    count=0
    for division in "$AGENCY_DIR"/*/; do
        division_name=$(basename "$division")
        if [[ "$division_name" == "scripts" || "$division_name" == "examples" ]]; then
            continue
        fi
        for agent in "$division"/*.md; do
            if [[ -f "$agent" ]]; then
                cp "$agent" "$CLAUDE_DIR/"
                count=$((count + 1))
            fi
        done
    done

    echo -e "${GREEN}Installed $count agents to $CLAUDE_DIR${NC}"
}

install_cursor() {
    echo -e "${GREEN}Installing agents for Cursor...${NC}"
    CURSOR_DIR=".cursor/agents"
    mkdir -p "$CURSOR_DIR"

    count=0
    for division in "$AGENCY_DIR"/*/; do
        division_name=$(basename "$division")
        if [[ "$division_name" == "scripts" || "$division_name" == "examples" ]]; then
            continue
        fi
        for agent in "$division"/*.md; do
            if [[ -f "$agent" ]]; then
                cp "$agent" "$CURSOR_DIR/"
                count=$((count + 1))
            fi
        done
    done

    echo -e "${GREEN}Installed $count agents to $CURSOR_DIR${NC}"
}

install_copilot() {
    echo -e "${GREEN}Installing agents for GitHub Copilot...${NC}"
    COPILOT_DIR=".github/copilot/agents"
    mkdir -p "$COPILOT_DIR"

    count=0
    for division in "$AGENCY_DIR"/*/; do
        division_name=$(basename "$division")
        if [[ "$division_name" == "scripts" || "$division_name" == "examples" ]]; then
            continue
        fi
        for agent in "$division"/*.md; do
            if [[ -f "$agent" ]]; then
                cp "$agent" "$COPILOT_DIR/"
                count=$((count + 1))
            fi
        done
    done

    echo -e "${GREEN}Installed $count agents to $COPILOT_DIR${NC}"
}

case "$TOOL" in
    claude)
        install_claude
        ;;
    cursor)
        install_cursor
        ;;
    copilot)
        install_copilot
        ;;
    all)
        install_claude
        install_cursor
        install_copilot
        ;;
    *)
        echo -e "${YELLOW}Usage: $0 [claude|cursor|copilot|all]${NC}"
        echo ""
        echo "Supported tools:"
        echo "  claude   - Install to ~/.claude/agents/"
        echo "  cursor   - Install to .cursor/agents/"
        echo "  copilot  - Install to .github/copilot/agents/"
        echo "  all      - Install for all tools"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}Done! Your AI agents are ready to work.${NC}"
echo -e "Activate an agent by referencing its name in your AI tool."
