# SC-006 before-image — construction sites as of 2026-09-07T18:35Z

## CoopFileCarrier.cs:183-190 (file plane)
```csharp
        var frame = new YnetFrame
        {
            Origin = _self.Identity,
            Sequence = Interlocked.Increment(ref _sequence) - 1,
            SenderNode = _self.Node,
            SenderActor = _self.Actor,
            Signal = signal,
            Body = body,
```

## QuicCarrier.cs:333-341 (wire plane)
```csharp
        var frame = new YnetFrame
        {
            Origin = _self.NodeId.ToString(),
            Sequence = Interlocked.Increment(ref _sequence),
            SenderNode = _self.NodeId.ToString(),
            SenderActor = _peer.Actor,
            Signal = message.Summary,
            Body = Encoding.UTF8.GetString(message.Body.Span),
        };
```

## T-001 baseline suite
ynet_client.tests: Failed 0, Passed 177, Skipped 0, Total 177
