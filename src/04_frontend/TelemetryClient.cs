using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Nnafa.Telemetry.V1;

namespace Frontend
{
    public class TelemetryClient
    {
        private readonly ClientWebSocket _client = new();
        private readonly Uri _uri = new("ws://127.0.0.1:8765");
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public event Action<TelemetryPayload>? OnTelemetryReceived;

        public async Task ConnectAsync(CancellationToken ct)
        {
            try
            {
                await _client.ConnectAsync(_uri, ct);
                _ = Task.Run(() => ReceiveLoopAsync(ct), ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[65536]; // Increased to 64KB
            while (_client.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Binary && ms.Length > 0)
                    {
                        var payload = TelemetryPayload.Parser.ParseFrom(ms.ToArray());
                        
                        await _semaphore.WaitAsync(ct);
                        try
                        {
                            OnTelemetryReceived?.Invoke(payload);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client.State == WebSocketState.Open)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
            }
            _client.Dispose();
        }
    }
}
