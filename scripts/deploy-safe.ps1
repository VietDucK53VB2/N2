param(
  [switch]$SkipBuild,
  [switch]$SkipGitCheck
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host 'Safe deploy checklist'
Write-Host '----------------------'
Write-Host '1. Review scope and keep changes limited to the intended files.'
Write-Host '2. Build locally and verify the UI before touching VPS.'
Write-Host '3. Commit on a branch, push to GitHub, and keep a rollback tag.'
Write-Host '4. Deploy only the exact built artifacts or commit you checked.'
Write-Host ''

if (-not $SkipGitCheck) {
  Write-Host 'Git status:'
  git status --short
  Write-Host ''
}

if (-not $SkipBuild) {
  if (Test-Path (Join-Path $repoRoot 'frontend\reader\package.json')) {
    Write-Host 'Building reader frontend...'
    Push-Location (Join-Path $repoRoot 'frontend\reader')
    try {
      npm run build
    } finally {
      Pop-Location
    }
  } else {
    Write-Host 'Reader frontend not found. Skipping build.'
  }
}

Write-Host ''
Write-Host 'Rollback tag reminder: reader-categories-sync-20260617'
