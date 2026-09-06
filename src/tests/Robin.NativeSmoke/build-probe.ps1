# Run in an x64 Visual Studio Developer PowerShell. Outputs stay under this test's ignored bin directory.
$ErrorActionPreference = 'Stop'
$probeOutput = Join-Path $PSScriptRoot 'bin\native'
New-Item -ItemType Directory -Path $probeOutput -Force | Out-Null
Push-Location $probeOutput
try {
    & cl.exe /nologo /O2 /EHsc /std:c++17 /LD (Join-Path $PSScriptRoot 'audio.cpp') /Fe:_audio2.dll /link /NOENTRY "/DEF:$(Join-Path $PSScriptRoot 'audio.def')"
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic audio build failed' }
    & cl.exe /nologo /O2 /EHsc /std:c++17 (Join-Path $PSScriptRoot 'probe.cpp') /Fe:probe.exe /link d3d11.lib dxgi.lib user32.lib
    if ($LASTEXITCODE -ne 0) { throw 'Native probe build failed' }
} finally { Pop-Location }
