# Unity CLI Loop helper script for Monochrome development
# Usage: .\\run_uloop.ps1 <command>
# Commands:
#   compile   - Recompile scripts
#   play      - Start Play mode
#   stop      - Stop Play mode
#   clear     - Clear Unity console
#   find <name> - Find GameObjects by name

param(
    [Parameter(Mandatory=$true)][string]$Action,
    [string]$Arg
)

switch ($Action.ToLower()) {
    'compile' { uloop-compile }
    'play' { uloop-control-play-mode start }
    'stop' { uloop-control-play-mode stop }
    'clear' { uloop-clear-console }
    'find' {
        if (-not $Arg) { Write-Error "Provide a name to search"; exit 1 }
        uloop-find-game-objects $Arg
    }
    default { Write-Error "Unknown action: $Action"; exit 1 }
}
