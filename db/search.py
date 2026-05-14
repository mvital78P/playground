from db.database import get_connection, _plain_connection
from db.embeddings import get_embedding, serialize, EMBEDDING_DIM
from db.llm import generate_answer

# Maximale cosine distance — alles darüber ist nicht relevant genug
MAX_DISTANCE = 0.40

# Deutsche + englische Stoppwörter die aus Suchanfragen entfernt werden
STOP_WORDS = {
    "suche", "such", "finde", "finden", "zeige", "zeig", "wo", "ist", "sind",
    "mein", "meine", "meinen", "meiner", "meinem", "dein", "deine",
    "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einem",
    "ich", "du", "er", "sie", "es", "wir", "ihr",
    "nach", "von", "für", "mit", "zu", "zum", "zur", "aus", "bei", "über",
    "und", "oder", "aber", "nicht", "auch", "noch", "schon", "nur",
    "was", "wie", "wer", "wann", "welche", "welcher", "welches",
    "hat", "haben", "bin", "habe", "gibt", "gibt's",
    "alle", "alles", "allen",
    "the", "a", "an", "is", "are", "my", "your", "find", "search", "show",
    "me", "for", "from", "with", "in", "on", "of", "to",
}


def _clean_query(query: str) -> str:
    """Stoppwörter entfernen, nur relevante Suchbegriffe behalten."""
    words = [w for w in query.split() if w.lower() not in STOP_WORDS]
    return " ".join(words) if words else query


def search(query: str, limit: int = 5) -> list[dict]:
    """
    Kombinierte Suche: semantisch (Vektor) + Volltext (FTS).
    Gibt bis zu `limit` Ergebnisse zurück, sortiert nach Relevanz.
    """
    cleaned = _clean_query(query)
    vector_results = _vector_search(cleaned, limit)
    fts_results = _fts_search(cleaned, limit)

    # Ergebnisse zusammenführen, Duplikate entfernen
    seen = set()
    merged = []
    fts_ids = {r["file_id"] for r in fts_results}

    # 1. Ergebnisse die in BEIDEN Listen sind (höchste Relevanz)
    for r in vector_results:
        if r["file_id"] in fts_ids:
            merged.append(r)
            seen.add(r["file_id"])

    # 2. Restliche FTS-Ergebnisse (exakte Textübereinstimmung)
    for r in fts_results:
        if r["file_id"] not in seen:
            merged.append(r)
            seen.add(r["file_id"])

    # 3. Vektor-only-Ergebnisse NUR wenn FTS gar nichts gefunden hat
    if not fts_results:
        for r in vector_results:
            if r["file_id"] not in seen:
                merged.append(r)
                seen.add(r["file_id"])

    return merged[:limit]


def _vector_search(query: str, limit: int) -> list[dict]:
    try:
        query_vec = serialize(get_embedding(query, task_type="RETRIEVAL_QUERY"))
        conn = get_connection()
        # Mehr Kandidaten holen, dann nach Distance filtern
        rows = conn.execute("""
            SELECT d.id, d.file_id, d.name, d.mime_type, d.modified_at,
                   d.text, vec_distance_cosine(e.vector, ?) AS distance
            FROM embeddings e
            JOIN documents d ON d.id = e.document_id
            ORDER BY distance ASC
            LIMIT ?
        """, (query_vec, limit * 3)).fetchall()
        conn.close()
        # Nur relevante Ergebnisse (distance < threshold)
        results = []
        for r in rows:
            if r["distance"] < MAX_DISTANCE:
                results.append(_row_to_dict(r, source="vector"))
        return results[:limit]
    except Exception as e:
        print(f"Vektorsuche fehlgeschlagen: {e}")
        return []


def _fts_search(query: str, limit: int) -> list[dict]:
    try:
        conn = _plain_connection()
        # Mehrere Wörter mit AND verbinden für präzisere Ergebnisse
        words = [w for w in query.split() if w]
        fts_query = " AND ".join(f'"{w}"' for w in words)
        rows = conn.execute("""
            SELECT d.id, d.file_id, d.name, d.mime_type, d.modified_at, d.text
            FROM documents_fts f
            JOIN documents d ON d.id = f.rowid
            WHERE documents_fts MATCH ?
            ORDER BY rank
            LIMIT ?
        """, (fts_query, limit)).fetchall()

        # Fallback: OR-Suche wenn AND keine Ergebnisse liefert
        if not rows and len(words) > 1:
            fts_query = " OR ".join(f'"{w}"' for w in words)
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
        "text": text,
        "source": source,
    }


def ask_documents(query: str, limit: int = 5) -> str:
    """
    Sucht nach relevanten Dokumenten und generiert eine Antwort auf die Frage.
    """
    results = search(query, limit=limit)
    if not results:
        return "Ich konnte keine Dokumente finden, die für deine Frage relevant sind."

    # Kontext für das LLM aufbauen
    context_parts = []
    for r in results:
        # Voller Text aus der DB holen (search() gibt nur preview zurück)
        conn = _plain_connection()
        row = conn.execute("SELECT text FROM documents WHERE id = ?", (r["id"],)).fetchone()
        conn.close()
        
        full_text = row["text"] if row and row["text"] else "Kein Inhalt verfügbar"
        context_parts.append(f"DOKUMENT: {r['name']} (ID: {r['id']})\nINHALT: {full_text}")

    context = "\n\n---\n\n".join(context_parts)
    
    # Antwort generieren
    return generate_answer(query, context)
