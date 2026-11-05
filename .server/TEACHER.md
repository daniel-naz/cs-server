# Teacher notes

The student-facing `Program.cs` contains just `HandleRequest(Request request, Database db)`.
There is no Main, server setup, listening loop, connection lifetime, or try/catch for
students to manage. Use `return` to finish a request; it does not stop the server.

`.server/Startup.cs` is the actual entry point. `ServerHost` wraps startup, opening
SQLite, running `database.sql`, the request loop, exception handling, and cleanup.
`Request` provides input parsing and responses, including automatic static files.

All infrastructure sources are compiled explicitly with `Visible=false` so they do
not appear in normal Visual Studio Solution Explorer. The folder has a dot prefix
and the Windows build/start script applies its Hidden attribute. Teachers can reveal
hidden files in File Explorer. This is for reducing clutter, not access control.
Visual Studio's default Just My Code debugging skips helper methods annotated with
`DebuggerNonUserCode`; breakpoints in `HandleRequest` still work normally.

The schema file is copied for builds and publishing. It runs once per process start.
`CREATE TABLE IF NOT EXISTS` preserves data; it does not change an existing table's
columns. For classroom resets, stop the server, delete `data/school.db`, and restart.
Do not put the database or schema in `website`.

To change the port, pass it in Startup: `ServerHost.Run(global::Program.HandleRequest, 8081)`.
To investigate the helper code, reveal `.server`; no separate helper project or DLL
needs to be rebuilt or installed.

This is a local classroom starter: localhost only, one request at a time, 1 MiB body
limit. No login/session system, uploads, HTTPS, range streaming, or deployment setup.
Normal POST forms use `application/x-www-form-urlencoded`. JSON is available as raw
UTF-8 through `request.Body`. Missing query/form fields are null; repeated keys are
available using `GetValues`.

On Windows, if HttpListener reports Access is denied, ask IT to reserve the exact
localhost URL for the student's account. An administrator can run
`netsh http add urlacl url=http://localhost:8080/ user=DOMAIN\Username` with the real
account name. Port conflicts usually mean another copy is running.

References:
- https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items
- https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener
- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/parameters
