# Release Helper Script for WordleSharp
# This script helps prepare and validate a manual release
# NOTE: Automatic releases happen when you merge to main - this is for manual releases only

param(
    [Parameter(Mandatory)]
    [string]$Version,
    [string]$ReleaseNotes = "",
    [switch]$DryRun
)

Write-Host "🚀 WordleSharp Release Helper" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in format X.Y.Z (e.g., 1.0.0)"
    exit 1
}

$tagName = "v$Version"

function Write-Step {
    param([string]$Message)
    Write-Host "▶️ $Message" -ForegroundColor Green
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

try {
    # Check if we're in a git repository
    if (-not (Test-Path ".git")) {
        throw "This must be run from a git repository"
    }

    # Check if working directory is clean
    Write-Step "Checking git status..."
    $gitStatus = git status --porcelain
    if ($gitStatus -and -not $DryRun) {
        Write-Warning "Working directory has uncommitted changes:"
        $gitStatus | ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
        $response = Read-Host "Continue anyway? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Host "Release cancelled by user"
            exit 0
        }
    }

    # Check if tag already exists
    Write-Step "Checking if tag $tagName already exists..."
    $existingTag = git tag -l $tagName
    if ($existingTag) {
        throw "Tag $tagName already exists"
    }
    Write-Success "Tag $tagName is available"

    # Validate current module version
    Write-Step "Reading current module manifest..."
    $manifestPath = "WordleSharp.psd1"
    if (-not (Test-Path $manifestPath)) {
        throw "Module manifest not found: $manifestPath"
    }

    $manifestContent = Get-Content $manifestPath -Raw
    if ($manifestContent -match "ModuleVersion\s*=\s*'([^']*)'") {
        $currentVersion = $matches[1]
        Write-Host "Current module version: $currentVersion" -ForegroundColor Cyan
        
        if ($currentVersion -eq $Version) {
            Write-Warning "Module is already at version $Version"
        } else {
            Write-Host "Module will be updated from $currentVersion to $Version" -ForegroundColor Cyan
        }
    } else {
        throw "Could not parse ModuleVersion from manifest"
    }

    # Run local build to ensure everything works
    Write-Step "Running local build to validate..."
    if (Test-Path "Build-Local.ps1") {
        & ./Build-Local.ps1 -Configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "Local build failed"
        }
    } else {
        # Fallback to basic build
        dotnet build WordleSharp.sln --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
    }
    Write-Success "Local build passed"

    # Show what will happen
    Write-Host ""
    Write-Host "🎯 Release Summary:" -ForegroundColor Cyan
    Write-Host "   Version: $Version" -ForegroundColor White
    Write-Host "   Tag: $tagName" -ForegroundColor White
    Write-Host "   Current branch: $(git branch --show-current)" -ForegroundColor White
    
    if ($ReleaseNotes) {
        Write-Host "   Release notes: $ReleaseNotes" -ForegroundColor White
    } else {
        Write-Host "   Release notes: (will be prompted)" -ForegroundColor Yellow
    }

    if ($DryRun) {
        Write-Warning "DRY RUN - No actual release will be created"
        Write-Host ""
        Write-Host "What would happen:" -ForegroundColor Cyan
        Write-Host "1. Create git tag: $tagName" -ForegroundColor White
        Write-Host "2. Push tag to origin" -ForegroundColor White
        Write-Host "3. GitHub Actions will:" -ForegroundColor White
        Write-Host "   • Update module version to $Version" -ForegroundColor White
        Write-Host "   • Build and test the solution" -ForegroundColor White
        Write-Host "   • Publish to PowerShell Gallery" -ForegroundColor White
        exit 0
    }

    # Confirm release
    Write-Host ""
    $response = Read-Host "Create release $tagName? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "Release cancelled by user"
        exit 0
    }

    # Create and push tag
    Write-Step "Creating git tag..."
    git tag -a $tagName -m "Release version $Version"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create git tag"
    }

    Write-Step "Pushing tag to origin..."
    git push origin $tagName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push tag"
    }    Write-Success "🎉 Manual release $tagName created successfully!"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Go to GitHub and create a release from the tag" -ForegroundColor White
    Write-Host "2. GitHub Actions will automatically:" -ForegroundColor White
    Write-Host "   • Build and test the module" -ForegroundColor White
    Write-Host "   • Update the module version" -ForegroundColor White
    Write-Host "   • Publish to PowerShell Gallery" -ForegroundColor White
    Write-Host ""
    Write-Host "💡 For future releases, consider using automatic releases:" -ForegroundColor Cyan
    Write-Host "   Just merge to main with proper commit messages!" -ForegroundColor White
    Write-Host "   • 'Add feature' → minor bump (default)" -ForegroundColor White
    Write-Host "   • 'Fix bug [patch]' → patch bump" -ForegroundColor White
    Write-Host "   • 'Breaking change [major]' → major bump" -ForegroundColor White
    Write-Host ""
    Write-Host "Monitor the build at: https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\([^.]*\).*/\1/')/actions" -ForegroundColor Cyan

} catch {
    Write-Host ""
    Write-Host "❌ Release failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
