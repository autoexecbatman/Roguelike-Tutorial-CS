# Builds every part from scratch and reports any warnings.
#
# An incremental build skips the analyzers, so `dotnet build` on an up-to-date tree prints
# "0 Warning(s)" whether there are warnings or not. That is not a measurement, and it went
# unnoticed here for ten parts: a real xUnit analyzer warning sat in a test file while every
# check said the build was clean.
#
# Cleaning first is what makes the number mean something. It is slow - a minute or so for ten
# parts - which is why it is a separate tool rather than part of the fast checks.
#
# Usage - from the repository root:
#
#     powershell -File tools/check-clean-build.ps1
#
# Prints one line per part and a total. A non-zero total means a warning nobody has seen.

$total = 0

foreach ($part in Get-ChildItem -Path "parts" -Directory | Sort-Object Name) {
    Push-Location $part.FullName

    # Clean first, or the analyzers do not run and the count is meaningless.
    dotnet clean -v q --nologo *> $null

    $output = dotnet build --nologo 2>&1 | Out-String

    # The per-file warning lines, not the summary, so the message itself is reportable.
    $warnings = $output -split "`n" | Where-Object { $_ -match ": warning " } | Sort-Object -Unique

    $total += $warnings.Count

    Write-Output ("{0,-40} {1} warning(s)" -f $part.Name, $warnings.Count)

    foreach ($warning in $warnings) {
        Write-Output ("    " + $warning.Trim())
    }

    Pop-Location
}

Write-Output ""
Write-Output "total warnings: $total"
