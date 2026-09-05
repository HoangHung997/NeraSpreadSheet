param(
    [string]$Repository = "HoangHung997/NeraSpreadSheet",
    [string]$DevelopBranch = "develop",
    [string]$FeatureBranch = "feature/bootstrap-architecture-v0.1",
    [switch]$CreatePullRequest
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git is required."
}

$sourceRoot = Split-Path -Parent $PSScriptRoot
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("NeraSpreadSheet-publish-" + [guid]::NewGuid())
$remoteUrl = "https://github.com/$Repository.git"

try {
    git clone $remoteUrl $tempRoot
    if ($LASTEXITCODE -ne 0) { throw "git clone failed." }

    Push-Location $tempRoot

    git fetch origin --prune
    if ($LASTEXITCODE -ne 0) { throw "git fetch failed." }

    git show-ref --verify --quiet "refs/remotes/origin/$DevelopBranch"
    if ($LASTEXITCODE -eq 0) {
        git checkout -B $DevelopBranch "origin/$DevelopBranch"
    }
    else {
        git checkout -B $DevelopBranch origin/main
        git push -u origin $DevelopBranch
        if ($LASTEXITCODE -ne 0) { throw "Unable to create remote develop branch." }
    }

    git checkout -B $FeatureBranch $DevelopBranch
    if ($LASTEXITCODE -ne 0) { throw "Unable to create feature branch." }

    $robocopyArgs = @(
        $sourceRoot,
        $tempRoot,
        "/MIR",
        "/XD", ".git", "artifacts", ".publish-temp",
        "/R:2",
        "/W:1",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS"
    )
    & robocopy @robocopyArgs | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE." }

    git add -A
    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "No changes to publish."
        return
    }

    git commit -m "chore: bootstrap NeraSpreadSheet architecture"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

    git push -u origin $FeatureBranch --force-with-lease
    if ($LASTEXITCODE -ne 0) { throw "git push failed." }

    if ($CreatePullRequest) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw "GitHub CLI is required for -CreatePullRequest. The branch was pushed successfully."
        }

        gh pr create `
            --repo $Repository `
            --base $DevelopBranch `
            --head $FeatureBranch `
            --title "chore: bootstrap NeraSpreadSheet architecture" `
            --body-file (Join-Path $tempRoot ".github/pull_request_bootstrap.md")
        if ($LASTEXITCODE -ne 0) { throw "gh pr create failed." }
    }

    Write-Host "Published $FeatureBranch to $Repository."
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force -LiteralPath $tempRoot -ErrorAction SilentlyContinue
}
