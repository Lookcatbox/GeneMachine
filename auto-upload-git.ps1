param(
    [string]$Message = "auto: update $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    [string]$Branch = "main",
    [string]$Remote = "origin",
    [string]$RemoteUrl = "https://github.com/Lookcatbox/GeneMachine.git"
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Text)
    Write-Host "[auto-upload] $Text" -ForegroundColor Cyan
}

try {
    if (-not (Test-Path ".git")) {
        Write-Step "Initializing git repository"
        git init | Out-Null
    }

    Write-Step "Ensuring git identity"
    git config user.name "Lookcatbox"
    git config user.email "1245495327@qq.com"

    $existingRemote = git remote
    if (-not ($existingRemote -contains $Remote)) {
        Write-Step "Adding remote $Remote"
        git remote add $Remote $RemoteUrl
    }

    Write-Step "Staging changes"
    git add .

    $hasChanges = git status --porcelain
    if (-not $hasChanges) {
        Write-Step "No changes to commit"
        exit 0
    }

    Write-Step "Committing changes"
    git commit -m $Message

    Write-Step "Pushing to $Remote/$Branch"
    git push -u $Remote $Branch

    Write-Step "Done"
}
catch {
    Write-Host "[auto-upload] Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
