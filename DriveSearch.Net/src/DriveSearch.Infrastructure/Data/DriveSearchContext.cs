using DriveSearch.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace DriveSearch.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for Drive Search.
/// Handles SQLite with sqlite-vec extension for vector operations and FTS5 for full-text search.
/// </summary>
public class DriveSearchContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Embedding> Embeddings => Set<Embedding>();

    public DriveSearchContext(DbContextOptions<DriveSearchContext> options) : base(options)
    {
        _dbPath = Database.GetConnectionString() ?? "Data Source=data/documents.db";
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite(_dbPath);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Document entity
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileId)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(e => e.FileId)
                .IsUnique();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.MimeType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Text)
                .HasColumnType("TEXT");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("TEXT");

            entity.Property(e => e.ModifiedAt)
                .HasColumnType("TEXT");

            entity.Property(e => e.IndexedAt)
                .HasColumnType("TEXT");

            entity.Property(e => e.FolderPath)
                .HasMaxLength(1000);
        });

        // Configure Embedding entity
        modelBuilder.Entity<Embedding>(entity =>
        {
            entity.ToTable("embeddings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Vector)
                .IsRequired()
                .HasColumnType("BLOB");

            entity.HasIndex(e => e.DocumentId)
                .IsUnique();

            // One-to-one relationship: Document <-> Embedding
            entity.HasOne(e => e.Document)
                .WithOne(d => d.Embedding)
                .HasForeignKey<Embedding>(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Note: FTS5 virtual table is NOT managed by EF Core migrations
        // It must be created manually via raw SQL after initial migration
        // See: InitializeFtsTableAsync()
    }

    /// <summary>
    /// Adds columns that were introduced after the initial schema creation.
    /// Safe to call on existing databases — errors are silently ignored.
    /// </summary>
    public async Task MigrateSchemaAsync()
    {
        try { await Database.ExecuteSqlRawAsync("ALTER TABLE documents ADD COLUMN FolderPath TEXT"); } catch { }
        await Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS sync_state (key TEXT PRIMARY KEY, value TEXT)");
    }

    public async Task UpdateLastSyncAtAsync(DateTime syncTime)
    {
        await Database.ExecuteSqlRawAsync(
            "INSERT OR REPLACE INTO sync_state (key, value) VALUES ('last_sync_at', {0})",
            syncTime.ToString("O"));
    }

    public async Task<DateTime?> GetLastSyncAtAsync()
    {
        var rows = await Database
            .SqlQueryRaw<string>("SELECT value FROM sync_state WHERE key = 'last_sync_at'")
            .ToListAsync();
        if (rows.Count == 0) return null;
        return DateTime.TryParse(rows[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt : null;
    }

    /// <summary>
    /// Initializes the FTS5 virtual table for full-text search.
    /// This must be called after the database is created.
    /// </summary>
    public async Task InitializeFtsTableAsync()
    {
        // Create FTS5 virtual table if it doesn't exist
        await Database.ExecuteSqlRawAsync(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS documents_fts
            USING fts5(name, text, content='documents', content_rowid='id', tokenize='unicode61')
        ");

        // Create triggers to keep FTS table in sync with documents table
        await Database.ExecuteSqlRawAsync(@"
            CREATE TRIGGER IF NOT EXISTS documents_fts_insert
            AFTER INSERT ON documents
            BEGIN
                INSERT INTO documents_fts(rowid, name, text)
                VALUES (new.id, new.name, new.text);
            END
        ");

        await Database.ExecuteSqlRawAsync(@"
            CREATE TRIGGER IF NOT EXISTS documents_fts_update
            AFTER UPDATE ON documents
            BEGIN
                UPDATE documents_fts
                SET name = new.name, text = new.text
                WHERE rowid = new.id;
            END
        ");

        await Database.ExecuteSqlRawAsync(@"
            CREATE TRIGGER IF NOT EXISTS documents_fts_delete
            AFTER DELETE ON documents
            BEGIN
                DELETE FROM documents_fts WHERE rowid = old.id;
            END
        ");
    }

    /// <summary>
    /// Gets a SQLite connection with sqlite-vec extension loaded.
    /// Use this for vector search operations.
    /// </summary>
    public async Task<SqliteConnection> GetVectorConnectionAsync()
    {
        var connection = new SqliteConnection(_dbPath);
        await connection.OpenAsync();

        try
        {
            // Attempt to load sqlite-vec extension
            // Platform-specific path resolution
            var extensionPath = GetSqliteVecExtensionPath();
            connection.LoadExtension(extensionPath);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - vector search will fail gracefully
            Console.WriteLine($"Warning: Could not load sqlite-vec extension: {ex.Message}");
            Console.WriteLine("Vector search will not be available.");
        }

        return connection;
    }

    /// <summary>
    /// Gets the platform-specific path to the sqlite-vec extension.
    /// </summary>
    private static string GetSqliteVecExtensionPath()
    {
        var platform = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => "win",
            PlatformID.Unix => "linux",
            PlatformID.MacOSX => "macos",
            _ => throw new PlatformNotSupportedException("Unsupported platform for sqlite-vec")
        };

        var arch = Environment.Is64BitProcess ? "x64" : "x86";
        var filename = platform == "win" ? "vec0.dll" : "vec0.so";

        // Get the solution root (5 levels up from bin/Debug/net10.0)
        var solutionRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../.."));

        // Look for extension in several locations
        var possiblePaths = new[]
        {
            Path.Combine(solutionRoot, "native", platform, arch, filename),
            Path.Combine("native", platform, arch, filename),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", platform, arch, filename),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename),
            filename // Fallback: assume it's in PATH or same directory
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Found sqlite-vec extension at: {path}");
                return path;
            }
        }

        // If not found, return the default path and let it fail with a clear error
        Console.WriteLine($"Warning: sqlite-vec extension not found. Tried: {string.Join(", ", possiblePaths)}");
        return possiblePaths[0];
    }
}
