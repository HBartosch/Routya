using Routya.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Demo
{
    // Request that returns a stream of data
    public class GetLargeDatasetRequest : IRequest<IAsyncEnumerable<DataChunk>>
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

    // Handler that streams data in chunks
    public class GetLargeDatasetHandler : IAsyncRequestHandler<GetLargeDatasetRequest, IAsyncEnumerable<DataChunk>>
    {
        public async Task<IAsyncEnumerable<DataChunk>> HandleAsync(
            GetLargeDatasetRequest request, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"  → Starting to stream {request.TotalRecords} records in chunks of {request.ChunkSize}...");
            return StreamDataAsync(request, cancellationToken);
        }

        private async IAsyncEnumerable<DataChunk> StreamDataAsync(
            GetLargeDatasetRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            int totalChunks = (request.TotalRecords + request.ChunkSize - 1) / request.ChunkSize;
            
            for (int chunkNumber = 0; chunkNumber < totalChunks; chunkNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int startIndex = chunkNumber * request.ChunkSize;
                int count = Math.Min(request.ChunkSize, request.TotalRecords - startIndex);
                
                // Simulate database/API fetch delay
                await Task.Delay(10, cancellationToken);
                
                var data = new string[count];
                for (int i = 0; i < count; i++)
                {
                    data[i] = $"Record_{startIndex + i}";
                }
                
                Console.WriteLine($"  ✓ Chunk {chunkNumber + 1}/{totalChunks}: Records {startIndex} to {startIndex + count - 1}");
                
                yield return new DataChunk
                {
                    ChunkNumber = chunkNumber,
                    StartIndex = startIndex,
                    Count = count,
                    Data = data
                };
            }
            
            Console.WriteLine($"  ✓ Stream completed: {request.TotalRecords} total records");
        }
    }
}
