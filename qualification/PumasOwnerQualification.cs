#if DEBUG
using Godot;
using Lanternwake.Core;
using Lanternwake.Conversation;
using Lanternwake.Presentation;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Http;
using HttpClient = System.Net.Http.HttpClient;

namespace Lanternwake.Qualification;

public partial class PumasOwnerQualification : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameView? _game;
    private int _checks;
    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, Private)!.GetValue(value)!;
    private static void Call(GameView game, string name, params object[] args) => typeof(GameView).GetMethod(name, Private)!.Invoke(game, args);
    private void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); _checks++; }
    private async Task Frames(int count = 2) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate)
    { for (var i = 0; i < 300; i++) { if (predicate()) return; await Frames(1); } throw new TimeoutException("Owned owner fixture did not settle."); }
    private Window Modal => Read<Window>(_game!, "_modal");
    private void Action(string caption) => Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().Single(b => b.Text == caption).EmitSignal(Button.SignalName.Pressed);
    private void Search()
    {
        Call(_game!, "ShowPumasSetup");
        var controls = Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<VBoxContainer>().Single();
        controls.GetNode<LineEdit>("Query").Text = "Owned fixture";
        controls.GetNode<Button>("Search").EmitSignal(Button.SignalName.Pressed);
    }
    public override async void _Ready()
    {
        RpcFixture? service = null; PumasOwnerClient? owner = null;
        try
        {
            var root = System.Environment.GetEnvironmentVariable("LANTERNWAKE_OWNER_FIXTURE") ?? "";
            if (!Path.IsPathFullyQualified(root) || !File.Exists(Path.Combine(root, "owned-fixture")) || !ProjectSettings.GlobalizePath("user://").StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Marked absolute owned profile required.");
            var library = Path.Combine(root, "existing-library"); Directory.CreateDirectory(library);
            var observer = Path.Combine(root, "owned-observer"); File.WriteAllText(observer, "Owned fixture observer seam; no installed Pumas execution.");
            service = new();
            service.Response = method => JsonNode.Parse(File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath("res://"), "integration/pumas/DiscoveryTests/Fixtures", method switch
            { "search_hf_models" => "search.json", "get_hf_download_details" => "details.json", _ => throw new InvalidOperationException("Unexpected fixture command.") })))!.AsObject();
            var descriptor = OwnedPumasFixtures.Description(library, service.Uri.AbsoluteUri);
            var malformed = false; TaskCompletionSource<byte[]>? held = null;
            owner = new(new DescribeHandler(() => descriptor), (_, token) =>
            {
                if (held is not null) return held.Task; // Deliberately retained callback exercises stale close guards.
                var value = descriptor.DeepClone().AsObject(); if (malformed) value["advertisement_schema_version"] = true;
                return Task.FromResult(Encoding.UTF8.GetBytes(value.ToJsonString()));
            });
            var settings = new AiSettings(DialogueProvider.OpenRouter, false, AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter), "saved-hosted-model")
            { LocalLibrary = new(library, observer), Transcription = new(false, Model: "saved-speech") { Profile = "saved-profile" }, CharacterVoices = new() { ["ada"] = "saved-voice" } };
            var settingsPath = Path.Combine(ProjectSettings.GlobalizePath("user://"), "ai-settings.json"); settings.Save(settingsPath);
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>();
            Read<PumasOwnerClient>(_game, "_pumasOwner").Dispose(); typeof(GameView).GetField("_pumasOwner", Private)!.SetValue(_game, owner);
            AddChild(_game); await Frames();
            Check(Read<AiSettings>(_game, "_aiSettings").LocalLibrary == settings.LocalLibrary && service.Bodies.Count == 0, "startup preserves selected existing library with no discovery or startup side effect");
            GetWindow().Size = new(640, 480); Call(_game, "ShowAiSetup"); await Frames();
            var form = Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<VBoxContainer>().Single();
            var dialogue = form.GetNode<VBoxContainer>("Capabilities/Dialogue");
            Check(dialogue.GetNode<LineEdit>("LibraryRoot").Text == library && dialogue.GetNode<LineEdit>("PumasObserver").Text == observer, "native form exposes saved root and observer");
            Check(Modal.Size.X <= 640, "owner selection labels wrap in native 640px setup");
            var save = (Button)Modal.FindChild("SaveConfiguration", true, false);
            save.EmitSignal(Button.SignalName.Pressed);
            await Until(() => !save.Disabled);
            var preserved = AiSettings.Load(settingsPath, settings);
            Check(preserved.Provider == settings.Provider && preserved.Model == settings.Model && preserved.Transcription.Profile == "saved-profile" && preserved.CharacterVoices["ada"] == "saved-voice", "saving selection preserves owner hosted/speech/voice configuration");
            Call(_game, "CloseModal");
            malformed = true; Search(); await Until(() => Modal.Title == "Pumas setup unavailable");
            Check(service.Bodies.Count == 0 && Read<PumasClient?>(_game, "_setupPumas") is null, "malformed scalar descriptor becomes static refusal before any RPC/client acquisition");
            malformed = false; descriptor["instance"]!["library_root"] = root; Search(); await Until(() => Modal.Title == "Pumas setup unavailable");
            Check(service.Bodies.Count == 0, "wrong selected root has no HTTP-only or startup fallback");
            descriptor["instance"]!["library_root"] = library;
            Search(); await Until(() => Modal.Title == "Pumas search results");
            Check(Read<PumasOwnerObservation>(_game, "_setupOwner").Endpoint == service.Uri.AbsoluteUri && service.Bodies.Count == 1, "setup uses authenticated selected owner instead of saved hosted/default environment endpoint");
            Check(service.Acquisitions == 0 && service.Bodies.First()["method"]!.GetValue<string>() == "search_hf_models", "borrowed search never downloads a model");
            Action("Inspect Fixture/Dialogue-GGUF"); await Until(() => Modal.Title == "Pumas download options");
            var option = Modal.GetNode<VBoxContainer>("%ModalActions").GetChildren().OfType<Button>().First(b => b.Text != "Back to setup").Text;
            Action(option); await Until(() => Modal.Title == "Review Pumas download request");
            service.Result = new JsonObject { ["outcome"] = "matches", ["candidates"] = new JsonArray(new JsonObject { ["identity"] = new JsonObject { ["model_ref"] = new JsonObject { ["model_ref_contract_version"] = 1, ["model_id"] = "owned/existing-model" } }, ["state"] = "incomplete" }) };
            Action("Request download through Pumas"); await Until(() => Modal.Title == "Pumas setup unavailable");
            Check(service.Acquisitions == 0 && Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains("local_model_exists"), "native explicit download action reuses/repairs local incomplete match without duplication");
            async Task ReviewRequest()
            {
                Search(); await Until(() => Modal.Title == "Pumas search results");
                Action("Inspect Fixture/Dialogue-GGUF"); await Until(() => Modal.Title == "Pumas download options");
                Action(option); await Until(() => Modal.Title == "Review Pumas download request");
                Action("Request download through Pumas"); await Until(() => Modal.Title == "Pumas setup unavailable");
            }
            service.Result = new JsonObject { ["outcome"] = "matches", ["candidates"] = new JsonArray() }; service.Rejected = true;
            await ReviewRequest();
            Check(service.Acquisitions == 1 && Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains("acquisition_unknown"), "native legacy backend refusal retains uncertain admission");
            await ReviewRequest();
            Check(service.Acquisitions == 1, "native second explicit click cannot replay uncertain submission");
            service.Rejected = false;
            descriptor["service_generation"] = "changed-owner"; Search(); await Until(() => Modal.Title == "Pumas setup unavailable");
            var requests = service.Bodies.Count;
            Check(Modal.GetNode<RichTextLabel>("%ModalText").Text.Contains("library_owner_unavailable"), "changed owner requires explicit reselection");
            descriptor["service_generation"] = "owned-http-generation";
            held = new(TaskCreationOptions.RunContinuationsAsynchronously); Search(); await Frames();
            Call(_game, "CloseModal"); Call(_game, "ShowHistory"); var newer = Modal;
            held.SetResult(Encoding.UTF8.GetBytes(descriptor.ToJsonString())); held = null;
            await Read<OwnedOperations>(_game, "_operations").DrainAsync();
            Check(Modal == newer && service.Bodies.Count == requests, "late cancelled discovery cannot request or replace a newer window");
            Check(AiSettings.Load(settingsPath, settings).LocalLibrary == settings.LocalLibrary, "lookup/cancel never rewrites library selection");
            held = new(TaskCreationOptions.RunContinuationsAsynchronously); Search(); await Frames();
            var pendingExit = Read<OwnedOperations>(_game, "_operations"); var beforeExit = service.Bodies.Count;
            Check(await _game.Audio.StopAndRetireAsync(), "native graceful audio teardown");
            _game.Free(); _game = null;
            held.SetResult(Encoding.UTF8.GetBytes(descriptor.ToJsonString())); held = null;
            await pendingExit.DrainAsync(); await Frames(3);
            Check(service.Bodies.Count == beforeExit, "pending discovery after tree exit cannot touch freed UI or acquire");
            owner = new(new DescribeHandler(() => descriptor), (_, _) => Task.FromResult(Encoding.UTF8.GetBytes(descriptor.ToJsonString())));
            _game = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<GameView>();
            Read<PumasOwnerClient>(_game, "_pumasOwner").Dispose(); typeof(GameView).GetField("_pumasOwner", Private)!.SetValue(_game, owner);
            AddChild(_game); await Frames(); Search(); await Until(() => Modal.Title == "Pumas search results");
            var localClient = Read<PumasClient>(_game, "_setupPumas");
            Read<HttpClient>(localClient, "_http").Dispose();
            var pendingRpc = new HeldRpcHandler();
            typeof(PumasClient).GetField("_http", Private)!.SetValue(localClient, new HttpClient(pendingRpc) { BaseAddress = service.Uri, Timeout = Timeout.InfiniteTimeSpan });
            Search(); await Until(() => pendingRpc.Started);
            var operationsAtExit = Read<OwnedOperations>(_game, "_operations");
            Check(await _game.Audio.StopAndRetireAsync(), "second native graceful audio teardown");
            _game.Free(); _game = null;
            await operationsAtExit.DrainAsync(); await Frames(3);
            Check(pendingRpc.Cancelled && pendingRpc.Disposed, "in-flight setup RPC settles before borrower disposal at tree exit");
            using var borrower = new PumasClient(service.Uri);
            Check((await borrower.QueryExistingHfModelsAsync(new("Fixture/Dialogue-GGUF", "Fixture", "Dialogue", "Q4_K_M", null))).Success, "tree exit closes local borrower and leaves existing service running");
            GD.Print($"LANTERNWAKE_OWNER_REUSE_OK checks={_checks} rpc={service.Bodies.Count} acquisitions={service.Acquisitions} startup=false authentication=owned_fixture");
            await service.DisposeAsync(); service = null; GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { owner?.Dispose(); if (service is not null) await service.DisposeAsync(); }
    }
    public override void _ExitTree() { if (GodotObject.IsInstanceValid(_game)) _game!.Free(); }
}
internal sealed class HeldRpcHandler : HttpMessageHandler
{
    public bool Started { get; private set; }
    public bool Cancelled { get; private set; }
    public bool Disposed { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        Started = true;
        try { await Task.Delay(Timeout.Infinite, cancellation); throw new InvalidOperationException("Held owned RPC unexpectedly completed."); }
        catch (OperationCanceledException) { Cancelled = true; throw; }
    }
    protected override void Dispose(bool disposing) { Disposed = disposing; base.Dispose(disposing); }
}
#endif
