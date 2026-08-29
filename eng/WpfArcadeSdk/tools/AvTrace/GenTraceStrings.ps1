param(
    [Parameter(Mandatory)][string]$i,  # Input file
    [Parameter(Mandatory)][string]$o   # Output .cs file
)

$ErrorActionPreference = 'Stop'

$header = @"

using System;
using System.Diagnostics;

namespace MS.Internal
{

"@

$footer = @"
}//endof namespace
"@

$classFooter = @"

        // Send a single trace output
        static public void Trace( TraceEventType type, AvTraceDetails traceDetails, params object[] parameters )
        {
            _avTrace.Trace( type, traceDetails.Id, traceDetails.Message, traceDetails.Labels, parameters );
        }

        // these help delay allocation of object array
        static public void Trace( TraceEventType type, AvTraceDetails traceDetails )
        {
            _avTrace.Trace( type, traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[0] );
        }
        static public void Trace( TraceEventType type, AvTraceDetails traceDetails, object p1 )
        {
            _avTrace.Trace( type, traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1 } );
        }
        static public void Trace( TraceEventType type, AvTraceDetails traceDetails, object p1, object p2 )
        {
            _avTrace.Trace( type, traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1, p2 } );
        }
        static public void Trace( TraceEventType type, AvTraceDetails traceDetails, object p1, object p2, object p3 )
        {
            _avTrace.Trace( type, traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1, p2, p3 } );
        }

        // Send a singleton "activity" trace (really, this sends the same trace as both a Start and a Stop)
        static public void TraceActivityItem( AvTraceDetails traceDetails, params Object[] parameters )
        {
            _avTrace.TraceStartStop( traceDetails.Id, traceDetails.Message, traceDetails.Labels, parameters );
        }

        // these help delay allocation of object array
        static public void TraceActivityItem( AvTraceDetails traceDetails )
        {
            _avTrace.TraceStartStop( traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[0] );
        }
        static public void TraceActivityItem( AvTraceDetails traceDetails, object p1 )
        {
            _avTrace.TraceStartStop( traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1 } );
        }
        static public void TraceActivityItem( AvTraceDetails traceDetails, object p1, object p2 )
        {
            _avTrace.TraceStartStop( traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1, p2 } );
        }
        static public void TraceActivityItem( AvTraceDetails traceDetails, object p1, object p2, object p3 )
        {
            _avTrace.TraceStartStop( traceDetails.Id, traceDetails.Message, traceDetails.Labels, new object[] { p1, p2, p3 } );
        }

        // Is tracing enabled here?
        static public bool IsEnabled
        {
            get { return _avTrace != null && _avTrace.IsEnabled; }
        }

        // Is there a Tracesource?  (See comment on AvTrace.IsEnabledOverride.)
        static public bool IsEnabledOverride
        {
            get { return _avTrace.IsEnabledOverride; }
        }

        // Re-read the configuration for this trace source
        static public void Refresh()
        {
            _avTrace.Refresh();
        }

    }//endof class {0}
"@

$sb = [System.Text.StringBuilder]::new()
[void]$sb.Append($header)

$lines = Get-Content -LiteralPath $i
$prevId = 0
$maxId = 0
$inClass = $false
$traceArea = ''
$traceClass = ''
$traceName = ''
$traceSourceName = ''

foreach ($stringIn in $lines) {
    # Skip comments and blank/cr lines
    if ($stringIn -match '^;') { continue }
    if ($stringIn -match '^\r$') { continue }
    if ([string]::IsNullOrWhiteSpace($stringIn)) { continue }

    # Handle section begin: [Name,Area,Class]
    if ($stringIn -match '^\[(.*),(.*),(.*)\]$') {
        $traceName = $Matches[1]
        $traceArea = $Matches[2]
        $traceClass = $Matches[3]
        $traceSourceName = "${traceArea}Source"

        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("    static internal partial class $traceClass")
        [void]$sb.AppendLine("    {")
        [void]$sb.AppendLine("        static private AvTrace _avTrace = new AvTrace(")
        [void]$sb.AppendLine("                delegate() { return PresentationTraceSources.$traceSourceName; },")
        [void]$sb.AppendLine("                delegate() { PresentationTraceSources._${traceSourceName} = null; }")
        [void]$sb.AppendLine("                );")
        [void]$sb.AppendLine("")

        $maxId = 0
        $prevId = 0
        $inClass = $true
        continue
    }

    # Handle section end: [end]
    if ($stringIn -match '^\[end\]') {
        [void]$sb.AppendFormat($classFooter, $traceClass)
        [void]$sb.AppendLine("")
        $inClass = $false
        continue
    }

    # Handle trace line: Name=ID,ShouldFormat,{Labels}
    if ($stringIn -match '^(\w+)=(\w*),(\w*),(.*)') {
        if (-not $inClass) {
            Write-Error "GenTraceStrings: Trace string '$stringIn' is not inside a section."
            exit 1
        }

        $name = $Matches[1]
        $idStr = $Matches[2]
        $shouldFormat = $Matches[3]
        $labels = $Matches[4]

        # Resolve ID
        if ($idStr -match '^\d+$') {
            $id = [int]$idStr
            if ($id -gt $maxId) { $maxId = $id }
        }
        elseif ($idStr -eq '' -or $idStr -eq 'AUTO') {
            $maxId++
            $id = $maxId
        }
        elseif ($idStr -eq 'PREVIOUS') {
            $id = $prevId
        }
        else {
            Write-Error "GenTraceStrings: invalid id '$idStr' for trace string."
            exit 1
        }

        if ($shouldFormat -and $shouldFormat -ne '' -and $shouldFormat -ne 'false' -and $shouldFormat -ne 'False') {
            # Create a method that passes args for the format string
            [void]$sb.AppendLine("")
            [void]$sb.AppendLine("        static AvTraceDetails _${name};")
            [void]$sb.AppendLine("        static public AvTraceDetails ${name}(params object[] args)")
            [void]$sb.AppendLine("        {")
            [void]$sb.AppendLine("            if ( _$name == null )")
            [void]$sb.AppendLine("            {")
            [void]$sb.AppendLine("                _$name = new AvTraceDetails( $id, new string[] $labels );")
            [void]$sb.AppendLine("            }")
            [void]$sb.AppendLine("")
            [void]$sb.AppendLine("            return new AvTraceFormat(_$name, args);")
            [void]$sb.AppendLine("        }")
        }
        else {
            # Create a property
            [void]$sb.AppendLine("")
            [void]$sb.AppendLine("        static AvTraceDetails _${name};")
            [void]$sb.AppendLine("        static public AvTraceDetails ${name}")
            [void]$sb.AppendLine("        {")
            [void]$sb.AppendLine("            get")
            [void]$sb.AppendLine("            {")
            [void]$sb.AppendLine("                if ( _$name == null )")
            [void]$sb.AppendLine("                {")
            [void]$sb.AppendLine("                    _$name = new AvTraceDetails( $id, new string[] $labels );")
            [void]$sb.AppendLine("                }")
            [void]$sb.AppendLine("")
            [void]$sb.AppendLine("                return _$name;")
            [void]$sb.AppendLine("            }")
            [void]$sb.AppendLine("        }")
        }

        $prevId = $id
        continue
    }
}

[void]$sb.AppendLine("")
[void]$sb.Append($footer)

$outDir = Split-Path -Parent $o
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

[System.IO.File]::WriteAllText($o, $sb.ToString())
Write-Host "Generated $o"
