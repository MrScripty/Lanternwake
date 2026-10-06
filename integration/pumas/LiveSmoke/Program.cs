using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lanternwake.Conversation;
using Lanternwake.Core;

// Match the game's authored context and fact intersection. No invented
// situation or private character bio is supplied to the model.
var story = Story.Parse(File.ReadAllText(args.Length > 0 ? args[0] : "Content/story.json"));
var session = new StorySession(story);
var target = args.Length > 1 ? args[1] : story.Chapters.SelectMany(c => c.Scenes)
    .SelectMany(s => s.Beats).First(b => b.Conversation is not null).Id;
while (session.Beat.Id != target)
{
    if (session.Beat.Activity is { } activity) session.AnswerActivity(activity.CorrectIndex);
    if (!session.Advance()) throw new ArgumentException("Conversation beat was not found: " + target);
}
var conversation = session.Beat.Conversation ?? throw new ArgumentException("Beat has no conversation.");
var character = story.Characters.Single(c => c.Id == conversation.CharacterId);
var context = session.ConversationContext();
var before = JsonSerializer.Serialize(session.Snapshot(), Story.Json);

using var client = new PumasClient();
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(50));
var clock = Stopwatch.StartNew();
var result = await client.GenerateAsync(conversation.CharacterId, context,
    args.Length > 2 ? args[2] : conversation.Suggestions[0], cancellation.Token);
var unchanged = before == JsonSerializer.Serialize(session.Snapshot(), Story.Json);
Console.WriteLine(JsonSerializer.Serialize(new
{
    success = result.Success, errorCode = result.ErrorCode, text = result.Text,
    elapsedMs = clock.ElapsedMilliseconds, beatId = target, characterId = character.Id,
    contextSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context))).ToLowerInvariant(),
    canonicalUnchanged = unchanged
}));
return result.Success && unchanged ? 0 : 1;
