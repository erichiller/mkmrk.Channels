using System;
using System.Globalization;
using System.Linq;

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;

namespace mkmrk.Channels.Benchmarks;

public class BenchmarkConfig : ManualConfig {
    public BenchmarkConfig( ) {
        Options       = ConfigOptions.Default;
        UnionRule     = ConfigUnionRule.AlwaysUseLocal;
        BuildTimeout  = TimeSpan.FromSeconds( 30.0 );
        // ArtifactsPath => System.IO.Path.Combine(RuntimeInformation.IsAndroid() ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
        ArtifactsPath = DefaultConfig.Instance.ArtifactsPath;
        WithSummaryStyle( new SummaryStyle(
                              // cultureInfo: CultureInfo.CurrentCulture,
                              cultureInfo: CultureInfo.CreateSpecificCulture( "en-US" ),
                              sizeUnit: SizeUnit.B,
                              printUnitsInHeader: true,
                              timeUnit: null!,
                              printUnitsInContent: true,
                              maxParameterColumnWidth: 20,
                              printZeroValuesInContent: false,
                              ratioStyle: RatioStyle.Value
                          ) );
        AddColumnProvider( DefaultColumnProviders.Instance );
        AddExporter( MarkdownExporter.GitHub ); // It says "Already Present" ; TODO: re-enable
        // HtmlExporter.Default, // TODO: re-enable
        // CsvExporter.Default, // It says "Already Present" ; TODO: re-enable
        // JsonExporter.Default //, // TODO: re-enable
        // AddExporter( RPlotExporter.Default ); // TODO: re-enable

        AddLogger( BenchmarkDotNet.Loggers.ConsoleLogger.Default );
        AddDiagnoser( MemoryDiagnoser.Default );
        AddAnalyser( DefaultConfig.Instance.GetAnalysers().ToArray() );
        // AddDiagnoser( EventPipeProfiler.Default ); // generates output for profiling. TODO: re-enable
        this.WithOptions( ConfigOptions.StopOnFirstError ) // TODO: re-enable
            .WithOptions( ConfigOptions.JoinSummary )      // TODO: re-enable
            ;
        Orderer = new DefaultOrderer( SummaryOrderPolicy.FastestToSlowest ); // TODO: re-enable // URGENT!
        
        WithOptions( ConfigOptions.DisableOptimizationsValidator );

        // AddJob( new Job1(), new Job2() );
        // AddJob( new Job( "Select" ) { Environment = { EnvironmentVariables = new[] { new EnvironmentVariable( "Autosummarize", "on" ) } } } );
        // AddColumn( new Column1(), new Column2() );
        // AddColumn( RankColumn.Arabic );
        // AddColumnProvider( new ColumnProvider1(), new ColumnProvider2() );
        AddValidator( DefaultConfig.Instance.GetValidators().ToArray() );
        // AddValidator( ExecutionValidator.FailOnError ); // TODO: re-enable
        // AddValidator( ReturnValueValidator.FailOnError ); // TODO: re-enable
        // AddHardwareCounters( HardwareCounter enum1, HardwareCounter enum2 );
        // AddFilter( new Filter1(), new Filter2() );
        // AddLogicalGroupRules( BenchmarkLogicalGroupRule enum1, BenchmarkLogicalGroupRule enum2 );
    }
}

// public class GenericLogger : BenchmarkDotNet.Loggers.LogCapture

// /*
//  * Sample: IntroConfigSource
//  * You can define own config attribute.
//  * https://benchmarkdotnet.org/articles/configs/configs.html#sample-introconfigsource
//  */
// /// <summary>
// /// Dry-x64 jobs for specific jits
// /// </summary>
// public class MyConfigSourceAttribute : Attribute, IConfigSource {
//     public IConfig Config { get; }
//
//     // public MyConfigSourceAttribute(params Jit[] jits)
//     // {
//     //     var jobs = jits
//     //                .Select(jit => new Job(Job.Dry) { Environment = { Jit = jit, Platform = Platform.X64 } })
//     //                .ToArray();
//     //     Config = ManualConfig.CreateEmpty().AddJob(jobs);
//     // }
// }