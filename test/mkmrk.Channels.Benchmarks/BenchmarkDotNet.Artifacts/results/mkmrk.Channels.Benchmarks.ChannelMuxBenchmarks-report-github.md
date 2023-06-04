``` ini

BenchmarkDotNet=v0.13.5, OS=ubuntu 22.04
Intel Core i5-8600K CPU 3.60GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK=7.0.302
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


```
|                                                             Method | MessageCount | withCancellationToken | Mean [ms] | Error [ms] | StdDev [ms] |      Gen0 |      Gen1 |      Gen2 | Allocated [B] |
|------------------------------------------------------------------- |------------- |---------------------- |----------:|-----------:|------------:|----------:|----------:|----------:|--------------:|
|                                               BroadcastChannelOnly |       100000 |                     ? |  12.32 ms |   0.244 ms |    0.291 ms |  687.5000 |  296.8750 |  140.6250 |     3922639 B |
|                                             LoopTryRead2_2Producer |       100000 |                 False |  15.88 ms |   0.295 ms |    0.316 ms |  812.5000 |  218.7500 |   62.5000 |     4133982 B |
|                                             LoopTryRead2_2Producer |       100000 |                  True |  17.32 ms |   0.345 ms |    0.785 ms |  968.7500 |  281.2500 |   62.5000 |     4945506 B |
|                                              LoopTryRead_2Producer |       100000 |                 False |  18.05 ms |   0.231 ms |    0.204 ms |  968.7500 |  968.7500 |  968.7500 |     7414194 B |
|                                              LoopTryRead_2Producer |       100000 |                  True |  18.82 ms |   0.371 ms |    0.412 ms | 1093.7500 |  906.2500 |  906.2500 |     7814457 B |
|                                        AsyncWaitLoopOnly_2Producer |       100000 |                 False |  27.99 ms |   0.559 ms |    1.204 ms | 1500.0000 |  468.7500 |  250.0000 |     7470840 B |
|                                        AsyncWaitLoopOnly_2Producer |       100000 |                  True |  32.42 ms |   0.645 ms |    1.676 ms | 2200.0000 |  400.0000 |  133.3333 |    11453091 B |
|                                             LoopTryRead2_3Producer |       100000 |                     ? |  38.83 ms |   0.763 ms |    0.937 ms | 1769.2308 |  615.3846 |   76.9231 |     9441872 B |
|                      LoopTryRead2_4Producer_4Tasks_4ReferenceTypes |       100000 |                     ? |  48.98 ms |   0.963 ms |    1.761 ms | 2700.0000 | 1900.0000 |  700.0000 |    15763193 B |
|           LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes |       100000 |                     ? |  50.17 ms |   0.999 ms |    1.110 ms | 2454.5455 | 1727.2727 |  636.3636 |    15050027 B |
|            LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes |       100000 |                     ? | 100.43 ms |   0.445 ms |    0.416 ms | 2000.0000 |         - |         - |     9612509 B |
|                                      LoopTryRead2_8Producer_8Tasks |       100000 |                 False | 105.07 ms |   2.072 ms |    1.938 ms | 5200.0000 | 3400.0000 | 1200.0000 |    33028643 B |
|                                      LoopTryRead2_8Producer_8Tasks |       100000 |                  True | 107.09 ms |   2.119 ms |    3.422 ms | 7200.0000 | 3800.0000 | 1200.0000 |    41432386 B |
| LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes_WriteAsync |       100000 |                     ? | 111.58 ms |   0.343 ms |    0.321 ms | 5200.0000 |         - |         - |    24812597 B |
