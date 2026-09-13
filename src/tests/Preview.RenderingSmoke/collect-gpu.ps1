param(
    [Parameter(Mandatory = $true)][int[]]$ProcessIds,
    [Parameter(Mandatory = $true)][string]$Output,
    [ValidateRange(2, 300)][int]$Samples = 15
)

$ErrorActionPreference = 'Stop'
$previewGpuOutput = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path -Parent $previewGpuOutput) -Force | Out-Null
$previewGpuPattern = '^pid_(' + (($ProcessIds | Select-Object -Unique) -join '|') + ')_'
$previewGpuRecords = [System.Collections.Generic.List[object]]::new()
$previewGpuPaths = @(
    '\GPU Engine(*)\Utilization Percentage',
    '\GPU Process Memory(*)\Dedicated Usage',
    '\GPU Process Memory(*)\Shared Usage',
    '\Process(dwm*)\ID Process',
    '\Process(dwm*)\% Processor Time'
)

# Sample in this helper process, independently of the preview UI thread. Keep
# individual adapters/engines; summing unrelated engines is not GPU utilization.
Get-Counter -Counter $previewGpuPaths -SampleInterval 1 -MaxSamples $Samples -ErrorAction SilentlyContinue -ErrorVariable previewGpuErrors | ForEach-Object {
    $previewGpuSample = $_
    $previewDwmInstances = @{}
    foreach ($previewCounter in $previewGpuSample.CounterSamples) {
        if ($previewCounter.Path.EndsWith('\id process') -and $ProcessIds -contains [int]$previewCounter.CookedValue) {
            $previewDwmInstances[$previewCounter.InstanceName] = [int]$previewCounter.CookedValue
        }
    }
    foreach ($previewCounter in $previewGpuSample.CounterSamples) {
        $previewCounterName = $previewCounter.Path.Substring($previewCounter.Path.LastIndexOf('\') + 1)
        if ($previewCounter.InstanceName -match $previewGpuPattern) {
            $previewGpuRecords.Add([pscustomobject]@{
                Timestamp = $previewGpuSample.Timestamp.ToUniversalTime().ToString('o')
                ProcessId = [int]$Matches[1]
                Instance = $previewCounter.InstanceName
                Counter = $previewCounterName
                Value = $previewCounter.CookedValue
                Status = $previewCounter.Status
            })
        }
        elseif ($previewCounterName -eq '% processor time' -and $previewDwmInstances.ContainsKey($previewCounter.InstanceName)) {
            $previewGpuRecords.Add([pscustomobject]@{
                Timestamp = $previewGpuSample.Timestamp.ToUniversalTime().ToString('o')
                ProcessId = $previewDwmInstances[$previewCounter.InstanceName]
                Instance = 'dwm'
                Counter = 'cpu percent of one core'
                Value = $previewCounter.CookedValue
                Status = $previewCounter.Status
            })
        }
    }
}

if ($previewGpuRecords.Count -eq 0) { throw 'No matching performance counters were collected.' }
$previewGpuSummary = @($previewGpuRecords | Where-Object { $_.Status -in @(0, 1) } | Group-Object ProcessId,Instance,Counter | ForEach-Object {
    $previewGpuFirst = $_.Group[0]
    $previewGpuStats = $_.Group | Measure-Object -Property Value -Average -Maximum -Minimum
    [pscustomobject]@{
        ProcessId = $previewGpuFirst.ProcessId
        Instance = $previewGpuFirst.Instance
        Counter = $previewGpuFirst.Counter
        Samples = $_.Count
        Mean = $previewGpuStats.Average
        Maximum = $previewGpuStats.Maximum
        Minimum = $previewGpuStats.Minimum
    }
})
[pscustomobject]@{
    ProcessIds = $ProcessIds
    RequestedSamples = $Samples
    CounterWarnings = @($previewGpuErrors | ForEach-Object { $_.Exception.Message } | Select-Object -Unique)
    Limitations = 'PDH process/engine utilization and allocation counters, not game frame times or rendering latency. Engines/adapters are separate; do not add different engine percentages. DWM includes every desktop application. Sampling is external to the preview process.'
    Summary = $previewGpuSummary
    Samples = @($previewGpuRecords)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $previewGpuOutput -Encoding UTF8
$previewGpuSummary | Where-Object { $_.Mean -gt 0.001 } | Format-Table ProcessId,Counter,Instance,Mean,Maximum -AutoSize
