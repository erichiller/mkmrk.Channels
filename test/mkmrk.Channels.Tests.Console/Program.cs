using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace mkmrk.Channels.Tests.Cli;

public static partial class Program {
    [Conditional("LOG")]
    private static void log(string? msg) => System.Console.WriteLine(msg);

    static async Task<int> Main(string[] args) {

        /* ************************************************************************ */

        System.Console.WriteLine("DEBUG_MUX");

        System.Console.WriteLine($"args len={args.Length} ; {String.Join(", ", args)}");

        if (args.Length >= 1) {
            switch (args[0]) {
                case nameof(CheckForOffsetCompletionErrors):
                    await CheckForOffsetCompletionErrors();
                    break;
                case nameof(StressTest):
                    await StressTest();
                    break;
                case nameof(SimpleTest):
                {
                    switch (args.Length) {
                        case 3 when Int32.TryParse(args[1], out int c) && Int32.TryParse(args[2], out int messageCount):
                            System.Console.WriteLine($"Running {nameof(SimpleTest)} with count={c}");
                            await SimpleTest(c, messageCount);
                            break;
                        case 2 when Int32.TryParse(args[1], out int c):
                            System.Console.WriteLine($"Running {nameof(SimpleTest)} with count={c}");
                            await SimpleTest(c);
                            break;
                        case 1:
                            await SimpleTest();
                            break;
                        default:
                            throw new Exception(String.Join( ',', args ));
                    }
                    break;
                }
                case nameof(ExceptionShouldRemoveFromBroadcastChannel):
                    await ExceptionShouldRemoveFromBroadcastChannel();
                    break;
                case nameof(TypeInheritanceTestingOneSubOfOther):
                    await TypeInheritanceTestingOneSubOfOther();
                    break;
                case nameof(TypeInheritanceTestingBothSubOfSame):
                    await TypeInheritanceTestingBothSubOfSame();
                    break;
                case nameof(ChannelMuxLatencyTest):
                    await ChannelMuxLatencyTest();
                    break;
                case nameof(AsyncWaitLoopOnly_2Producer):
                    await AsyncWaitLoopOnly_2Producer();
                    break;
                case nameof(LoopTryRead2_2Producer):
                {
                    int? count = null;
                    if (args.Length >= 1 && Int32.TryParse(args[1], out int c)) {
                        count = c;
                    }
                    await LoopTryRead2_2Producer(count);
                    break;
                }
                case nameof(LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes):
                    await LoopTryRead2_4Producer_1Task_1ValueType_3ReferenceTypes();
                    break;
                case nameof(ChannelMux_LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes):
                    await ChannelMux_LoopTryRead2_4Producer_4Tasks_1ValueType_3ReferenceTypes();
                    break;
                case nameof(ChannelMux_LoopTryRead2_4Producer_4Tasks_4ReferenceTypes):
                    await ChannelMux_LoopTryRead2_4Producer_4Tasks_4ReferenceTypes();
                    break;
                case nameof(ChannelMux_LoopTryRead2_8Producer_8Tasks):
                    await ChannelMux_LoopTryRead2_8Producer_8Tasks();
                    break;
                case nameof(LatencyTest):
                    await LatencyTest();
                    break;
                case nameof(ChannelComplete_WithException_ShouldThrow_UponAwait):
                    await ChannelComplete_WithException_ShouldThrow_UponAwait();
                    break;
                default:
                    log($"not known: {args[0]}");
                    return 1;
            }
            return 0;
        }
        return 1;
    }
}