using System.Diagnostics;
using Lanternwake.Conversation;

using var client = new PumasClient();
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(50));
var clock = Stopwatch.StartNew();
var result = await client.GenerateAsync("nessa", "You are Nessa Ward, a fictional island keeper. The storm has grounded the ferry. You do not know when it will resume. No other facts are available.", "Can I leave on the ferry tonight?", cancellation.Token);
Console.WriteLine($"success={result.Success} elapsed_ms={clock.ElapsedMilliseconds} error={result.ErrorCode}");
if (result.Success) Console.WriteLine(result.Text);
return result.Success ? 0 : 1;
