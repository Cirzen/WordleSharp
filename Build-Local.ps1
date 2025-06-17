# Local Build and Test Script for WordleSharp
# This script mimics what the CI/CD pipeline does locally

param(
    [switch]$SkipTests,
    [switch]$SkipAnalysis,
    [string]$Configuration = "Release"
)

Write-Host "🔧 WordleSharp Local Build Script" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Check if we're in the right directory
if (-not (Test-Path "WordleSharp.sln")) {
    Write-Error "This script must be run from the WordleSharp root directory"
    exit 1
}

# Function to write colored output
function Write-Step {
    param([string]$Message)
    Write-Host "▶️ $Message" -ForegroundColor Green
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

try {
    # Step 1: Restore dependencies
    Write-Step "Restoring NuGet packages..."
    dotnet restore WordleSharp.sln
    if ($LASTEXITCODE -ne 0) { throw "Failed to restore packages" }
    Write-Success "Packages restored successfully"

    # Step 2: Build solution
    Write-Step "Building solution in $Configuration mode..."
    dotnet build WordleSharp.sln --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Success "Build completed successfully"

    # Step 3: Run tests (unless skipped)
    if (-not $SkipTests) {
        Write-Step "Running unit tests..."
        dotnet test WordleSharp.Tests/WordleSharp.Tests.csproj --configuration $Configuration --no-build --verbosity normal
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
        Write-Success "All tests passed"
    } else {
        Write-Warning "Skipping tests"
    }

    # Step 4: Test module import
    Write-Step "Testing PowerShell module import..."
    $modulePath = "bin/$Configuration/net8.0/WordleSharp.dll"
    
    if (Test-Path $modulePath) {
        Import-Module $modulePath -Force
        $module = Get-Module WordleSharp
        
        if ($module) {
            Write-Success "Module imported successfully (Version: $($module.Version))"
            
            # List available commands
            $commands = Get-Command -Module WordleSharp -ErrorAction SilentlyContinue
            if ($commands) {
                Write-Host "📋 Available commands ($($commands.Count)):" -ForegroundColor Cyan
                $commands | ForEach-Object { 
                    Write-Host "   • $($_.Name)" -ForegroundColor White
                }
            } else {
                Write-Warning "No commands found in module"
            }
        } else {
            throw "Failed to import module"
        }
    } else {
        throw "Module DLL not found at $modulePath"
    }

    # Step 5: Run PSScriptAnalyzer (unless skipped)
    if (-not $SkipAnalysis) {
        Write-Step "Running PSScriptAnalyzer..."
        
        # Check if PSScriptAnalyzer is installed
        if (-not (Get-Module PSScriptAnalyzer -ListAvailable)) {
            Write-Warning "PSScriptAnalyzer not installed. Installing..."
            Install-Module PSScriptAnalyzer -Force -Scope CurrentUser
        }
        
        $psFiles = Get-ChildItem -Path . -Filter "*.ps1" -Recurse -ErrorAction SilentlyContinue
        if ($psFiles) {
            Write-Host "Found $($psFiles.Count) PowerShell files to analyze"
            $issues = Invoke-ScriptAnalyzer -Path $psFiles -Recurse
            
            $errors = $issues | Where-Object { $_.Severity -eq 'Error' }
            $warnings = $issues | Where-Object { $_.Severity -eq 'Warning' }
            $info = $issues | Where-Object { $_.Severity -eq 'Information' }
            
            Write-Host "📊 PSScriptAnalyzer Results:" -ForegroundColor Cyan
            Write-Host "   Errors: $($errors.Count)" -ForegroundColor $(if($errors.Count -gt 0){'Red'}else{'Green'})
            Write-Host "   Warnings: $($warnings.Count)" -ForegroundColor $(if($warnings.Count -gt 0){'Yellow'}else{'Green'})
            Write-Host "   Information: $($info.Count)" -ForegroundColor Cyan
            
            if ($errors) {
                Write-Host "❌ Errors found:" -ForegroundColor Red
                $errors | ForEach-Object {
                    Write-Host "   • $($_.Message) [$($_.ScriptName):$($_.Line)]" -ForegroundColor Red
                }
                throw "PSScriptAnalyzer found errors"
            }
            
            if ($warnings) {
                Write-Host "⚠️ Warnings found:" -ForegroundColor Yellow
                $warnings | ForEach-Object {
                    Write-Host "   • $($_.Message) [$($_.ScriptName):$($_.Line)]" -ForegroundColor Yellow
                }
            }
            
            Write-Success "PSScriptAnalyzer completed"
        } else {
            Write-Host "No PowerShell script files found to analyze" -ForegroundColor Cyan
        }
    } else {
        Write-Warning "Skipping code analysis"
    }

    # Step 6: Show build output location
    Write-Host "📁 Build Output Location:" -ForegroundColor Cyan
    Write-Host "   $((Resolve-Path "bin/$Configuration/net8.0").Path)" -ForegroundColor White
    
    Write-Host ""
    Write-Success "🎉 Local build completed successfully!"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "• Import the module: Import-Module '$modulePath' -Force" -ForegroundColor White
    Write-Host "• Create a release to trigger publishing to PowerShell Gallery" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "❌ Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
