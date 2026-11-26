using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Classroom;
// Supplied helper. Students use the public properties and response methods.
[System.Diagnostics.DebuggerNonUserCode]
public sealed class Request : IDisposable
{
    private const int MaxBodyBytes = 1024 * 1024;
    // Let beginners use public fields as well as properties in their classes.
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true
    };
    private readonly HttpListenerContext context;
    private readonly string websiteRoot;
    private bool responded;
    public string Method => context.Request.HttpMethod;
    public string Path { get; private set; } = "/";
    public NameValueCollection Query { get; private set; } = new NameValueCollection();
    public NameValueCollection Form { get; private set; } = new NameValueCollection();
    public string Body { get; private set; } = "";
    internal Request(HttpListenerContext context, string websiteRoot)
    {
        this.context = context;
        this.websiteRoot = System.IO.Path.GetFullPath(websiteRoot);
    }
    internal void ReadInput()
    {
        string raw = context.Request.RawUrl ?? "/";
        int question = raw.IndexOf('?');
        string rawPath = question < 0 ? raw : raw[..question];
        Path = Uri.UnescapeDataString(rawPath);
        if (!Path.StartsWith('/') || Path.Contains('\0'))
            throw new RequestInputException(400, "Invalid path.");
        Query = ParseFields(question < 0 ? "" : raw[(question + 1)..]);
        if (context.Request.ContentLength64 > MaxBodyBytes)
            throw new RequestInputException(413, "Request body is too large (maximum 1 MiB).");

        if (!context.Request.HasEntityBody)
            return;
        using MemoryStream body = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = context.Request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (body.Length + read > MaxBodyBytes)
                throw new RequestInputException(413, "Request body is too large (maximum 1 MiB).");
            body.Write(buffer, 0, read);
        }
        Body = Encoding.UTF8.GetString(body.ToArray());
        string contentType = (context.Request.ContentType ?? "").Split(';')[0].Trim();
        if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            Form = ParseFields(Body);
        else if (contentType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new RequestInputException(415, "File uploads are not supported. Use a normal HTML form.");
        // For JSON or another text format, the student's code can read Body.
    }
    public void Text(string text, int statusCode = 200) => Send(text, "text/plain; charset=utf-8", statusCode);
    public void Html(string html, int statusCode = 200) => Send(html, "text/html; charset=utf-8", statusCode);
    public void Json(object? value, int statusCode = 200) => Send(JsonSerializer.Serialize(value, JsonOptions), "application/json; charset=utf-8", statusCode);
    public void Redirect(string url)
    {
        if (responded) return;
        if (!url.StartsWith('/') || url.StartsWith("//") || url.Contains('\\') || url.Contains('\r') || url.Contains('\n'))
            throw new ArgumentException("Use a local URL such as /students.", nameof(url));
        context.Response.Headers["Location"] = url;
        Send("", "text/plain; charset=utf-8", 303);
    }
    // Relative to website. Cannot read files outside website.
    public void File(string relativePath)
    {
        if (!TryFile(relativePath))
            Text("File not found.", 404);
    }

    // A missing website file is not an error: the request can reach HandleRequest.
    internal bool TryServeWebsite()
    {
        if (Method != "GET" && Method != "HEAD")
            return false;
        return TryFile(Path);
    }

    private bool TryFile(string relativePath)
    {
        if (responded) return true;
        string? filename = ResolvePublicFile(relativePath);
        if (filename == null) return false;
        // Open before marking the response as sent, so a failed open can become 500.
        using FileStream stream = System.IO.File.OpenRead(filename);
        responded = true;
        try
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = MimeTypes[System.IO.Path.GetExtension(filename)];
            SetHeaders();
            context.Response.ContentLength64 = stream.Length;
            if (Method != "HEAD") stream.CopyTo(context.Response.OutputStream);
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException)
        {
            // A browser may navigate away before the response finishes.
        }
        finally { CloseResponse(); }
        return true;
    }
    private void Send(string text, string contentType, int statusCode)
    {
        if (responded) return;
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        responded = true;
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            SetHeaders();
            context.Response.ContentLength64 = bytes.Length;
            if (Method != "HEAD") context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException) { }
        finally { CloseResponse(); }
    }
    private void SetHeaders()
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.KeepAlive = false;
    }

    private void CloseResponse()
    {
        try { context.Response.Close(); }
        catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException) { }
    }
    private static NameValueCollection ParseFields(string encoded)
    {
        NameValueCollection fields = new NameValueCollection();
        foreach (string item in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = item.IndexOf('=');
            string key = equals < 0 ? item : item[..equals];
            string value = equals < 0 ? "" : item[(equals + 1)..];
            fields.Add(WebUtility.UrlDecode(key), WebUtility.UrlDecode(value));
        }
        return fields;
    }
    private string? ResolvePublicFile(string requested)
    {
        // Reject parent paths, Windows paths, hidden files and links/junctions.
        if (requested.Contains('\\') || requested.Contains(':') || requested.Contains('\0')) return null;
        string[] parts = requested.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part.StartsWith('.') || part.EndsWith(' ') || part.EndsWith('.'))) return null;
        string filename = websiteRoot;
        if ((System.IO.File.GetAttributes(websiteRoot) & FileAttributes.ReparsePoint) != 0) return null;
        foreach (string part in parts)
        {
            filename = System.IO.Path.Combine(filename, part);
            if ((System.IO.File.Exists(filename) || Directory.Exists(filename)) &&
                (System.IO.File.GetAttributes(filename) & FileAttributes.ReparsePoint) != 0) return null;
        }

        if (Directory.Exists(filename))
        {
            string index = System.IO.Path.Combine(filename, "index.html");
            if (System.IO.File.Exists(index))
                filename = index;
            else if (parts.Length > 0 && !System.IO.Path.HasExtension(filename))
                filename += ".html";
            else
                return null;
        }
        else if (!System.IO.File.Exists(filename) && !System.IO.Path.HasExtension(filename))
        {
            // /login resolves to website/login.html if no exact file exists.
            filename += ".html";
        }

        if (!System.IO.File.Exists(filename)) return null;
        if ((System.IO.File.GetAttributes(filename) & FileAttributes.ReparsePoint) != 0) return null;
        return MimeTypes.ContainsKey(System.IO.Path.GetExtension(filename)) ? filename : null;
    }
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8", [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8", [".json"] = "application/json; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8", [".png"] = "image/png", [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp", [".avif"] = "image/avif", [".ico"] = "image/x-icon",
        [".pdf"] = "application/pdf", [".woff"] = "font/woff", [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf", [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav",
        [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".wasm"] = "application/wasm"
    };
    public void Dispose()
    {
        if (responded) return;
        Text("No handler for this URL and method.", 404);
    }
}
internal sealed class RequestInputException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}