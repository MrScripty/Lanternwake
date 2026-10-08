using Lanternwake.Qualification;
using static Lanternwake.Qualification.OwnedPumasFixtures;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Lanternwake.Core;
using Lanternwake.Conversation;

var checks = 0;
var owned = Path.Combine(Path.GetTempPath(), "lanternwake-owned selection-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(owned);
var executable = Path.Combine(owned, "owned observer"); File.WriteAllText(executable, "owned fixture; not installed Pumas");
var selection = new PumasLibrarySelection(owned, executable);
try
{
    var endpoint = "http://127.0.0.1:45678/";
    var description = Description(owned, endpoint);
    using (var observer = Observer(() => description))
    {
        var receipt = await observer.AuthenticateAsync(selection, endpoint, default);
        Check(receipt.Lifetime == "Borrowed" && receipt.RegistryLibraryId == "owned-fixture-library", "supported omitted provenance accepted; borrowed observation");
        Check(receipt.Endpoint == endpoint && receipt.LibraryRoot == owned, "selected root and endpoint retained");
        var reordered = JsonNode.Parse(description.ToJsonString())!.AsObject(); var moved = reordered["endpoint"]!.DeepClone(); reordered.Remove("endpoint"); reordered["endpoint"] = moved;
        Check(PumasOwnerClient.Parse(Encoding.UTF8.GetBytes(reordered.ToJsonString()), selection, endpoint).Descriptor == receipt.Descriptor, "property ordering does not invent changed ownership");
    }
    foreach (var mutation in new Action<JsonObject>[] {
        d=>d["advertisement_schema_version"]=true,
        d=>d["advertisement_schema_version"]=2,
        d=>d["service_generation"]="",
        d=>d["token"]="never export credentials",
        d=>d["endpoint"]="http://localhost:45678/",
        d=>d["endpoint"]="http://127.0.0.1:0/",
        d=>d["endpoint"]="http://127.0.0.1:45679/",
        d=>d["instance"]!["library_root"]=owned+"-missing",
        d=>d["instance"]!["generation"]="",
        d=>d["instance"]!["registry_library_id"]=null,
        d=>d["instance"]!["selector_schema_version"]="1",
        d=>d["instance"]!["capabilities"]=new JsonArray("model.query@1"),
        d=>d["instance"]!["protocols"]=new JsonArray(),
        d=>d["build_info"]!["component"]="wrong",
        d=>d["build_info"]!["schemas"]![0]!["version"]=true,
        d=>d["build_info"]!["protocols"]![1]!["versions"]=new JsonArray(2),
        d=>d["build_info"]!["compiled_features"]=new JsonArray("same","same") })
    {
        var changed = description.DeepClone().AsObject(); mutation(changed);
        await Reject(async()=> { using var observer=Observer(()=>changed); await observer.AuthenticateAsync(selection, endpoint, default); }, "malformed/ambiguous/unauthenticated descriptor refused");
    }
    await Reject(()=>Task.Run(()=>PumasOwnerClient.Parse(Encoding.UTF8.GetBytes(description.ToJsonString().Replace("\"advertisement_schema_version\":1", "\"advertisement_schema_version\":1,\"advertisement_schema_version\":1")),selection,endpoint)), "duplicate keys refused");
    await Reject(()=>Task.Run(()=>PumasOwnerClient.Parse(new byte[65537],selection,endpoint)), "bounded description");
    using (var owner = Observer(()=>description, live:()=> {var changed=description.DeepClone().AsObject();changed["service_generation"]="successor";return changed;}))
        await Reject(()=>owner.AuthenticateAsync(selection,endpoint,default), "HTTP description cannot authenticate a changed owner");
    var observations=0;
    using(var owner=Observer(()=> {var changed=description.DeepClone().AsObject();if(++observations>1)changed["instance"]!["generation"]="successor";return changed;}))
        await Reject(()=>owner.AuthenticateAsync(selection,endpoint,default), "final core reauthentication detects turnover");
    using(var owner=Observer(()=>description))
    {
        var previous=await owner.AuthenticateAsync(selection,endpoint,default);
        var changed=description.DeepClone().AsObject();changed["service_generation"]="successor";
        using var successor=Observer(()=>changed);
        await Reject(()=>successor.AuthenticateAsync(selection,endpoint,default,previous), "pinned owner cannot silently retarget");
    }
    using(var refused=new PumasOwnerClient(new DescribeHandler(()=>description), (_,_)=>throw new IOException("owned refusal")))
        await Reject(()=>refused.AuthenticateAsync(selection,endpoint,default), "observer failure has no HTTP-only fallback or startup");
    var deadListener = new TcpListener(IPAddress.Loopback, 0); deadListener.Start();
    var deadEndpoint = $"http://127.0.0.1:{((IPEndPoint)deadListener.LocalEndpoint).Port}/"; deadListener.Stop();
    var deadDescription = Description(owned, deadEndpoint);
    using(var deadOwner=new PumasOwnerClient(new SocketsHttpHandler { UseProxy=false, AllowAutoRedirect=false },
        (_,_)=>Task.FromResult(Encoding.UTF8.GetBytes(deadDescription.ToJsonString()))))
        await Reject(()=>deadOwner.AuthenticateAsync(selection,deadEndpoint,default), "dead owned HTTP listener cannot authorize fallback startup or acquisition");
    using(var owner=new PumasOwnerClient(new DescribeHandler(()=>description), async(_,token)=> {await Task.Delay(Timeout.Infinite,token);return []; }))
    {
        var waiting=owner.AuthenticateAsync(selection,endpoint,default);owner.Dispose();
        await Reject(()=>waiting,"disposing finite observer cancels its read, never a borrowed daemon");
        await Reject(()=>owner.AuthenticateAsync(selection,endpoint,default),"disposed borrower cannot reacquire");
    }
    var preferences=new AiSettings(DialogueProvider.OpenRouter,false,AiSettings.DefaultEndpoint(DialogueProvider.OpenRouter),"fixture-text")
        {LocalLibrary=selection,Transcription=new(true,Model:"speech-fixture") {Profile="fixture-profile"},CharacterVoices=new() {["ada"]="fixture-voice"}};
    var settingsPath=Path.Combine(owned,"preferences.json");preferences.Save(settingsPath);
    var loaded=AiSettings.Load(settingsPath,AiSettings.FromEnvironment());
    Check(loaded.LocalLibrary==selection && loaded.Provider==DialogueProvider.OpenRouter && loaded.Transcription.Profile=="fixture-profile" && loaded.CharacterVoices["ada"]=="fixture-voice","additive selection preserves hosted provider and speech/voice preferences");
    await Reject(()=>Task.Run(()=>new PumasLibrarySelection(owned,"").Validate()),"partial selection refused");
    await Reject(()=>Task.Run(()=>new PumasLibrarySelection("relative",executable).Validate()),"relative root refused");
    File.WriteAllText(settingsPath,"{\"Provider\":0,\"DialogueEnabled\":false,\"Endpoint\":\"http://127.0.0.1:8080/\",\"Model\":\"\"}");
    Check(!AiSettings.Load(settingsPath,AiSettings.FromEnvironment()).LocalLibrary.Selected,"legacy settings remain readable without inferred root");
    await using var rpc=new RpcFixture();
    using var client=new PumasClient(rpc.Uri);
    var advertised=Description(owned,rpc.Uri.AbsoluteUri);using var authenticated=Observer(()=>advertised);
    var observed=await authenticated.AuthenticateAsync(selection,rpc.Uri.AbsoluteUri,default);
    var gate=new PumasAcquisitionGate();
    var requested=new HfDownloadRequest("OwnedFixture/Dialogue-GGUF","OwnedFixture","Dialogue","Q4_K_M",null);
    foreach(var state in new[]{"ready","incomplete","unsatisfied"})
    {
        rpc.Result=new JsonObject { ["outcome"]="matches",["candidates"]=new JsonArray(Candidate(state)) };
        var result=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
        Check(!result.Success && result.ErrorCode=="local_model_exists" && rpc.Acquisitions==0,"local "+state+" match blocks duplicate and invented readiness");
    }
    rpc.Result=new JsonObject {["outcome"]="matches",["candidates"]=new JsonArray(Candidate("ready"),Candidate("ready","other/model"))};
    Check(!(await gate.RequestAsync(client,authenticated,selection,observed,requested,default)).Success && rpc.Acquisitions==0,"multiple local matches never select a winner");
    foreach(var malformed in new JsonObject[] {
        new(){["outcome"]="unavailable",["diagnostics"]=new JsonArray()},
        new(){["outcome"]="unsupported",["diagnostics"]=new JsonArray()},
        new(){["outcome"]="invalid_requirement",["diagnostics"]=new JsonArray()},
        new(){["outcome"]="matches",["candidates"]=new JsonArray(),["diagnostics"]=new JsonArray("failure")},
        new(){["outcome"]="matches",["candidates"]=new JsonArray(),["error"]="failure"},
        new(){["outcome"]="matches"},
        new(){["outcome"]="matches",["candidates"]=new JsonArray(Candidate("available"))},
        new(){["outcome"]="matches",["candidates"]=new JsonArray(Candidate("ready"),Candidate("ready"))} })
    {
        rpc.Result=malformed;
        var result=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
        Check(!result.Success && result.ErrorCode=="local_query_blocked" && rpc.Acquisitions==0,"query refusal/malformed outcome blocks before submission");
    }
    rpc.Result=new JsonObject{["outcome"]="matches",["candidates"]=new JsonArray()};
    var accepted=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
    Check(accepted.Success && rpc.Acquisitions==1,"only explicit empty-match action requests acquisition once");
    var query=rpc.Bodies.First(b=>b["method"]!.GetValue<string>()=="intent_query_models")["params"]!["requirement"]!;
    Check(query["acquisition_policy"]!.GetValue<string>()=="local_only" && query["selector"]!["kind"]!.GetValue<string>()=="upstream_repository" && query["artifact"]!["format"]!.GetValue<string>()=="gguf","producer-owned exact local-only query wire");
    Check(rpc.Bodies.All(b=>b["method"]!.GetValue<string>() is "intent_query_models" or "start_model_download_from_hf"),"no get/ensure, startup or native-binding command");
    var repeated=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
    Check(repeated.ErrorCode=="acquisition_already_requested" && rpc.Acquisitions==1,"accepted receipt cannot create a duplicate in session");
    requested=requested with {RepoId="OwnedFixture/Second-GGUF"};
    rpc.Unknown=true;var uncertain=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);var submitted=rpc.Acquisitions;
    Check(!uncertain.Success,"unknown submitted receipt retained as refusal");
    var replay=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
    Check(replay.ErrorCode=="acquisition_unknown" && rpc.Acquisitions==submitted,"uncertain acquisition cannot be replayed in session");
    rpc.Unknown=false;rpc.Rejected=true;requested=requested with {RepoId="OwnedFixture/Refused-GGUF"};
    var refusedSubmission=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);var refusedCount=rpc.Acquisitions;
    Check(refusedSubmission.ErrorCode=="acquisition_unknown","legacy success:false does not prove non-admission");
    var refusedReplay=await gate.RequestAsync(client,authenticated,selection,observed,requested,default);
    Check(refusedReplay.ErrorCode=="acquisition_unknown" && rpc.Acquisitions==refusedCount,"durably admitted backend refusal cannot be replayed");
    rpc.Rejected=false;
    var before=rpc.Bodies.Count;using var cancelled=new CancellationTokenSource();cancelled.Cancel();
    var cancelledResult=await new PumasAcquisitionGate().RequestAsync(client,authenticated,selection,observed,requested,cancelled.Token);
    Check(!cancelledResult.Success && rpc.Bodies.Count==before,"pre-cancellation sends no query or acquisition");
    authenticated.Dispose();rpc.Unknown=false;
    Check((await client.QueryExistingHfModelsAsync(requested)).Success,"borrower close leaves existing service alive");
    if(!OperatingSystem.IsWindows())
    {
        var script=Path.Combine(owned,"owned observer script");var jsonFile=Path.Combine(owned,"owned-description.json");var argsFile=Path.Combine(owned,"owned-args.txt");
        File.WriteAllText(jsonFile,description.ToJsonString());
        File.WriteAllText(script,$"#!/bin/sh\nprintf '%s\\n' \"$@\" > '{argsFile}'\ncat '{jsonFile}'\n");File.SetUnixFileMode(script,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var bytes=await PumasOwnerClient.ObserveAsync(new(owned,script),default);
        Check(JsonNode.Parse(bytes) is not null && File.ReadAllLines(argsFile).SequenceEqual(new[]{"--describe-local-http","--launcher-root",owned}),"real owned CLI process preserves spaced arguments and supported read-only command");
        File.WriteAllText(script,"#!/bin/sh\nexec sleep 30\n");using var deadline=new CancellationTokenSource(80);
        await Reject(()=>PumasOwnerClient.ObserveAsync(new(owned,script),deadline.Token),"cancel actual owned observer process; no daemon PID involved");
        File.WriteAllText(script,"#!/bin/sh\ni=0; while [ \"$i\" -lt 700 ]; do printf '%0100d' 0; i=$((i+1)); done\n");
        await Reject(()=>PumasOwnerClient.ObserveAsync(new(owned,script),default),"actual stdout bound rejects an oversized observer without unbounded buffering");
        File.WriteAllText(script,"#!/bin/sh\nexit 3\n");await Reject(()=>PumasOwnerClient.ObserveAsync(new(owned,script),default),"actual failed CLI cannot authorize startup");
    }
    Console.WriteLine($"PASS selected-owner and local-first assertions={checks}; owned CLI/HTTP fixtures only; no Pumas startup, actual acquisition, microphone or model inference.");
}
finally {Directory.Delete(owned,true);}
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
async Task Reject(Func<Task> operation,string name){try{await operation();}catch(Exception e) when(e is InvalidDataException or IOException or OperationCanceledException or ObjectDisposedException or HttpRequestException){Check(true,name);return;}throw new Exception("Unexpected admission: "+name);}
PumasOwnerClient Observer(Func<JsonObject> describe,Func<JsonObject>? live=null)=>new(new DescribeHandler(live??describe),(_,_)=>Task.FromResult(Encoding.UTF8.GetBytes(describe().ToJsonString())));
static JsonObject Candidate(string state,string id="owned/model")=>new(){["state"]=state,["identity"]=new JsonObject{["model_ref"]=new JsonObject{["model_ref_contract_version"]=1,["model_id"]=id}}};
