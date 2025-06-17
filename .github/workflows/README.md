# GitHub Actions Workflows for WordleSharp

This repository includes three GitHub Actions workflows to automate building, testing, and publishing the WordleSharp PowerShell module with automatic releases.

## Workflows

### 1. Continuous Integration (`ci.yml`)

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `feature/` branches

**What it does:**
- Builds the solution on both Windows and Ubuntu
- Runs .NET unit tests (xUnit)
- Tests PowerShell module functionality
- Runs PSScriptAnalyzer (if PowerShell script files exist)
- Uploads test results and build artifacts

### 2. Auto Release (`auto-release.yml`)

**Triggers:**
- Push to `main` branch (automatic releases)

**What it does:**
- Builds and tests the solution
- Automatically determines next version (minor bump by default)
- Generates release notes from commit messages
- Creates GitHub release with proper tag
- Triggers the CI/CD pipeline for publishing

**Version Bumping Logic:**
- `[major]` or `BREAKING CHANGE` in commit → Major version bump
- `[patch]` or `fix`/`bug` in commit → Patch version bump  
- Default → Minor version bump
- `[skip release]` in commit → Skip automatic release

### 3. CI/CD Pipeline (`ci-cd.yml`)

**Triggers:**
- Pull requests to `main` branch
- Release creation (manual or automatic)

**What it does:**
- Everything from CI workflow
- **On release creation:**
  - Extracts version from release tag
  - Updates module manifest with new version
  - Builds release version
  - Prepares module package
  - Tests module import
  - Publishes to PowerShell Gallery

## Setup Instructions

### 1. PowerShell Gallery API Key

To publish to PowerShell Gallery, you need to set up a secret:

1. Go to [PowerShell Gallery](https://www.powershellgallery.com/)
2. Sign in with your Microsoft account
3. Go to your account settings and create an API key
4. In your GitHub repository, go to **Settings** → **Secrets and variables** → **Actions**
5. Create a new repository secret named `POWERSHELL_GALLERY_API_KEY`
6. Paste your PowerShell Gallery API key as the value

### 2. Automatic Releases (Recommended)

**Simply merge to main** - that's it! The auto-release workflow will:

1. **Automatically determine the next version** based on your commit messages
2. **Create a GitHub release** with generated release notes
3. **Trigger publishing** to PowerShell Gallery

**Control version bumping with commit messages:**
```bash
git commit -m "Add new feature"                    # → Minor bump (1.0.0 → 1.1.0)
git commit -m "Fix critical bug [patch]"           # → Patch bump (1.0.0 → 1.0.1)  
git commit -m "Breaking change [major]"            # → Major bump (1.0.0 → 2.0.0)
git commit -m "Update docs [skip release]"         # → No release created
```

### 3. Manual Releases (Optional)

You can still create manual releases:

1. Create a new release in GitHub with tag format `v1.0.0`
2. The CI/CD pipeline will handle the rest

### 4. Version Management

**Automatic versioning:**
- Analyzes commit messages for version hints
- Default: Minor version bump
- Updates `WordleSharp.psd1` automatically
- Creates proper Git tags

**Manual versioning:**
- For CI builds: Uses the version from `WordleSharp.psd1`
- For releases: Uses the Git tag version and updates the manifest

## Workflow Features

### Caching
- PowerShell modules are cached to speed up builds
- Dependencies are restored efficiently

### Cross-Platform Testing
- CI runs on both Windows and Ubuntu to ensure compatibility
- Module functionality is tested on the built output

### Comprehensive Testing
- .NET unit tests via xUnit
- PowerShell module import testing
- PSScriptAnalyzer for code quality (if applicable)

### Artifact Management
- Test results are uploaded for analysis
- Build artifacts are preserved
- Release packages are prepared correctly

## File Structure After Workflow

When publishing, the workflow creates a module package with:
```
WordleSharp/
├── WordleSharp.dll          # Main binary module
├── WordleSharp.psd1         # Module manifest
├── en-US/                   # Help files
│   └── WordleSharp.dll-Help.xml
└── WordLists/               # Word list files
    ├── Answers.txt
    └── StartWords.txt
```

## Troubleshooting

### Common Issues

1. **PowerShell Gallery API Key Missing**
   - Error: "PowerShell Gallery API key not found"
   - Solution: Add `POWERSHELL_GALLERY_API_KEY` secret

2. **Version Format Issues**
   - Error: Invalid version format
   - Solution: Use semantic versioning (e.g., `v1.0.0`, `v2.1.3`)

3. **Module Import Failures**
   - Error: Module fails to import during testing
   - Solution: Check that all dependencies are properly included

4. **Test Failures**
   - CI will fail if any xUnit tests fail
   - Check test results in the Actions tab

### Viewing Results

- **Build Status**: Check the Actions tab in your GitHub repository
- **Test Results**: Download artifacts from completed workflow runs
- **Published Modules**: View on [PowerShell Gallery](https://www.powershellgallery.com/packages/WordleSharp)

## Customization

You can customize these workflows by:
- Modifying the trigger conditions
- Adding additional test steps
- Changing the target .NET version
- Adding code coverage reporting
- Including additional static analysis tools

## Best Practices

1. **Always test locally** before pushing
2. **Use semantic versioning** for releases
3. **Update help documentation** before releases
4. **Monitor workflow runs** for any issues
5. **Keep dependencies up to date**
