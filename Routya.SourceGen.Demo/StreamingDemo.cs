using Routya.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Demo
{
    // A stream request produces many items rather than one response.
    //
    // Use IStreamRequest<T> rather than IRequest<IAsyncEnumerable<T>>. The latter returns a task
    // that hands back a sequence, so the pipeline completes the moment the sequence is handed over,
    // before a single item has been produced. A behavior wrapped around that shape cannot observe
    // items, cannot catch a failure part way through, and times only the handover.
    public class GetLargeDatasetRequest : IStreamRequest<DataChunk>
    {
        public int TotalRecords { get; set; }

        public int ChunkSize { get; set; } = 100;
    }

    public class DataChunk
    {
        public int ChunkNumber { get; set; }

        public int StartIndex { get; set; }

        public int Count { get; set; }

        public string[] Data { get; set; } = Array.Empty<string>();
    }

    // The handler is an async iterator, so items are produced lazily and nothing is buffered. The
    // consumer begins receiving chunks while later ones are still being fetched.
    public class GetLargeDatasetHandler : IStreamRequestHandler<GetLargeDatasetRequest, DataChunk>
    {
        public async IAsyncEnumerable<DataChunk> Handle(
            GetLargeDatasetRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var totalChunks = (request.TotalRecords + request.ChunkSize - 1) / request.ChunkSize;

            for (var chunkNumber = 0; chunkNumber < totalChunks; chunkNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startIndex = chunkNumber * request.ChunkSize;
                var count = Math.Min(request.ChunkSize, request.TotalRecords - startIndex);

                // Stands in for a database or API round trip
                await Task.Delay(10, cancellationToken);

                var data = new string[count];

                for (var i = 0; i < count; i++)
                {
                    data[i] = $"Record_{startIndex + i}";
                }

                Console.WriteLine($"  ✓ Produced chunk {chunkNumber + 1}/{totalChunks}: records {startIndex} to {startIndex + count - 1}");

                yield return new DataChunk
                {
                    ChunkNumber = chunkNumber,
                    StartIndex = startIndex,
                    Count = count,
                    Data = data,
                };
            }
        }
    }

    // This is what IStreamRequest<T> buys you.
    //
    // A stream behavior wraps the enumeration itself rather than the call that returns it, so it
    // sees every item as it is produced and its timing covers the whole stream. Run the demo and
    // note that the "produced" and "observed" lines interleave: the behavior is not waiting for the
    // handler to finish.
    public class StreamLoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    {
        public async IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            StreamHandlerDelegate<TResponse> next,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var itemCount = 0;

            Console.WriteLine($"  [behavior] {typeof(TRequest).Name} stream starting");

            await foreach (var item in next(cancellationToken).WithCancellation(cancellationToken))
            {
                itemCount++;
                Console.WriteLine($"  [behavior] observed item {itemCount} at {stopwatch.ElapsedMilliseconds} ms");

                yield return item;
            }

            Console.WriteLine(
                $"  [behavior] {typeof(TRequest).Name} stream finished: {itemCount} items in {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
