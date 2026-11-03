param (
    [string]$SourceDirectory = ".",
    [string]$OutputFile = "Exported_Project.txt"
)

# Resolve paths
$SourceDirectory = (Resolve-Path $SourceDirectory).Path;
$DestinationPath = Join-Path (Get-Location) $OutputFile;

# Exclude build output and cache directories
$ExcludeDirectories = @('bin', 'obj', '.vs', 'packages', 'TestResults');

# Collect files
$Files = Get-ChildItem -Path $SourceDirectory -Recurse -File -Include *.cs, *.xaml | 
    Where-Object {
        $relativePath = $_.FullName.Substring($SourceDirectory.Length);
        $parts = $relativePath.Split([System.IO.Path]::DirectorySeparatorChar);
        -not ($parts | Where-Object { $ExcludeDirectories -contains $_ });
    } |
    Sort-Object -Property @{ Expression = { $_.FullName -replace '\.xaml\.cs$', '.xaml' } }, FullName;

# Reset output file
if (Test-Path $DestinationPath) { 
    Remove-Item $DestinationPath -Force;
}

$Border = "=" * 80;
$TotalCount = $Files.Count;
Write-Host "Found $TotalCount files. Bundling into '$OutputFile'..." -ForegroundColor Cyan;

foreach ($File in $Files) {
    $RelativePath = $File.FullName.Substring($SourceDirectory.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar);
    
    # Construct boundary header
    $Header = "`r`n$Border`r`nFILE: $RelativePath`r`nPATH: $($File.FullName)`r`nSIZE: $($File.Length) bytes`r`n$Border`r`n`r`n";
    
    [System.IO.File]::AppendAllText($DestinationPath, $Header, [System.Text.Encoding]::UTF8);

    try {
        $Content = [System.IO.File]::ReadAllText($File.FullName);
        [System.IO.File]::AppendAllText($DestinationPath, "$Content`r`n", [System.Text.Encoding]::UTF8);
    }
    catch {
        $ErrorNotice = "[ERROR READING FILE: $($_.Exception.Message)]`r`n";
        [System.IO.File]::AppendAllText($DestinationPath, $ErrorNotice, [System.Text.Encoding]::UTF8);
    }
}

Write-Host "Export completed: $DestinationPath" -ForegroundColor Green;