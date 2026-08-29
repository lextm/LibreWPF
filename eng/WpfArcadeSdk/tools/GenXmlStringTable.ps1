param(
    [Parameter(Mandatory)][string]$n,  # Namespace file
    [Parameter(Mandatory)][string]$x,  # XmlString file
    [Parameter(Mandatory)][string]$e,  # Full enum class name
    [Parameter(Mandatory)][string]$c,  # Full string table class name
    [Parameter(Mandatory)][string]$o   # Output .cs file
)

$ErrorActionPreference = 'Stop'

# Parse class names
$enumnamespace = $e -replace '\.[^\.]+$', ''
$enumsrclass   = $e -replace '^.*\.', ''
$tablenamespace = $c -replace '\.[^\.]+$', ''
$tablesrclass   = $c -replace '^.*\.', ''

# Parse entries from a text file (skip comments/blank lines, match NAME=VALUE patterns)
function Parse-Entries {
    param([string]$Path)
    $entries = @()
    foreach ($line in (Get-Content -LiteralPath $Path)) {
        if ($line -match '^\s*(\w+)=(\S+)') {
            $entries += @{ Name = $Matches[1]; Value = $Matches[2] }
        }
    }
    return $entries
}

# Also parse string entries with extra fields: NAME=VALUE Namespace ValueType
function Parse-StringEntries {
    param([string]$Path)
    $entries = @()
    foreach ($line in (Get-Content -LiteralPath $Path)) {
        if ($line -match '^\s*(\w+)=(\S+)\s+(\S+)(?:\s+(\S+))?') {
            $entries += @{ Name = $Matches[1]; Value = $Matches[2]; Namespace = $Matches[3]; ValueType = $Matches[4] }
        }
    }
    return $entries
}

$nsEntries   = Parse-Entries -Path $n
$strEntries  = Parse-StringEntries -Path $x
$tableSize   = 1 + $nsEntries.Count + $strEntries.Count

# --- Generate the C# file ---
$sb = [System.Text.StringBuilder]::new()

[void]$sb.AppendLine('//-------------------------------------------------------------------------------')
[void]$sb.AppendLine('// <copyright from=''1999'' to=''2005'' company=''Microsoft Corporation''>')
[void]$sb.AppendLine('//    Copyright (c) Microsoft Corporation. All Rights Reserved.')
[void]$sb.AppendLine('//    Information Contained Herein is Proprietary and Confidential.')
[void]$sb.AppendLine('// </copyright>')
[void]$sb.AppendLine('//')
[void]$sb.AppendLine("// This file is generated from $n and $x by GenXmlStringTable.ps1")
[void]$sb.AppendLine('//           - do not modify this file directly')
[void]$sb.AppendLine('//-------------------------------------------------------------------------------')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('using System;')
[void]$sb.AppendLine('using System.Collections;')
[void]$sb.AppendLine('using System.Diagnostics;')
[void]$sb.AppendLine('using System.Xml;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("using $enumnamespace;")
[void]$sb.AppendLine('')
[void]$sb.AppendLine("namespace $enumnamespace")
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("    internal enum $enumsrclass : int")
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        NotDefined = 0,')

foreach ($e2 in $nsEntries) {
    [void]$sb.AppendLine("        $($e2.Name),")
}
foreach ($e2 in $strEntries) {
    [void]$sb.AppendLine("        $($e2.Name),")
}

[void]$sb.AppendLine('    }   // end of enum')
[void]$sb.AppendLine('}   // end of namespace')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("namespace $tablenamespace")
[void]$sb.AppendLine('{')
[void]$sb.AppendLine("    internal static class $tablesrclass")
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine("        static $tablesrclass()")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            Object str;')
[void]$sb.AppendLine('')

foreach ($e2 in $nsEntries) {
    [void]$sb.AppendLine("             str = _nameTable.Add(`"$($e2.Value)`");")
    [void]$sb.AppendLine("             _xmlstringtable[(int) $enumsrclass.$($e2.Name)] = new XmlStringTableStruct(str, $enumsrclass.NotDefined, null);")
}
[void]$sb.AppendLine('')

foreach ($e2 in $strEntries) {
    [void]$sb.AppendLine("             str = _nameTable.Add(`"$($e2.Value)`");")
    $vt = if ($e2.ValueType) { "`"$($e2.ValueType)`"" } else { 'null' }
    [void]$sb.AppendLine("             _xmlstringtable[(int) $enumsrclass.$($e2.Name)] = new XmlStringTableStruct(str, $enumsrclass.$($e2.Namespace), $vt);")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        internal static $enumsrclass GetEnumOf(Object xmlString)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            Debug.Assert(xmlString is String);')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('            for (int i = 1; i < _xmlstringtable.GetLength(0) ; ++i)')
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                if (Object.ReferenceEquals(_xmlstringtable[i].Name, xmlString))')
[void]$sb.AppendLine('                {')
[void]$sb.AppendLine("                    return (PackageXmlEnum) i;")
[void]$sb.AppendLine('                }')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("            return $enumsrclass.NotDefined;")
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        internal static string GetXmlString($enumsrclass id)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            CheckIdRange(id);')
[void]$sb.AppendLine('            return (string) _xmlstringtable[(int) id].Name;')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        internal static Object GetXmlStringAsObject($enumsrclass id)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            CheckIdRange(id);')
[void]$sb.AppendLine('            return _xmlstringtable[(int) id].Name;')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        internal static $enumsrclass GetXmlNamespace($enumsrclass id)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            CheckIdRange(id);')
[void]$sb.AppendLine('            return _xmlstringtable[(int) id].Namespace;')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        internal static string GetValueType($enumsrclass id)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            CheckIdRange(id);')
[void]$sb.AppendLine('            return _xmlstringtable[(int) id].ValueType;')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('        internal static NameTable NameTable')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            get { return _nameTable; }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        private static void CheckIdRange($enumsrclass id)")
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine("            if ((int) id <= 0 || (int) id >= $tableSize)")
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                throw new ArgumentOutOfRangeException("id");')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('        internal static NameTable CloneNameTable()')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            NameTable nameTable = new NameTable();')
[void]$sb.AppendLine("            for (int i=1; i<$tableSize; ++i)")
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                nameTable.Add((string)_xmlstringtable[i].Name);')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('            return nameTable;')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('        private struct XmlStringTableStruct')
[void]$sb.AppendLine('        {')
[void]$sb.AppendLine('            private Object _nameString;')
[void]$sb.AppendLine("            private $enumsrclass _namespace;")
[void]$sb.AppendLine('            private string _valueType;')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("            internal XmlStringTableStruct(Object nameString, $enumsrclass ns, string valueType)")
[void]$sb.AppendLine('            {')
[void]$sb.AppendLine('                _nameString = nameString;')
[void]$sb.AppendLine('                _namespace = ns;')
[void]$sb.AppendLine('                _valueType = valueType;')
[void]$sb.AppendLine('            }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('            internal Object Name { get { return (String) _nameString; } }')
[void]$sb.AppendLine("            internal $enumsrclass Namespace { get { return _namespace; } }")
[void]$sb.AppendLine('            internal string ValueType { get { return _valueType; } }')
[void]$sb.AppendLine('        }')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("        private static XmlStringTableStruct[] _xmlstringtable = new XmlStringTableStruct[$tableSize];")
[void]$sb.AppendLine('        private static NameTable _nameTable = new NameTable();')
[void]$sb.AppendLine("    }    //endof class $tablesrclass")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('}   // end of namespace')

# Write output
$outputDir = Split-Path -Parent $o
if (-not (Test-Path -LiteralPath $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
[System.IO.File]::WriteAllText($o, $sb.ToString().Replace("`r`n", "`n"))

Write-Host "Generated $o ($tableSize entries)"
