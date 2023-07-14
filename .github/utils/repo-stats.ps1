#!/bin/pwsh

# ENABLE BELOW FOR TESTING
#$srcDir = "../../../src/" ;
$srcDir = "./src";
$includeFileTypes = @("*.xaml", "*.axaml", "*.ps1", "*.cs" );

$todoFilePath = Join-Path -Path $srcDir -ChildPath "TODO.md";
$rootDir = Split-Path -Resolve -Path $srcDir -Parent
$repoStats = Join-Path -Path $srcDir -ChildPath "repo-stats.md";

#################### TO-DO ####################

#################### repo-stats.md ####################

Add-Content -Path $repoStats -Value ( (
    "# mkmrk Repo Stats`n`n" +
    "## TO-DO`n`n" +
    "{0} | {1}`n" +
    "{2}-|-{3}" ) -f (
        "Project".PadRight(40),
        "Count".PadRight(5),
        "".PadLeft(40, '-'),
        "".PadLeft(5, '-')
    ) );

Add-Content -Path $todoFilePath -Value ( "{0,-60} | {1,-8} | {2}" -f (
        "File:LineNumber",
        "Level",
        "Todo"
    ) );
Add-Content -Path $todoFilePath -Value ( "{0} | {1} | {2}" -f (
        "".PadLeft(60),
        "".PadLeft(8),
        "".PadLeft(32)
    ) );
Get-ChildItem -Path $srcDir -Directory | Select-Object | ForEach-Object {
    $projectCount = 0;
    Get-ChildItem -Path "$($_.FullName)/*" -Recurse -Include $includeFileTypes 
    | ForEach-Object {
        $fullFileName = ($_.FullName | Select-String -Pattern "mkmrk-channels/(src/.*)").Matches.Groups[0].Groups[1].Value
        Select-String  -Path $_.FullName  -Pattern "(?:`/`*|`/`/)[\* ]*(?<Level>TODO|URGENT): (?<Content>[^`n]*)(\`\*|`n)" 
        | ForEach-Object { 
            $projectCount ++;
            return "{0,-60} | {1,-8} | {2}" -f (
                "``$($fullFileName):$($_.LineNumber)``",
                $_.Matches.Groups[0].Groups["Level"].Value,
                $_.Matches.Groups[0].Groups["Content"].Value 
            );
        }
    }
    Add-Content -Path $repoStats -Value ( "{0,-40} | {1:N0}" -f ( $_.Name, $projectCount ) );
} | Add-Content -Path $todoFilePath;
# TODO: where is the TODO.md file going??


Copy-Item -Recurse `
    -Path ( Join-Path -Path $rootDir -Resolve -ChildPath ".github/workflows/mkmrk-repo-utils/" ) `
    -Destination ( Join-Path -Path ( Split-Path -Path $srcDir -Resolve ) -ChildPath "mkmrk-repo-utils/" ) ;

Add-Content -Path $repoStats -Value ( (
    "`n`n" +
    "## Line Counts`n`n" +
    "{0} | {1}`n" +
    "{2}-|-{3}" ) -f (
        "Folder".PadRight(40),
        "Line Count".PadRight(5),
        "".PadLeft(40, '-'),
        "".PadLeft(5, '-')
    ) );

Get-ChildItem -Path $srcDir -Directory | ForEach-Object {
    "{0,-40} | {1:N0}" -f (
        "``$($_.Name)``", 
        ( Get-ChildItem -Path "$($_.FullName)/*" -Recurse -Include $includeFileTypes 
        | ForEach-Object { Get-Content -Path $_.FullName } 
        | Measure-Object).Count )
} | Add-Content -Path $repoStats ;
Add-Content -Path $repoStats -Value (
    ( "{0,-40} | {1:N0}`n" -f (
        "``/src`` Lines",
        ( Get-ChildItem -Path "$($srcDir)*" -Recurse -Include $includeFileTypes `
        | ForEach-Object { Get-Content -Path $_.FullName } | Measure-Object).Count 
    ) ) +
    ( "{0,-40} | {1:N0}`n" -f (
        "**Project Lines**",
        ( Get-ChildItem -Path "$($rootDir)*" -Recurse -Include $includeFileTypes `
        | ForEach-Object { 
            Get-Content -Path $_.FullName 
        } | Measure-Object).Count 
    ) ) +
    "" );
    

