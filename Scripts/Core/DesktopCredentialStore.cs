using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Lanternwake.Core;

public interface ICredentialStore
{
    Task<string?> LoadAsync(CancellationToken token);
    Task SaveAsync(string key, CancellationToken token);
    Task ForgetAsync(CancellationToken token);
}

/// <summary>Linux Secret Service via libsecret. Never falls back to a file or shell command.</summary>
public sealed class DesktopCredentialStore(string account = "openrouter") : ICredentialStore
{
    public Task<string?> LoadAsync(CancellationToken token) => RunAsync(Operation.Load, "", token);
    public async Task SaveAsync(string key, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 4096 || key.Any(char.IsControl))
            throw new InvalidDataException("Enter a valid API key.");
        await RunAsync(Operation.Save, key, token).ConfigureAwait(false);
    }
    public async Task ForgetAsync(CancellationToken token) => await RunAsync(Operation.Forget, "", token).ConfigureAwait(false);

    private enum Operation { Load, Save, Forget }
    private async Task<string?> RunAsync(Operation operation, string key, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux()) throw Unavailable();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            return await Task.Run(() => Execute(operation, key, deadline.Token), deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw Unavailable(); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { throw Unavailable(); }
    }

    private string? Execute(Operation operation, string key, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var attributes = new Attributes(account);
        var cancellable = Native.g_cancellable_new();
        try
        {
            using var registration = token.Register(() => Native.g_cancellable_cancel(cancellable));
            IntPtr error = IntPtr.Zero, password = IntPtr.Zero, keyPointer = IntPtr.Zero;
            var keyBytes = 0;
            try
            {
                // A null schema is supported by libsecret. Both attributes are mandatory:
                // no operation may search, replace or remove another application's key.
                var success = 1;
                if (operation == Operation.Load)
                    password = Native.secret_password_lookupv_sync(IntPtr.Zero, attributes.Table, cancellable, out error);
                else if (operation == Operation.Save)
                {
                    var buffer = Encoding.UTF8.GetBytes(key + "\0"); keyBytes = buffer.Length;
                    keyPointer = Marshal.AllocCoTaskMem(keyBytes);
                    try { Marshal.Copy(buffer, 0, keyPointer, keyBytes); }
                    finally { CryptographicOperations.ZeroMemory(buffer); }
                    success = Native.secret_password_storev_sync(IntPtr.Zero, attributes.Table, "default",
                        "Lanternwake — OpenRouter API key", keyPointer, cancellable, out error);
                }
                else
                {
                    // clearv skips locked items. Lookup first so a locked key is either
                    // unlocked by the desktop prompt or reported as an error.
                    password = Native.secret_password_lookupv_sync(IntPtr.Zero, attributes.Table, cancellable, out error);
                    if (error == IntPtr.Zero && password != IntPtr.Zero)
                        success = Native.secret_password_clearv_sync(IntPtr.Zero, attributes.Table, cancellable, out error);
                }
                token.ThrowIfCancellationRequested();
                if (error != IntPtr.Zero || success == 0) throw Unavailable();
                return password == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(password);
            }
            finally
            {
                if (keyPointer != IntPtr.Zero)
                {
                    Marshal.Copy(new byte[keyBytes], 0, keyPointer, keyBytes);
                    Marshal.FreeCoTaskMem(keyPointer);
                }
                if (password != IntPtr.Zero) Native.secret_password_free(password);
                // Native error messages are deliberately never copied, logged or surfaced.
                if (error != IntPtr.Zero) Native.g_error_free(error);
            }
        }
        finally { Native.g_object_unref(cancellable); }
    }

    private static IOException Unavailable() => new("The desktop keyring is unavailable or locked. Unlock it and try again. No API key was saved to a file.");

    private sealed class Attributes : IDisposable
    {
        public IntPtr Table { get; }
        private readonly List<IntPtr> _strings = [];
        private static readonly IntPtr Glib = NativeLibrary.Load("libglib-2.0.so.0");
        public Attributes(string account)
        {
            Table = Native.g_hash_table_new(NativeLibrary.GetExport(Glib, "g_str_hash"), NativeLibrary.GetExport(Glib, "g_str_equal"));
            Add("application", "org.lanternwake.game"); Add("account", account);
        }
        private void Add(string name, string value)
        {
            var namePointer = Marshal.StringToCoTaskMemUTF8(name); _strings.Add(namePointer);
            var valuePointer = Marshal.StringToCoTaskMemUTF8(value); _strings.Add(valuePointer);
            Native.g_hash_table_insert(Table, namePointer, valuePointer);
        }
        public void Dispose()
        {
            Native.g_hash_table_unref(Table);
            foreach (var pointer in _strings) Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static class Native
    {
        private const string Secret = "libsecret-1.so.0", Glib = "libglib-2.0.so.0", Gio = "libgio-2.0.so.0", GObject = "libgobject-2.0.so.0";
        [DllImport(Secret)] internal static extern IntPtr secret_password_lookupv_sync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] internal static extern int secret_password_storev_sync(IntPtr schema, IntPtr attributes,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
            IntPtr password, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] internal static extern int secret_password_clearv_sync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);
        [DllImport(Secret)] internal static extern void secret_password_free(IntPtr password);
        [DllImport(Glib)] internal static extern IntPtr g_hash_table_new(IntPtr hash, IntPtr equal);
        [DllImport(Glib)] internal static extern int g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);
        [DllImport(Glib)] internal static extern void g_hash_table_unref(IntPtr table);
        [DllImport(Glib)] internal static extern void g_error_free(IntPtr error);
        [DllImport(Gio)] internal static extern IntPtr g_cancellable_new();
        [DllImport(Gio)] internal static extern void g_cancellable_cancel(IntPtr cancellable);
        [DllImport(GObject)] internal static extern void g_object_unref(IntPtr instance);
    }
}

/// <summary>Previews and automated game checks must never access the player's keyring.</summary>
public sealed class DisabledCredentialStore : ICredentialStore
{
    public Task<string?> LoadAsync(CancellationToken token) => Task.FromResult<string?>(null);
    public Task SaveAsync(string key, CancellationToken token) => Task.FromException(new IOException("Credential storage is disabled in previews and automated checks."));
    public Task ForgetAsync(CancellationToken token) => Task.FromException(new IOException("Credential storage is disabled in previews and automated checks."));
}
