#!/bin/pwsh

Param(
    [switch]$Build,
    [switch]$Debug,
    [switch]$Run,
    [string]$DockerHost=$env:DOCKER_HOST
#    [Parameter(ParameterSetName="Production")]
#    [switch]$Publish,
#    [Parameter(ParameterSetName="Production")]
#    [switch]$BuildImage,
#    [Parameter(ParameterSetName="Production")]
#    [switch]$Clean,
#    [Parameter(ParameterSetName="Production")]
#    [switch]$RemoveBuildOutput,
#    [Parameter(ParameterSetName="Debug")]
#    [switch]$DebugImage
)

$InformationPreference='Continue';
$DebugPreference='Continue';


$Name="broadcast-queue-tests";
$ImageName="${Name}:latest";
$ContainerName=$Name;
#$DockerHost=$env:DOCKER_HOST;

Write-Information "Using DockerHost=$DockerHost";
if ( $Build ) {
    try {
        Push-Location (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
        Write-Debug "Building image $ImageName in path $( Get-Location )"
        #        docker build --tag $ImageName --file InterThread/BroadcastQueueTests/Dockerfile .
        docker --host $DockerHost build --tag $ImageName --file InterThread/BroadcastQueueTests/Dockerfile .
    } finally {
        Pop-Location
    }
}

#if ( Get-Location -ne $PSScriptRoot ){
#    Pu
#}


if ( $Debug ) {
    docker --host $DockerHost run --rm -it --cpuset-cpus="0" --name broadcast-queue-test --entrypoint /bin/bash broadcast-queue-tests:latest
} elseif ( $Run ) {

    #    Push-Location $PSScriptRoot
    #    Write-Output "Running: 'docker run --rm -it --cpuset-cpus=`"0`" --name $ContainerName $ImageName --filter Benchmarks.InterThread.ChannelMuxTests.ChannelMuxTests'";
    #    URGENT: --cpuset-cpus=0 does not seem to have an effect
    #    docker run --rm -it --cpuset-cpus="0" --name $ContainerName $ImageName --filter Benchmarks.InterThread.ChannelMuxTests.ChannelMuxTests

    Write-Output "Running: 'docker run --host $DockerHost --rm -it --name $ContainerName $ImageName --filter Benchmarks.InterThread.ChannelMuxTests.ChannelMuxTests'";
    docker --host $DockerHost run --rm -it --name $ContainerName $ImageName --filter Benchmarks.InterThread.ChannelMuxTests.ChannelMuxTests

    #    Pop-Location
}