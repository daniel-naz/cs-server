using System.Data;
using Microsoft.Data.Sqlite;

namespace Classroom;

// Supplied helper. Students can use this without changing it.
[System.Diagnostics.DebuggerNonUserCode]
public sealed class Database : IDisposable
{
    private readonly SqliteConnection connection;

    public Database(string filename)
    {
        string fullPath = Path.GetFullPath(filename);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true
        }.ToString());
        connection.Open();
    }

    // CREATE TABLE, INSERT, UPDATE, DELETE. Returns the number of affected rows.
    public int Execute(string sql, params object?[] values)
    {
        using SqliteCommand command = CreateCommand(sql, values);
        return command.ExecuteNonQuery();
    }

    // SELECT. Access results with table.Rows and row["ColumnName"].
    public DataTable Query(string sql, params object?[] values)
    {
        using SqliteCommand command = CreateCommand(sql, values);
        using SqliteDataReader reader = command.ExecuteReader();
        DataTable table = new DataTable();
        table.Load(reader);
        return table;
    }

    // One value, e.g. SELECT COUNT(*) FROM Students.
    public object? Scalar(string sql, params object?[] values)
    {
        using SqliteCommand command = CreateCommand(sql, values);
        object? result = command.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    private SqliteCommand CreateCommand(string sql, object?[] values)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        for (int i = 0; i < values.Length; i++)
            command.Parameters.AddWithValue("@p" + i, values[i] ?? DBNull.Value);
        return command;
    }

    public void Dispose() => connection.Dispose();
}
