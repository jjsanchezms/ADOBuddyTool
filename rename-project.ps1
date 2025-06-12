# PowerShell script to rename project from CreateRoadmapADO to ADOBuddyTool

Write-Host "Renaming project from CreateRoadmapADO to ADOBuddyTool..." -ForegroundColor Green

# Get all .cs files in the src directory
$sourceFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

foreach ($file in $sourceFiles) {
    Write-Host "Processing: $($file.FullName)" -ForegroundColor Yellow
    
    $content = Get-Content $file.FullName -Raw
    
    # Replace namespace declarations
    $content = $content -replace "namespace CreateRoadmapADO\.", "namespace ADOBuddyTool."
    
    # Replace using statements
    $content = $content -replace "using CreateRoadmapADO\.", "using ADOBuddyTool."
    
    # Write back to file
    Set-Content -Path $file.FullName -Value $content -NoNewline
}

# Update VS Code launch.json
Write-Host "Updating VS Code launch.json..." -ForegroundColor Yellow
$launchFile = ".vscode\launch.json"
if (Test-Path $launchFile) {
    $content = Get-Content $launchFile -Raw
    $content = $content -replace "CreateRoadmapADO\.dll", "ADOBuddyTool.dll"
    $content = $content -replace "CreateRoadmapADO\.csproj", "ADOBuddyTool.csproj"
    Set-Content -Path $launchFile -Value $content -NoNewline
}

# Update VS Code tasks.json if it exists
Write-Host "Updating VS Code tasks.json..." -ForegroundColor Yellow
$tasksFile = ".vscode\tasks.json"
if (Test-Path $tasksFile) {
    $content = Get-Content $tasksFile -Raw
    $content = $content -replace "CreateRoadmapADO\.csproj", "ADOBuddyTool.csproj"
    Set-Content -Path $tasksFile -Value $content -NoNewline
}

# Update README.md
Write-Host "Updating README.md..." -ForegroundColor Yellow
if (Test-Path "README.md") {
    $content = Get-Content "README.md" -Raw
    $content = $content -replace "CreateRoadmapADO", "ADOBuddyTool"
    Set-Content -Path "README.md" -Value $content -NoNewline
}

Write-Host "Project rename completed! Remember to:" -ForegroundColor Green
Write-Host "1. Delete the old CreateRoadmapADO.csproj and CreateRoadmapADO.sln files" -ForegroundColor Cyan
Write-Host "2. Clean and rebuild the solution" -ForegroundColor Cyan
Write-Host "3. Update any external references to the old project name" -ForegroundColor Cyan
