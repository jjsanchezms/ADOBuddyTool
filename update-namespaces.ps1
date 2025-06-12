# PowerShell script to update namespaces in the Clean Architecture structure

# Infrastructure Azure DevOps Services
$files = @(
    "src\Infrastructure\AzureDevOps\Services\*.cs",
    "src\Infrastructure\AzureDevOps\Interfaces\*.cs",
    "src\Infrastructure\AzureDevOps\AzureDevOpsService.cs"
)

foreach ($pattern in $files) {
    Get-ChildItem $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        # Update Azure DevOps service namespaces
        $content = $content -replace "namespace CreateRoadmapADO\.Services\.AzureDevOps;", "namespace CreateRoadmapADO.Infrastructure.AzureDevOps.Services;"
        $content = $content -replace "namespace CreateRoadmapADO\.Interfaces;", "namespace CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
        $content = $content -replace "namespace CreateRoadmapADO\.Services;", "namespace CreateRoadmapADO.Infrastructure.AzureDevOps;"
        
        # Update using statements
        $content = $content -replace "using CreateRoadmapADO\.Services\.AzureDevOps;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Services;"
        $content = $content -replace "using CreateRoadmapADO\.Configuration;", "using CreateRoadmapADO.Presentation.Configuration;"
        $content = $content -replace "using CreateRoadmapADO\.Models;", "using CreateRoadmapADO.Domain.Entities;"
        $content = $content -replace "using CreateRoadmapADO\.Interfaces;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
        
        Set-Content $_.FullName $content
        Write-Host "Updated: $($_.FullName)"
    }
}

# Infrastructure Hygiene Checks
Get-ChildItem "src\Infrastructure\HygieneChecks\*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace "namespace CreateRoadmapADO\.Services;", "namespace CreateRoadmapADO.Infrastructure.HygieneChecks;"
    $content = $content -replace "using CreateRoadmapADO\.Configuration;", "using CreateRoadmapADO.Presentation.Configuration;"
    $content = $content -replace "using CreateRoadmapADO\.Models;", "using CreateRoadmapADO.Domain.Entities;"
    $content = $content -replace "using CreateRoadmapADO\.Interfaces;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
    $content = $content -replace "using CreateRoadmapADO\.Services\.AzureDevOps;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Services;"
    Set-Content $_.FullName $content
    Write-Host "Updated: $($_.FullName)"
}

Get-ChildItem "src\Infrastructure\HygieneChecks\Checks\*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace "namespace CreateRoadmapADO\.Services\.HygieneChecks;", "namespace CreateRoadmapADO.Infrastructure.HygieneChecks.Checks;"
    $content = $content -replace "using CreateRoadmapADO\.Models;", "using CreateRoadmapADO.Domain.Entities;"
    $content = $content -replace "using CreateRoadmapADO\.Interfaces;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
    Set-Content $_.FullName $content
    Write-Host "Updated: $($_.FullName)"
}

# Infrastructure Output and Roadmap
Get-ChildItem "src\Infrastructure\Output\*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace "namespace CreateRoadmapADO\.Services;", "namespace CreateRoadmapADO.Infrastructure.Output;"
    $content = $content -replace "using CreateRoadmapADO\.Models;", "using CreateRoadmapADO.Domain.Entities;"
    Set-Content $_.FullName $content
    Write-Host "Updated: $($_.FullName)"
}

Get-ChildItem "src\Infrastructure\Roadmap\*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace "namespace CreateRoadmapADO\.Services;", "namespace CreateRoadmapADO.Infrastructure.Roadmap;"
    $content = $content -replace "using CreateRoadmapADO\.Models;", "using CreateRoadmapADO.Domain.Entities;"
    $content = $content -replace "using CreateRoadmapADO\.Interfaces;", "using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
    Set-Content $_.FullName $content
    Write-Host "Updated: $($_.FullName)"
}

# BaseService
Get-ChildItem "src\Infrastructure\BaseService.cs" -ErrorAction SilentlyContinue | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace "namespace CreateRoadmapADO\.Services;", "namespace CreateRoadmapADO.Infrastructure;"
    Set-Content $_.FullName $content
    Write-Host "Updated: $($_.FullName)"
}

Write-Host "Namespace update completed!"
