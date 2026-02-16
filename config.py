import os
from dotenv import load_dotenv

load_dotenv()

# Google Drive
GOOGLE_CREDENTIALS_FILE = os.getenv("GOOGLE_CREDENTIALS_FILE", "credentials.json")
GOOGLE_TOKEN_FILE = "token.json"
GOOGLE_SCOPES = [
    "https://www.googleapis.com/auth/drive.readonly",
]

# Gemini
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")

# Telegram
TELEGRAM_BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN")
_raw_user_id = os.getenv("TELEGRAM_ALLOWED_USER_ID", "0")
TELEGRAM_ALLOWED_USER_ID = int(_raw_user_id) if _raw_user_id.isdigit() else 0

# Embeddings
EMBEDDING_PROVIDER = os.getenv("EMBEDDING_PROVIDER", "gemini")  # "gemini" or "ollama"
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
OLLAMA_EMBED_MODEL = os.getenv("OLLAMA_EMBED_MODEL", "nomic-embed-text")

# Sync
SYNC_INTERVAL_MINUTES = int(os.getenv("SYNC_INTERVAL_MINUTES", "5"))

# Database
DB_PATH = os.getenv("DB_PATH", "data/documents.db")
DOWNLOAD_PATH = os.getenv("DOWNLOAD_PATH", "data/downloads")

# Supported file types
SUPPORTED_MIME_TYPES = {
    "application/pdf": "pdf",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "docx",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "xlsx",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation": "pptx",
    "application/vnd.google-apps.document": "gdoc",
    "application/vnd.google-apps.spreadsheet": "gsheet",
    "application/vnd.google-apps.presentation": "gslides",
}
