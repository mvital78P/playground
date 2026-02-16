from db.database import get_connection, _plain_connection
from db.embeddings import get_embedding, serialize, EMBEDDING_DIM


def search(query: str, limit: int = 5) -> list[dict]:
    """
    Kombinierte Suche: semantisch (Vektor) + Volltext (FTS).
    Gibt bis zu `limit` Ergebnisse zurück, sortiert nach Relevanz.
    """
    vector_results = _vector_search(query, limit)
    fts_results = _fts_search(query, limit)

    # Ergebnisse zusammenführen, Duplikate entfernen (Vektor hat Vorrang)
    seen = {r["file_id"] for r in vector_results}
    merged = vector_results[:]
    for r in fts_results:
        if r["file_id"] not in seen:
            merged.append(r)
            seen.add(r["file_id"])

    return merged[:limit]


def _vector_search(query: str, limit: int) -> list[dict]:
    try:
        query_vec = serialize(get_embedding(query, task_type="RETRIEVAL_QUERY"))
        conn = get_connection()
        rows = conn.execute("""
            SELECT d.id, d.file_id, d.name, d.mime_type, d.modified_at,
                   d.text, vec_distance_cosine(e.vector, ?) AS distance
            FROM embeddings e
            JOIN documents d ON d.id = e.document_id
            ORDER BY distance ASC
            LIMIT ?
        """, (query_vec, limit)).fetchall()
        conn.close()
        return [_row_to_dict(r, source="vector") for r in rows]
    except Exception as e:
        print(f"Vektorsuche fehlgeschlagen: {e}")
        return []


def _fts_search(query: str, limit: int) -> list[dict]:
    try:
        conn = _plain_connection()
        # Mehrere Wörter mit OR verbinden, damit Teilbegriffe gefunden werden
        fts_query = " OR ".join(f'"{w}"' for w in query.split() if w)
        rows = conn.execute("""
            SELECT d.id, d.file_id, d.name, d.mime_type, d.modified_at, d.text
            FROM documents_fts f
            JOIN documents d ON d.id = f.rowid
            WHERE documents_fts MATCH ?
            ORDER BY rank
            LIMIT ?
        """, (fts_query, limit)).fetchall()
        conn.close()
        return [_row_to_dict(r, source="fts") for r in rows]
    except Exception as e:
        print(f"FTS-Suche fehlgeschlagen: {e}")
        return []


def _row_to_dict(row, source: str) -> dict:
    text = row["text"] or ""
    return {
        "id": row["id"],
        "file_id": row["file_id"],
        "name": row["name"],
        "mime_type": row["mime_type"],
        "modified_at": row["modified_at"],
        "preview": text[:200].replace("\n", " "),
        "source": source,
    }
