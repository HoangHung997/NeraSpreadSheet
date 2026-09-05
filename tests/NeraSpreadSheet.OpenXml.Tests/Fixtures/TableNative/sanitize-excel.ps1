param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$destinationPath = [IO.Path]::GetFullPath($Destination)
if ($sourcePath -eq $destinationPath -or (Test-Path -LiteralPath $destinationPath)) {
    throw 'Use a new destination; the native original must remain unchanged.'
}
$sourcePackage = [IO.Compression.ZipFile]::OpenRead($sourcePath)
$outputPackage = [IO.Compression.ZipFile]::Open($destinationPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $sourcePackage.Entries) {
        $outputEntry = $outputPackage.CreateEntry($entry.FullName, [IO.Compression.CompressionLevel]::Optimal)
        $outputEntry.LastWriteTime = [DateTimeOffset]::new(2026, 9, 5, 0, 0, 0, [TimeSpan]::Zero)
        $inputStream = $entry.Open()
        $outputStream = $outputEntry.Open()
        try {
            if ($entry.FullName -in @('docProps/core.xml', 'xl/workbook.xml')) {
                $xml = [xml]::new()
                $xml.PreserveWhitespace = $true
                $xml.Load($inputStream)
                if ($entry.FullName -eq 'docProps/core.xml') {
                    foreach ($node in @($xml.DocumentElement.ChildNodes)) {
                        [void]$xml.DocumentElement.RemoveChild($node)
                    }
                } else {
                    foreach ($node in @($xml.SelectNodes('//*[local-name()="AlternateContent"][.//*[local-name()="absPath"]] | //*[local-name()="revisionPtr"]'))) {
                        [void]$node.ParentNode.RemoveChild($node)
                    }
                }
                $xml.Save($outputStream)
            } else {
                $inputStream.CopyTo($outputStream)
            }
        } finally {
            $inputStream.Dispose()
            $outputStream.Dispose()
        }
    }
} finally {
    $sourcePackage.Dispose()
    $outputPackage.Dispose()
}

# All other part payloads, especially Table IDs/relationships/formulas/caches,
# are byte-for-byte native. The test suite independently verifies privacy and schema.
Get-FileHash -LiteralPath $sourcePath, $destinationPath -Algorithm SHA256 |
    Select-Object @{Name='File';Expression={[IO.Path]::GetFileName($_.Path)}}, Hash
