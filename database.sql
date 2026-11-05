-- Runs automatically once each time you start the program.
-- Add your CREATE TABLE statements here. Existing rows are kept.
CREATE TABLE IF NOT EXISTS Students (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Age INTEGER NOT NULL
);
