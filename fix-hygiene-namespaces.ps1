# Fix hygiene check namespaces
$files = @(
    "src\Infrastructure\HygieneChecks\Checks\ReleaseTrainCompletenessCheck.cs",
    "src\Infrastructure\HygieneChecks\Checks\IterationPathAlignmentCheck.cs", 
    "src\Infrastructure\HygieneChecks\Checks\StatusNotesDocumentationCheck.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $content = $content -replace "namespace CreateRoadmapADO\.Services\.HygieneChecks;", "namespace CreateRoadmapADO.Infrastructure.HygieneChecks.Checks;"
        Set-Content $file $content
        Write-Host "Updated: $file"
    }
}

Write-Host "Hygiene check namespace fix completed!"
