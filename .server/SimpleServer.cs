using System.Net;

namespace Classroom;

// Supplied helper. The class deliberately processes one request at a time.
[System.Diagnostics.DebuggerNonUserCode]
internal sealed class SimpleServer : IDisposable
{
    private readonly HttpListener listener = new HttpListener();
    private readonly string websiteRoot;
    public string DatabasePath { get; }
    public string ProjectRoot { get; }

    public SimpleServer(int port = 8080)
    {
        string root = FindProjectRoot();
        ProjectRoot = root;
        websiteRoot = Path.Combine(root, "website");
        Directory.CreateDirectory(websiteRoot);
        DatabasePath = Path.Combine(root, "data", "database.db");
        string address = $"http://localhost:{port}/";
        listener.Prefixes.Add(address);
        try
        {
            listener.Start();
        }
        catch
        {
            listener.Close();
            throw;
        }
        Console.WriteLine($"Open {address}");
        Console.WriteLine($"Website: {websiteRoot}");
        Console.WriteLine($"Database: {DatabasePath}");
        Console.WriteLine("Press Ctrl+C to stop.\n");
    }

    public Request WaitForRequest()
    {
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            Request request = new Request(context, websiteRoot);
            try
            {
                request.ReadInput();
                Console.WriteLine($"{request.Method} {request.Path}");
                return request;
            }
            catch (RequestInputException ex)
            {
                request.Text(ex.Message, ex.StatusCode);
                request.Dispose();
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or ArgumentException)
            {
                request.Text("Invalid request.", 400);
                request.Dispose();
            }
        }
    }

    private static string FindProjectRoot()
    {
        // Visual Studio runs in bin/Debug/net10.0. Walk up to the source project
        // so edits to website are visible on refresh and the DB survives rebuilds.
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudentServer.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        // A published application uses website/data beside its executable.
        return AppContext.BaseDirectory;
    }

    public void Dispose() => listener.Close();
}
