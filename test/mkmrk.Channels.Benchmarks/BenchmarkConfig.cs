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
        Options      = ConfigOptions.Default;
        UnionRule    = ConfigUnionRule.AlwaysUseLocal;
        BuildTimeout = TimeSpan.FromSeconds( 30.0 );
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
        AddExporter( MarkdownExporter.GitHub );

        AddLogger( BenchmarkDotNet.Loggers.ConsoleLogger.Default );
        AddDiagnoser( MemoryDiagnoser.Default );
        AddAnalyser( DefaultConfig.Instance.GetAnalysers().ToArray() );
        this.WithOptions( ConfigOptions.StopOnFirstError )
            .WithOptions( ConfigOptions.JoinSummary );
        Orderer = new DefaultOrderer( SummaryOrderPolicy.FastestToSlowest );
        WithOptions( ConfigOptions.DisableOptimizationsValidator );
        AddValidator( DefaultConfig.Instance.GetValidators().ToArray() );
    }
}