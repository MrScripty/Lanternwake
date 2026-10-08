using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 3) throw new ArgumentException("Expected source root, DLL and portable PDB.");
var root = Path.GetFullPath(args[0]) + Path.DirectorySeparatorChar;
using var assembly = File.OpenRead(args[1]);
using var pe = new PEReader(assembly);
using var symbols = File.OpenRead(args[2]);
using var provider = MetadataReaderProvider.FromPortablePdbStream(symbols);
var pdb = provider.GetMetadataReader();
var id = new BlobContentId(pdb.DebugMetadataHeader!.Id);
var entries = pe.ReadDebugDirectory().Where(e => e.Type == DebugDirectoryEntryType.CodeView).ToArray();
if (entries.Length != 1 || pe.ReadCodeViewDebugDirectoryData(entries[0]).Guid != id.Guid || entries[0].Stamp != id.Stamp)
    throw new InvalidDataException("DLL CodeView identity does not match portable PDB.");
var sha256 = new Guid("8829d00f-11b8-4213-878b-770e8597ac16");
var sha1 = new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460");
var embeddedSource = new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
var documents = new List<object>();
foreach (var handle in pdb.Documents)
{
    var document = pdb.GetDocument(handle);
    var name = Path.GetFullPath(pdb.GetString(document.Name));
    if (!name.StartsWith(root, StringComparison.Ordinal))
        throw new InvalidDataException($"Compiler document outside owned source: {name}");
    var algorithm = pdb.GetGuid(document.HashAlgorithm);
    var expected = pdb.GetBlobBytes(document.Hash);
    byte[] bytes;
    var origin = "disk";
    if (File.Exists(name)) bytes = File.ReadAllBytes(name);
    else
    {
        var embedded = pdb.GetCustomDebugInformation(handle).Select(h => pdb.GetCustomDebugInformation(h))
            .Single(info => pdb.GetGuid(info.Kind) == embeddedSource);
        var blob = pdb.GetBlobBytes(embedded.Value);
        var length = BitConverter.ToInt32(blob, 0);
        if (length == 0) bytes = blob[4..];
        else
        {
            using var compressed = new MemoryStream(blob, 4, blob.Length - 4);
            using var inflater = new DeflateStream(compressed, CompressionMode.Decompress);
            using var expanded = new MemoryStream();
            inflater.CopyTo(expanded);
            bytes = expanded.ToArray();
            if (bytes.Length != length) throw new InvalidDataException("Embedded compiler source length differs.");
        }
        origin = "embedded-in-pdb";
    }
    var actual = algorithm == sha256 ? SHA256.HashData(bytes) : algorithm == sha1 ? SHA1.HashData(bytes) :
        throw new InvalidDataException($"Unsupported compiler checksum algorithm: {algorithm}");
    if (!actual.SequenceEqual(expected)) throw new InvalidDataException($"Compiler checksum mismatch: {name}");
    documents.Add(new { path = Path.GetRelativePath(root, name), origin, compilerChecksum = Convert.ToHexString(expected).ToLowerInvariant() });
}
if (documents.Count == 0) throw new InvalidDataException("No compiler source documents.");
Console.WriteLine(JsonSerializer.Serialize(new { dllPdbMatch = true, pdbGuid = id.Guid, pdbStamp = id.Stamp, documents }));
