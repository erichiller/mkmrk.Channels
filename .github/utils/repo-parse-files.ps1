#!/bin/pwsh

# $repoMetaPath = "../../../../repo-meta/";
$repoMetaPath = Join-Path -Path "./repo-meta/" -ChildPath "mkmrk-channels";
$coveragePath = Join-Path -Path $repoMetaPath -ChildPath "coverage";
$buildLogPath = Join-Path -Path $repoMetaPath -ChildPath "build-logs";

# *************** Do not modify beyond this point ***************

Remove-Item -Path (Join-Path -Path $coveragePath -ChildPath "SummaryGithub.md" ) -ErrorAction Continue;
# should save with a prefixed datetime?
New-Item -ItemType Directory -Path $buildLogPath -Force ;
$repoStatsPath = Join-Path -Path $repoMetaPath -ChildPath "repo-stats.md";
Copy-Item -Path "./mkmrk-channels/src/repo-stats.md" -Destination $repoStatsPath ;
$jsonBuildLogStatsHistoryPath = Join-Path -Path $buildLogPath -ChildPath "history.json";
[PSCustomObject]$buildLogStatsHistory = [PSCustomObject]::new();
if ( Test-Path -Path $jsonBuildLogStatsHistoryPath ) {
    $buildLogStatsHistory = Get-Content -Path $jsonBuildLogStatsHistoryPath | ConvertFrom-Json;
}
# Summary Output
$buildLogCombinedPath = Join-Path -Path $buildLogPath -ChildPath "combined.log";
$buildLogStatsPath = Join-Path -Path $buildLogPath -ChildPath "stats.md";
Remove-Item -Path $buildLogCombinedPath -ErrorAction Continue;
Remove-Item -Path $buildLogStatsPath -ErrorAction Continue;
Add-Content -Path $buildLogStatsPath -Value ( "{0:-40} | {1}" -f ( "Build Log", "Warning Count" ) );
Add-Content -Path $buildLogStatsPath -Value ( "{0}-|--------------" -f ( "".PadLeft(40, "-")))
[System.Collections.Generic.Dictionary[string, int]] $buildLogStats = [System.Collections.Generic.Dictionary[string, int]]::new();
Get-ChildItem -Path ./mkmrk-channels/src/*/*build.log | ForEach-Object {
    $projectBuildLog = "./repo-meta/mkmrk-channels/build-logs/$( $_.Name )";
    #
    Remove-Item -Path $projectBuildLog -ErrorAction Continue;
    #(Get-Content -Path $_.FullName | Select-String -Pattern "mkmrk-channels/(src.*)").Matches.Groups[1].Value | Sort-Object | Add-Content -Path $projectBuildLog;
    Get-Content -Path $_.FullName | ForEach-Object {
        ( $_ | Select-String -Pattern "mkmrk-channels/(src.*)").Matches.Groups[1].Value #| Add-Content -Path $projectBuildLog;
    } | Sort-Object | Add-Content -Path $projectBuildLog;
    $builtProjectName = $_.Name -replace '-build.log' ;
    Get-Content -Path $projectBuildLog | Add-Content -Path "./_tmp.log"
    [int] $count = (Get-Content -Path $_.FullName | Measure-Object).Count;
    $buildLogStats.Add($builtProjectName, $count)
    Add-Content -Path $buildLogStatsPath -Value ("{0,-40} | {1:N0}" -f ( $builtProjectName, $count ) );
}
Get-Content -Path "./_tmp.log" | Sort-Object | Set-Content -Path $buildLogCombinedPath;
[int] $combinedCount = (Get-Content -Path $buildLogCombinedPath | Measure-Object).Count;
$buildLogStats.Add("Combined", $combinedCount);

Add-Member -InputObject $buildLogStatsHistory -NotePropertyName ( Get-Date -UFormat "%Y-%m-%dT%H:%M:%S%Z" ) -NotePropertyValue $buildLogStats -Force
$buildLogStatsHistory | ConvertTo-Json | Set-Content -Path $jsonBuildLogStatsHistoryPath ;
Add-Content -Path $buildLogStatsPath -Value ("{0,-40} | **{1:N0}**" -f ( "**Combined**", $combinedCount));
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "";
Get-Content -Path $buildLogStatsPath | ForEach-Object {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $_;
}
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "";
Get-Content -Path $repoStatsPath | ForEach-Object {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $_;
}
# TODO: CREATE CHART HERE

# Cleanup excess changes

$coverageSummaryPath = Join-Path -Path $coveragePath -ChildPath "Summary.md" ;
( Get-Content -Path $coverageSummaryPath `
| Select-String -NotMatch -Pattern "^\| Tag: \| [0-9a-f_]+ \|$" `
| Select-String -NotMatch -Pattern "Feature is only available for sponsors" `
| Select-String -NotMatch -Pattern "^\| Generated on: \|" `
| Select-String -NotMatch -Pattern "^\| Coverage date: \|" `
) | Set-Content -Path $coverageSummaryPath;

$coverageSummaryJson = Join-Path -Path $coveragePath -ChildPath "Summary.json" ;
( Get-Content -Path $coverageSummaryJson `
| Select-String -NotMatch -Pattern "^    `"generatedon`": " `
) | Set-Content -Path $coverageSummaryJson;

$coverageDeltaSummaryPath = Join-Path -Path $coveragePath -ChildPath "DeltaSummary.md" ;
( Get-Content -Path $coverageDeltaSummaryPath `
| Select-String -NotMatch -Pattern "^\| Tag: \| [0-9a-f_]+ \|" `
| Select-String -NotMatch -Pattern "Feature is only available for sponsors" `
| Select-String -NotMatch -Pattern "^\| Generated on: \|" `
| Select-String -NotMatch -Pattern "^\| Coverage date: \|"
) | Set-Content -Path $coverageDeltaSummaryPath;


