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
Write-Host '5. Always delete the remote UI folder before copying a new bundle.'
Write-Host ''

if (-not $SkipGitCheck) {
  Write-Host 'Git status:'
  git status --short
  Write-Host ''
}

if (-not $SkipBuild) {
  if (Test-Path (Join-Path $repoRoot 'frontend\librarian\package.json')) {
    Write-Host 'Building librarian frontend...'
    Push-Location (Join-Path $repoRoot 'frontend\librarian')
    try {
      npm run build
    } finally {
      Pop-Location
    }
  } else {
    Write-Host 'Librarian frontend not found. Skipping librarian build.'
  }

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

function Get-LibrarianAssetReference {
  param(
    [Parameter(Mandatory = $true)]
    [string]$IndexHtmlPath
  )

  $indexHtml = Get-Content -LiteralPath $IndexHtmlPath -Raw
  $scriptMatch = [regex]::Match($indexHtml, '/ui/librarian/assets/(?<name>index-[^"/]+\.js)')
  $styleMatch = [regex]::Match($indexHtml, '/ui/librarian/assets/(?<name>index-[^"/]+\.css)')

  if (-not $scriptMatch.Success -or -not $styleMatch.Success) {
    throw "Could not find the librarian entry assets in $IndexHtmlPath."
  }

  [pscustomobject]@{
    Script = $scriptMatch.Groups['name'].Value
    Style  = $styleMatch.Groups['name'].Value
  }
}

function Assert-SingleLibrarianEntryBundle {
  param(
    [Parameter(Mandatory = $true)]
    [string]$AssetsDir,
    [Parameter(Mandatory = $true)]
    [string]$IndexHtmlPath
  )

  $refs = Get-LibrarianAssetReference -IndexHtmlPath $IndexHtmlPath
  $scriptFiles = @(Get-ChildItem -LiteralPath $AssetsDir -Filter 'index-*.js' -File)
  $styleFiles = @(Get-ChildItem -LiteralPath $AssetsDir -Filter 'index-*.css' -File)

  if ($scriptFiles.Count -ne 1) {
    throw "Expected exactly 1 librarian entry JS bundle, found $($scriptFiles.Count) in $AssetsDir."
  }

  if ($styleFiles.Count -ne 1) {
    throw "Expected exactly 1 librarian entry CSS bundle, found $($styleFiles.Count) in $AssetsDir."
  }

  if ($scriptFiles[0].Name -ne $refs.Script) {
    throw "Librarian index.html points to $($refs.Script) but the only JS entry bundle is $($scriptFiles[0].Name)."
  }

  if ($styleFiles[0].Name -ne $refs.Style) {
    throw "Librarian index.html points to $($refs.Style) but the only CSS entry bundle is $($styleFiles[0].Name)."
  }
}

if (Test-Path (Join-Path $repoRoot 'backend\wwwroot\ui\librarian\index.html')) {
  Write-Host 'Verifying librarian entry bundle...'
  Assert-SingleLibrarianEntryBundle `
    -AssetsDir (Join-Path $repoRoot 'backend\wwwroot\ui\librarian\assets') `
    -IndexHtmlPath (Join-Path $repoRoot 'backend\wwwroot\ui\librarian\index.html')
  Write-Host 'Librarian bundle looks consistent.'
  Write-Host ''
}

Write-Host ''
Write-Host 'Rollback tag reminder: reader-categories-sync-20260617'
