# ===================================================================
# Export-Solution.ps1
#
# Creates a folder containing individual TXT files for all
# .cs, .xaml and .json files while preserving folder structure.
# ===================================================================

# -------------------------------------------------------------------
# Solution Root Folder
# -------------------------------------------------------------------
$SourceFolder = "C:\Users\220096\source\repos\SWDrawingChecker"

# -------------------------------------------------------------------
# Export Folder
# -------------------------------------------------------------------
$OutputFolder = Join-Path $SourceFolder "CodeExport"

Write-Host ""
Write-Host "Starting export..."
Write-Host ""

# -------------------------------------------------------------------
# Remove previous export folder
# -------------------------------------------------------------------
if (Test-Path $OutputFolder)
{
    Remove-Item $OutputFolder -Recurse -Force
}

# -------------------------------------------------------------------
# Create export folder
# -------------------------------------------------------------------
New-Item `
    -ItemType Directory `
    -Path $OutputFolder `
    -Force | Out-Null

# -------------------------------------------------------------------
# Export files
# -------------------------------------------------------------------
Get-ChildItem `
    -Path $SourceFolder `
    -Recurse `
    -File |
Where-Object {
    $_.Extension -in @(
        ".cs",
        ".xaml",
        ".json"
    )
} |
ForEach-Object {

    # Relative path
    $RelativePath =
        $_.FullName.Substring($SourceFolder.Length).TrimStart("\")

    # Output TXT file
    $TargetFile =
        Join-Path $OutputFolder ($RelativePath + ".txt")

    # Ensure destination folder exists
    $TargetFolder =
        Split-Path $TargetFile -Parent

    if (!(Test-Path $TargetFolder))
    {
        New-Item `
            -ItemType Directory `
            -Path $TargetFolder `
            -Force | Out-Null
    }

    # Write header
    "=================================================" | Set-Content $TargetFile
    "FILE: $($_.FullName)" | Add-Content $TargetFile
    "=================================================" | Add-Content $TargetFile
    "" | Add-Content $TargetFile

    # Write source code
    Get-Content $_.FullName | Add-Content $TargetFile

    Write-Host "Exported: $RelativePath"
}

Write-Host ""
Write-Host "======================================="
Write-Host "Export Complete"
Write-Host "======================================="
Write-Host ""
Write-Host "Output Folder:"
Write-Host $OutputFolder
Write-Host ""