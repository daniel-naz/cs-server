namespace Classroom;

// One wrapper owns all server infrastructure. Student code only sees Request/Database.
[System.Diagnostics.DebuggerNonUserCode]
internal static class ServerHost
{
    public static void Run(Action<Request, Database> handleRequest, int port = 8080)
    {
        using SimpleServer server = new SimpleServer(port);
        using Database db = new Database(server.DatabasePath);
        // Student-editable SQL is run once per launch, before handling requests.
        string schemaPath = Path.Combine(server.ProjectRoot, "database.sql");
        if (File.Exists(schemaPath))
        {
            string sql = File.ReadAllText(schemaPath);
            if (!string.IsNullOrWhiteSpace(sql)) db.Execute(sql);
        }
        while (true)
        {
            using Request request = server.WaitForRequest();
            try
            {
                // Serve a matching website file before running student code.
                if (!request.TryServeWebsite())
                    handleRequest(request, db);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                request.Text("Server error. Check the console for details.", 500);
            }
            // Request.Dispose sends 404 when no response was sent.
        }
    }
}
