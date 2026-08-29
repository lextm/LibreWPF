param(
    [Parameter(Mandatory, Position=0)][string]$InputFile,
    [Parameter(Mandatory, Position=1)][string]$OutputFile
)

$ErrorActionPreference = 'Stop'

$version = '3.0.0.0'

$lines = Get-Content -LiteralPath $InputFile

$outDir = Split-Path -Parent $OutputFile
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("<!--===========================================================================")
[void]$sb.AppendLine("Copyright (C) Microsoft Corporation.  All rights reserved.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("PresentationUI Styles For Windows Presentation Foundation Version $version")
[void]$sb.AppendLine("===========================================================================-->")

$inComment = $false
$lineNum = 0
foreach ($line in $lines) {
    $lineNum++

    # Skip BOM line (line 1 is just the BOM)
    if ($lineNum -eq 1) { continue }

    # Validate: comments must be on their own line (matches original Perl logic)
    # \S matches non-whitespace including '<', so '\S+\s*<!--' catches 'content <!--' but not '<!-- content'
    if ($lineNum -gt 1 -and $line -match '\s*\S+\s*<!--') {
        Write-Error "error $InputFile`:$lineNum`: Comments must be on their own line (or this script needs xml processing)"
        exit 1
    }
    if ($lineNum -gt 1 -and $line -match '-->\s*\S+\s*') {
        Write-Error "error $InputFile`:$lineNum`: Comments must be on their own line (or this script needs xml processing)"
        exit 1
    }

    if ($line -match '<!--') {
        $inComment = $true
    }

    if (-not $inComment) {
        [void]$sb.AppendLine($line)
    }

    if ($line -match '-->') {
        $inComment = $false
    }
}

[System.IO.File]::WriteAllText($OutputFile, $sb.ToString())
