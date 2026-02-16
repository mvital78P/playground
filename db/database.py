import sqlite3
from pathlib import Path
import sqlite_vec

import config


def _plain_connection() -> sqlite3.Connection:
    """Verbindung ohne sqlite_vec — für normale SQL + FTS5 Operationen."""
    Path(config.DB_PATH).parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(config.DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn


def get_connection() -> sqlite3.Connection:
    """Verbindung mit sqlite_vec — nur für Vektor-Operationen."""
    conn = _plain_connection()
    conn.enable_load_extension(True)
    sqlite_vec.load(conn)
    conn.enable_load_extension(False)
    return conn


def init_db():
    conn = _plain_connection()
    conn.execute("""
        CREATE TABLE IF NOT EXISTS documents (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            file_id     TEXT UNIQUE NOT NULL,
            name        TEXT NOT NULL,
            mime_type   TEXT NOT NULL,
            size        INTEGER DEFAULT 0,
            created_at  TEXT,
            modified_at TEXT,
            text        TEXT,
            indexed_at  TEXT
        )
    """)
    conn.execute("""
        CREATE VIRTUAL TABLE IF NOT EXISTS documents_fts
        USING fts5(name, text, tokenize='unicode61')
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS embeddings (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            document_id INTEGER UNIQUE NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
            vector      BLOB NOT NULL
        )
    """)
    conn.commit()
    conn.close()


def upsert_document(file_id: str, name: str, mime_type: str, size: int,
                    created_at: str, modified_at: str, text: str) -> int:
    conn = _plain_connection()
    with conn:
        conn.execute("""
            INSERT INTO documents (file_id, name, mime_type, size, created_at, modified_at, text, indexed_at)
            VALUES (?, ?, ?, ?, ?, ?, ?, datetime('now'))
            ON CONFLICT(file_id) DO UPDATE SET
                name        = excluded.name,
                mime_type   = excluded.mime_type,
                size        = excluded.size,
                modified_at = excluded.modified_at,
                text        = excluded.text,
                indexed_at  = datetime('now')
        """, (file_id, name, mime_type, size, created_at, modified_at, text))

        row = conn.execute("SELECT id FROM documents WHERE file_id = ?", (file_id,)).fetchone()
        doc_id = row["id"]

        # FTS sync
        conn.execute("DELETE FROM documents_fts WHERE rowid = ?", (doc_id,))
        conn.execute("INSERT INTO documents_fts(rowid, name, text) VALUES (?, ?, ?)",
                     (doc_id, name, text or ""))
        conn.commit()
    conn.close()
    return doc_id


def delete_document(file_id: str):
    conn = _plain_connection()
    with conn:
        conn.execute("DELETE FROM documents WHERE file_id = ?", (file_id,))
    conn.close()


def get_all_file_ids() -> set[str]:
    conn = _plain_connection()
    rows = conn.execute("SELECT file_id FROM documents").fetchall()
    conn.close()
    return {r["file_id"] for r in rows}


def get_document_by_file_id(file_id: str) -> sqlite3.Row | None:
    conn = _plain_connection()
    row = conn.execute("SELECT * FROM documents WHERE file_id = ?", (file_id,)).fetchone()
    conn.close()
    return row


def get_stats() -> dict:
    conn = _plain_connection()
    total = conn.execute("SELECT COUNT(*) as n FROM documents").fetchone()["n"]
    last_sync = conn.execute("SELECT MAX(indexed_at) as t FROM documents").fetchone()["t"]
    conn.close()
    return {"total": total, "last_sync": last_sync}
