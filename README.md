# `BroadcastChannel` and `ChannelMux`


## `BroadcastChannel`

Is a single input, multi output type where each output is guaranteed to receive all the data from the input. 
This is in contrast to `System.Threading.Channels.Channel` or a _Queue_ type where each input is only ever read by a 
single output.

## `ChannelMux`

`ChannelMux` is meant to aggregate multiple `BroadcastChannel` into a single, awaitable object. 
It is a generic type and each type parameter has a dedicated `TryRead(out T data)` method.
`ChannelMuxInput` acts presents as a writer to `BroadcastChannelWriter` and each has a
`SingleProducerSingleConsumerQueue`.

Note that each `ChannelMuxInput` is a single input, single output where _single_ means both a single instance writing
and a single instance reading, and thus can be optimized using `SingleProducerSingleConsumerQueue`.

```mermaid
flowchart LR

    Producing_Object1("Producing Object") --> BroadcastChannelWriter1
    Producing_Object2("Producing Object") --> BroadcastChannelWriter2

    subgraph BroadcastChannel1 [BroadcastChannel]
        BroadcastChannelWriter1["BroadcastChannelWriter"]
        BroadcastChannelReader1_1["BroadcastChannelReader"]
        BroadcastChannelReader1_2["BroadcastChannelReader"]
        BroadcastChannelReader1_3["BroadcastChannelReader"]
    end
    BroadcastChannelReader1_1 --> Consuming_Object_BR1_1("Consuming Object")
    BroadcastChannelReader1_2 --> Consuming_Object_BR1_2("Consuming Object")
    BroadcastChannelReader1_3 --> Consuming_Object_BR1_3("Consuming Object")

    subgraph BroadcastChannel2 [BroadcastChannel]
        BroadcastChannelWriter2["BroadcastChannelWriter"]
        BroadcastChannelWriter2 --> BroadcastChannelReader2_1["BroadcastChannelReader"]
        BroadcastChannelWriter2 --> BroadcastChannelReader2_2["BroadcastChannelReader"]
        BroadcastChannelWriter2 --> BroadcastChannelReader2_3["BroadcastChannelReader"]
    end
    BroadcastChannelReader2_1 --> Consuming_Object_BR2_1("Consuming Object")
    BroadcastChannelReader2_2 --> Consuming_Object_BR2_2("Consuming Object")
    BroadcastChannelReader2_3 --> Consuming_Object_BR2_3("Consuming Object")
    
    subgraph ChannelMux
        ChannelMuxT_1["ChannelMux<T1,T2,...>"]
        ChannelMuxInput1["ChannelMuxInput"] --> ChannelMuxT_1
        ChannelMuxInput2["ChannelMuxInput"] --> ChannelMuxT_1
    end
    BroadcastChannelWriter1 --> ChannelMuxInput1

    BroadcastChannelWriter1 --> BroadcastChannelReader1_1
    BroadcastChannelWriter1 --> BroadcastChannelReader1_2
    BroadcastChannelWriter1 --> BroadcastChannelReader1_3
    BroadcastChannelWriter2 --> ChannelMuxInput2
    ChannelMuxT_1 --> Consuming_Object_1("Consuming Object")

    Consuming_Object_BR1_1 ~~~ ChannelMux
    Consuming_Object_BR2_1 ~~~ ChannelMux
    BroadcastChannel2 ~~~ ChannelMux


```