param(
    [Parameter(Mandatory = $true)][string]$IfxRoot,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$expectedCommit = 'cc06528ab17052baafa496df2a89f6cde9207384'
$actualCommit = (& git -C $IfxRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $expectedCommit) {
    throw "IFX source commit does not match the reviewed LT00 baseline."
}
$sourceChanges = @(& git -C $IfxRoot status --porcelain 2>$null)
if ($LASTEXITCODE -ne 0 -or $sourceChanges.Count -ne 0) {
    throw "IFX source checkout is not clean or cannot be inspected."
}
$projectRelative = 'tests/Architecture/IFX.Architecture.Tests'
$projectRoot = Join-Path $IfxRoot $projectRelative
$projectPath = Join-Path $projectRoot 'IFX.Architecture.Tests.csproj'
$testFiles = @('ArchitectureTests.cs', 'FixtureTests.cs', 'ApplicationPortAdapterTests.cs')

function Get-Sha256([string]$Path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$classes = @(
    foreach ($name in $testFiles) {
        $path = Join-Path $projectRoot $name
        $lines = Get-Content -LiteralPath $path
        $methods = @(
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -notmatch '^\s*\[(Fact|Theory)\]') { continue }
                $kind = $Matches[1]
                $inlineData = @()
                $next = $i + 1
                while ($next -lt $lines.Count -and $lines[$next] -match '^\s*\[InlineData\((.*)\)\]') {
                    $inlineData += $Matches[1].Trim()
                    $next++
                }
                if ($next -ge $lines.Count -or $lines[$next] -notmatch '^\s*public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void)\s+(\w+)\s*\(') {
                    throw "Cannot identify test method after $name line $($i + 1)."
                }
                $method = $Matches[1]
                if ($kind -eq 'Theory' -and $inlineData.Count -eq 0) {
                    throw "Theory data requires explicit inventory: $name::$method"
                }
                [pscustomobject]@{
                    method = $method
                    kind = $kind
                    sourceLine = $i + 1
                    inlineData = $inlineData
                }
                $i = $next
            }
        )
        [pscustomobject]@{
            name = [IO.Path]::GetFileNameWithoutExtension($name)
            source = $name
            sha256 = Get-Sha256 $path
            methodCount = $methods.Count
            staticCaseCount = [int](@($methods | ForEach-Object { if ($_.kind -eq 'Fact') { 1 } else { $_.inlineData.Count } } | Measure-Object -Sum)[0].Sum)
            methods = $methods
        }
    }
)

$sourcePaths = @($testFiles | ForEach-Object { Join-Path $projectRoot $_ })
$sourcePaths += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Rules'), (Join-Path $projectRoot 'Infrastructure'), (Join-Path $projectRoot 'Fixtures') -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
$sourceFiles = @(
    $sourcePaths | Sort-Object -Unique | ForEach-Object {
        [pscustomobject]@{
            path = ([IO.Path]::GetRelativePath($projectRoot, $_)).Replace('\', '/')
            sha256 = Get-Sha256 $_
        }
    }
)
$projectText = Get-Content -LiteralPath $projectPath -Raw
$references = @([regex]::Matches($projectText, '<ProjectReference Include="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
$methodCount = [int](@($classes | Measure-Object methodCount -Sum)[0].Sum)
$caseCount = [int](@($classes | Measure-Object staticCaseCount -Sum)[0].Sum)
if ($methodCount -ne 65 -or $caseCount -ne 80) {
    throw "Unexpected IFX source inventory: $methodCount methods, $caseCount static cases. Review source before changing baseline."
}

$baseline = [ordered]@{
    schemaVersion = 1
    kind = 'lt00-ifx-source-baseline'
    ifxCommit = $actualCommit
    testProject = "$projectRelative/IFX.Architecture.Tests.csproj"
    projectSha256 = Get-Sha256 $projectPath
    projectReferences = $references
    sourceFiles = $sourceFiles
    classes = $classes
    staticMethodCount = $methodCount
    staticCaseCount = $caseCount
    runnerDiscoveryVerified = $false
}
$baseline | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
