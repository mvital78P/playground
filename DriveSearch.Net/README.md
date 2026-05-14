# DriveSearch.Net

A comprehensive C# port of the Python-based Google Drive search system with semantic search, full-text search, RAG (Retrieval-Augmented Generation), and multi-provider LLM support.

## Features

- **Google Drive Synchronization**: Automated sync with OAuth2 authentication
- **Hybrid Search**: Combines semantic vector search (sqlite-vec) with full-text search (FTS5)
- **RAG (Retrieval-Augmented Generation)**: Ask questions and get AI-generated answers from your documents
- **Multi-Provider Support**:
  - LLM: Gemini, Claude (Anthropic), Ollama, LM Studio
  - Embeddings: Gemini, Ollama, LM Studio
- **Telegram Bot**: Interactive bot with 7 commands
- **Web Dashboard**: Modern web UI for configuration management and service control
- **Background Sync Service**: Automated periodic synchronization

## Architecture

Built using Clean Architecture principles:

```
DriveSearch.Net/
├── src/
│   ├── DriveSearch.Core/              # Domain models & interfaces
│   ├── DriveSearch.Infrastructure/    # Data access, Google APIs, LLM providers
│   ├── DriveSearch.Application/       # Business logic & services
│   ├── DriveSearch.TelegramBot/       # Telegram bot console app
│   ├── DriveSearch.SyncService/       # Background sync service
│   └── DriveSearch.Dashboard/         # ASP.NET Core Web Dashboard
└── DriveSearch.sln
```

## Prerequisites

- **.NET 10.0 SDK** or later
- **Google Drive API credentials** (credentials.json)
- **SQLite** with sqlite-vec extension support
- **API Keys** (at least one):
  - Gemini API key (recommended)
  - Anthropic API key (for Claude)
  - Local LLM server (Ollama or LM Studio)

## Setup

### 1. Clone and Build

```bash
cd DriveSearch.Net
dotnet restore
dotnet build
```

### 2. Google Drive API Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google Drive API
4. Create OAuth 2.0 credentials (Desktop app)
5. Download the credentials file as `credentials.json`
6. Place `credentials.json` in the root directory or specify path in configuration

### 3. Configuration

Create a `.env` file in the root directory:

```env
# Google Drive
GOOGLE_CREDENTIALS_FILE=credentials.json
GOOGLE_TOKEN_FILE=token.json

# Database
DATABASE_PATH=data/documents.db

# LLM Provider (gemini, claude, ollama, lmstudio)
LLM_PROVIDER=gemini
GEMINI_API_KEY=your_gemini_api_key_here
ANTHROPIC_API_KEY=your_claude_api_key_here

# Embedding Provider (gemini, ollama, lmstudio)
EMBEDDINGS_PROVIDER=gemini

# Ollama (if using local LLM)
OLLAMA_URL=http://localhost:11434
OLLAMA_MODEL=llama3.2
OLLAMA_EMBEDDING_MODEL=nomic-embed-text
OLLAMA_DIMENSIONS=768

# LM Studio (if using local LLM)
LMSTUDIO_URL=http://localhost:1234/v1
LMSTUDIO_MODEL=local-model
LMSTUDIO_EMBEDDING_MODEL=text-embedding-nomic-embed-text-v1.5
LMSTUDIO_DIMENSIONS=768

# Telegram Bot
TELEGRAM_BOT_TOKEN=your_bot_token_here
TELEGRAM_ALLOWED_USER_ID=your_telegram_user_id

# Sync Service
SYNC_INTERVAL_MINUTES=5
```

Each service also has an `appsettings.json` file that can override these settings.

### 4. First Run - OAuth Authentication

Run any service for the first time to authenticate with Google:

```bash
cd src/DriveSearch.SyncService
dotnet run
```

This will:
1. Open a browser window for Google OAuth
2. Ask you to grant permissions
3. Save the token to `token.json`

## Running the Services

### Sync Service (Background Synchronization)

Automatically syncs Google Drive files on a configurable interval:

```bash
cd src/DriveSearch.SyncService
dotnet run
```

Configuration in `appsettings.json`:
```json
{
  "Sync": {
    "IntervalMinutes": 5
  }
}
```

### Telegram Bot

Interactive bot with search and RAG capabilities:

```bash
cd src/DriveSearch.TelegramBot
dotnet run
```

**Available Commands:**
- `/start` - Welcome message and help
- `/help` - Show all commands
- `/search <query>` - Search documents (hybrid search)
- `/ask <question>` - Ask a question (RAG with AI answer)
- `/status` - Show system statistics
- `/recent [limit]` - Show recently indexed documents
- `/download <doc_id>` - Get download link for a document

**Security:** Only the user ID specified in `TELEGRAM_ALLOWED_USER_ID` can use the bot.

### Web Dashboard

Modern web interface for configuration and service management:

```bash
cd src/DriveSearch.Dashboard
dotnet run
```

Then open `http://localhost:5000` in your browser.

**Features:**
- View service status
- Edit configuration in real-time
- Manage services (start/stop/restart)
- View system information

## Usage Examples

### Search Documents

The hybrid search combines:
- **Vector Search**: Semantic similarity using embeddings (cosine distance < 0.40)
- **Full-Text Search**: Keyword matching with FTS5

Priority order:
1. Documents found by both methods (highest priority)
2. Documents found by FTS only
3. Documents found by vector search only

### RAG (Ask Questions)

```
User: What is the company's vacation policy?

System:
1. Generates embedding for the question
2. Searches for relevant documents
3. Extracts relevant snippets
4. Sends to LLM with context
5. Returns AI-generated answer with source citations
```

## Supported File Types

- **PDF** (via PdfPig)
- **Microsoft Word** (.docx via OpenXML SDK)
- **Microsoft Excel** (.xlsx via OpenXML SDK)
- **Microsoft PowerPoint** (.pptx via OpenXML SDK)
- **Google Docs** (exported as PDF)
- **Google Sheets** (exported as PDF)
- **Google Slides** (exported as PDF)
- **Plain text** (.txt)

## Database Schema

### SQLite with Extensions

The system uses SQLite with two extensions:
- **sqlite-vec**: Vector similarity search
- **FTS5**: Full-text search

### Tables

**documents**
```sql
CREATE TABLE documents (
    id INTEGER PRIMARY KEY,
    file_id TEXT UNIQUE NOT NULL,
    name TEXT,
    mime_type TEXT,
    text TEXT,
    modified_at TEXT,
    indexed_at TEXT
);
```

**embeddings**
```sql
CREATE TABLE embeddings (
    id INTEGER PRIMARY KEY,
    document_id INTEGER UNIQUE REFERENCES documents(id),
    vector BLOB NOT NULL
);
```

**documents_fts** (FTS5 virtual table)
```sql
CREATE VIRTUAL TABLE documents_fts USING fts5(name, text);
```

## Development

### Building from Source

```bash
# Restore packages
dotnet restore

# Build all projects
dotnet build

# Build specific project
dotnet build src/DriveSearch.TelegramBot/DriveSearch.TelegramBot.csproj

# Run tests (if any)
dotnet test
```

### Project Dependencies

- **Core**: No dependencies (pure domain models)
- **Infrastructure**: Depends on Core
- **Application**: Depends on Core, Infrastructure
- **Services**: Depend on Core, Infrastructure, Application

### Key NuGet Packages

- `Microsoft.EntityFrameworkCore.Sqlite` (10.0.0) - Database ORM
- `Google.Apis.Drive.v3` (1.68.0+) - Google Drive API
- `Google.Apis.Auth` (1.68.0+) - OAuth2
- `Telegram.Bot` (22.10.0.1) - Telegram integration
- `PdfPig` (0.1.9+) - PDF text extraction
- `DocumentFormat.OpenXml` (3.1.0+) - Office document parsing
- `DotNetEnv` (3.2.0+) - .env file support

## Troubleshooting

### sqlite-vec Extension Not Found

The system requires the sqlite-vec extension. On Windows, place `sqlite-vec.dll` in the application directory. On Linux, use `sqlite-vec.so`.

### Google OAuth Errors

If you get OAuth errors:
1. Delete `token.json`
2. Run the service again
3. Complete the OAuth flow in the browser

### Empty Search Results

- Check if documents are indexed: Use `/status` command in Telegram or check Dashboard
- Run sync service to index documents
- Verify API keys are configured correctly

### LLM Provider Errors

- **Gemini**: Verify `GEMINI_API_KEY` is valid
- **Claude**: Verify `ANTHROPIC_API_KEY` is valid
- **Ollama**: Ensure Ollama is running on `http://localhost:11434`
- **LM Studio**: Ensure LM Studio server is running on `http://localhost:1234`

### Build Errors

Common NuGet warnings (safe to ignore):
- `NU1603`: Package version resolution (uses newer compatible version)
- `NU1510`: Transitive package trimming (optimization warning)

## Migration from Python Version

This C# version maintains compatibility with the Python version's database schema. You can:

1. Keep using the same `documents.db` file
2. Run both versions side-by-side
3. Mix and match services (e.g., C# sync + Python MCP)

**Note**: Both versions can share the same database, but avoid running sync simultaneously.

## Performance

- **Vector Search**: Cosine distance calculation in SQLite (milliseconds)
- **FTS Search**: SQLite FTS5 (very fast, milliseconds)
- **Sync**: Depends on Drive file count and sizes (incremental sync)
- **Embedding Generation**: Depends on provider (Gemini ~1-2s per document)

## License

This project is a C# port created for educational purposes to learn C# development.

## Future Enhancements

- [ ] MCP Server implementation in C# (currently Python)
- [ ] Docker containerization
- [ ] PostgreSQL support with pgvector
- [ ] Additional LLM providers (OpenAI, Azure OpenAI)
- [ ] Web search interface
- [ ] Document preview in Dashboard
- [ ] Advanced filtering and faceted search
- [ ] Multi-user support
- [ ] Rate limiting and caching

## Support

For issues, questions, or contributions, please refer to the original Python implementation or open an issue in the repository.

## Credits

- Original Python implementation
- Built with .NET 10.0
- Uses Google Drive API, Anthropic Claude, Google Gemini
- SQLite with sqlite-vec extension by Alex Garcia
