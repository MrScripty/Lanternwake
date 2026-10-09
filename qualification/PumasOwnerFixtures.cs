#if DEBUG
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
namespace Lanternwake.Qualification;

// Marked owned test fixtures. No registry authentication, downloads or inference qualification.
internal static class OwnedPumasFixtures
{
public static JsonObject Description(string root,string endpoint)
{
    JsonObject Protocol(string name)=>new(){["name"]=name,["versions"]=new JsonArray(1)};
    JsonObject Build(string component)
    {
        var schemas=new JsonArray(new JsonObject{["name"]="pumas.build-info",["version"]=1},new JsonObject{["name"]="pumas.discovery",["version"]=1},new JsonObject{["name"]="pumas.model-ref",["version"]=1},new JsonObject{["name"]="pumas.model-selector",["version"]=1});
        var protocols=new JsonArray(Protocol("pumas.local-ipc"));
        if(component=="pumas-rpc"){schemas.Add(new JsonObject{["name"]="pumas.http-advertisement",["version"]=1});protocols.Add(Protocol("pumas.local-http"));}
        return new(){["build_info_schema_version"]=1,["component"]=component,["package_version"]="0.7.0",["compiled_features"]=new JsonArray(),["protocols"]=protocols,["schemas"]=schemas};
    }
    return new(){["advertisement_schema_version"]=1,["service_generation"]="owned-http-generation",["endpoint"]=endpoint,["build_info"]=Build("pumas-rpc"),["instance"]=new JsonObject{["discovery_schema_version"]=1,["build_info"]=Build("pumas-library"),["registry_library_id"]="owned-fixture-library",["library_root"]=root,["generation"]="owned-core-generation",["pumas_version"]="0.7.0",["protocols"]=new JsonArray(Protocol("pumas.local-ipc")),["capabilities"]=new JsonArray("model.query@1","model.get.local@1","model.selector@1","artifact.resolve@1"),["model_ref_schema_version"]=1,["selector_schema_version"]=1}};
}
}
internal sealed class DescribeHandler(Func<JsonObject> value):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    { token.ThrowIfCancellationRequested();if(request.Method!=HttpMethod.Get||request.RequestUri!.AbsolutePath!="/.well-known/pumas")throw new Exception("unexpected owner route");var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(value().ToJsonString(),Encoding.UTF8,"application/json")};response.Headers.CacheControl=new(){NoStore=true};return Task.FromResult(response); }
}
internal sealed class RpcFixture:IAsyncDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly Task loop;
    public Uri Uri {get;}public ConcurrentQueue<JsonObject> Bodies {get;}=new();public JsonObject Result {get;set;}=new(){["outcome"]="matches",["candidates"]=new JsonArray()};public bool Unknown { get; set; } public bool Rejected { get; set; }public int Acquisitions;
    public Func<string, JsonObject>? Response { get; set; }
    public RpcFixture(){listener.Start();Uri=new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");loop=Serve();}
    async Task Serve()
    {
        try{while(!stop.IsCancellationRequested){using var connection=await listener.AcceptTcpClientAsync(stop.Token);using var stream=connection.GetStream();var header=new List<byte>();var one=new byte[1];
            while(header.Count<8192){if(await stream.ReadAsync(one,stop.Token)==0)throw new IOException("short request");header.Add(one[0]);if(header.Count>=4 && header.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10}))break;}
            var headers=Encoding.ASCII.GetString(header.ToArray());var line=headers.Split("\r\n").Single(l=>l.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase));var size=int.Parse(line.Split(':')[1]);if(size>65536)throw new Exception("request unbounded");var bytes=new byte[size];await stream.ReadExactlyAsync(bytes,stop.Token);var body=JsonNode.Parse(bytes)!.AsObject();Bodies.Enqueue(body);
            var method=body["method"]!.GetValue<string>();JsonObject result;
            if (Response is not null && method is not ("intent_query_models" or "start_model_download_from_hf")) result=Response(method);else if(method=="intent_query_models")result=Result.DeepClone().AsObject();else if(method=="start_model_download_from_hf"){Acquisitions++;result=Rejected ? new JsonObject{["success"]=false,["error"]="Owned synthetic failure after durable admission"} : new(){["success"]=true,["download_id"]="owned-download",["selectedArtifactId"]="owned-artifact",["artifactId"]="owned-artifact"};}else throw new Exception("unexpected mutation");
            var envelope=new JsonObject{["jsonrpc"]="2.0",["id"]=Unknown && method=="start_model_download_from_hf"?"wrong-id":body["id"]!.DeepClone(),["result"]=result};var output=Encoding.UTF8.GetBytes(envelope.ToJsonString());var response=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {output.Length}\r\nConnection: close\r\n\r\n");await stream.WriteAsync(response,stop.Token);await stream.WriteAsync(output,stop.Token);
        }}catch(OperationCanceledException){}catch(ObjectDisposedException){}
    }
    public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();await loop;stop.Dispose();}
}

#endif
