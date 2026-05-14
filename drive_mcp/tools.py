"""
MCP Tool-Implementierungen: search_documents, get_document, list_documents.
"""
from db.search import search as db_search, ask_documents as db_ask
from db.database import _plain_connection, get_stats


def search_documents(query: str, limit: int = 5) -> dict:
    """
    Dokumente semantisch + per Volltext suchen.
    Gibt eine Liste von Treffern mit Name, Datum, Vorschau zurück.
    """
    results = db_search(query, limit=limit)
    if not results:
        return {"query": query, "count": 0, "results": []}

    return {
        "query": query,
        "count": len(results),
        "results": [
            {
                "id": r["id"],
                "name": r["name"],
                "mime_type": r["mime_type"],
                "modified_at": r["modified_at"],
                "preview": r["preview"],
                "source": r["source"],
            }
            for r in results
        ],
    }


def get_document(document_id: int) -> dict:
    """
    Vollständigen Text eines Dokuments anhand der DB-ID abrufen.
    """
    conn = _plain_connection()
    row = conn.execute(
        "SELECT id, file_id, name, mime_type, size, created_at, modified_at, text, indexed_at "
        "FROM documents WHERE id = ?",
        (document_id,),
    ).fetchone()
    conn.close()

    if not row:
        return {"error": f"Dokument mit ID {document_id} nicht gefunden."}

    return {
        "id": row["id"],
        "file_id": row["file_id"],
        "name": row["name"],
        "mime_type": row["mime_type"],
        "size": row["size"],
        "created_at": row["created_at"],
        "modified_at": row["modified_at"],
        "indexed_at": row["indexed_at"],
        "text": row["text"] or "",
        "has_text": bool(row["text"] and row["text"].strip()),
    }


def list_documents(limit: int = 20, offset: int = 0, mime_filter: str = "") -> dict:
    """
    Alle indexierten Dokumente auflisten (mit optionalem MIME-Type Filter).
    """
    conn = _plain_connection()

    if mime_filter:
        rows = conn.execute(
            "SELECT id, name, mime_type, size, modified_at, indexed_at "
            "FROM documents WHERE mime_type LIKE ? "
            "ORDER BY modified_at DESC LIMIT ? OFFSET ?",
            (f"%{mime_filter}%", limit, offset),
        ).fetchall()
        total = conn.execute(
            "SELECT COUNT(*) as n FROM documents WHERE mime_type LIKE ?",
            (f"%{mime_filter}%",),
        ).fetchone()["n"]
    else:
        rows = conn.execute(
            "SELECT id, name, mime_type, size, modified_at, indexed_at "
            "FROM documents ORDER BY modified_at DESC LIMIT ? OFFSET ?",
            (limit, offset),
        ).fetchall()
        total = conn.execute("SELECT COUNT(*) as n FROM documents").fetchone()["n"]

    conn.close()

    return {
        "total": total,
        "limit": limit,
        "offset": offset,
        "documents": [
            {
                "id": r["id"],
                "name": r["name"],
                "mime_type": r["mime_type"],
                "size": r["size"],
                "modified_at": r["modified_at"],
                "indexed_at": r["indexed_at"],
            }
            for r in rows
        ],
    }


def ask_docs(query: str, limit: int = 5) -> dict:
    """
    Frage an die Dokumente stellen (RAG).
    Gibt Antwort und verwendete Quellen zurück.
    """
    result = db_ask(query, limit=limit)
    return {
        "query": query,
        "answer": result["answer"],
        "sources": result["sources"]
    }


def get_db_stats() -> dict:
    """Statistik der indizierten Dokumente."""
    return get_stats()
