param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string]$Output = '.\bin\preview-rendering-results\mock-matrix',
    [ValidateRange(1, 600)][int]$Seconds = 15,
    [int[]]$Counts = @(1, 4, 12, 24)
)

$ErrorActionPreference = 'Stop'
$previewSmokeExe = (Resolve-Path -LiteralPath $Executable).Path
$previewMatrixOutput = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $previewMatrixOutput -Force | Out-Null
foreach ($previewCount in $Counts) {
    foreach ($previewRenderer in @('legacy', 'native', 'avalonia')) {
        $previewEffects = if ($previewRenderer -eq 'legacy') { @('none') } else { @('none', 'all') }
        foreach ($previewEffect in $previewEffects) {
            $previewRunDirectory = Join-Path $previewMatrixOutput "$previewRenderer-$previewEffect-$previewCount"
            New-Item -ItemType Directory -Path $previewRunDirectory -Force | Out-Null
            & $previewSmokeExe --renderer $previewRenderer --effects $previewEffect --count $previewCount --seconds $Seconds --output $previewRunDirectory *> (Join-Path $previewRunDirectory 'run.log')
            if ($LASTEXITCODE -ne 0) {
                Get-Content -LiteralPath (Join-Path $previewRunDirectory 'run.log')
                throw "Renderer validation failed: $previewRenderer, $previewEffect, $previewCount"
            }
            $previewResult = Get-Content -Raw -LiteralPath (Join-Path $previewRunDirectory 'result.json') | ConvertFrom-Json
            Write-Output ('PASS {0,-8} {1,-4} previews={2,2} CPU(core)={3:N2}% p95={4:N2}ms private={5:N1}MiB' -f $previewRenderer, $previewEffect, $previewCount,
                $previewResult.CpuCorePercent, $previewResult.MaintenanceMilliseconds.P95, ($previewResult.After.PrivateBytes / 1MB))
        }
    }
}
