# Creates the demo account, team and sample board. Safe to run again.
#   ./scripts/seed.ps1            uses the Docker stack if it is up, otherwise runs the API locally
#   $env:SEED_DEMO_PASSWORD = '...'; ./scripts/seed.ps1
# Docker and dotnet write progress to stderr, so success is judged by exit codes, not by stderr.
Set-Location (Join-Path $PSScriptRoot '..')

$running = @(docker compose ps --status running --services 2>$null)
if ($running -contains 'api') {
    docker compose run --rm -e "SEED_DEMO_PASSWORD=$env:SEED_DEMO_PASSWORD" api seed
} else {
    Write-Host 'The Docker stack is not running; seeding with the local API against the configured MongoDB.'
    dotnet run --project server/src/driving-adapters/EventStorming.Host -- seed
}

exit $LASTEXITCODE
