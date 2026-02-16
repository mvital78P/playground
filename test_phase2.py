"""
Phase 2 Test-Script
Indexiert die ersten 5 Drive-Dokumente und testet die Suche.

Ausführen: python test_phase2.py
"""
import os
import tempfile

from sync.drive_client import build_service, list_all_files, download_file
from sync.extractor import extract_text
from db.database import init_db, upsert_document, get_stats
from db.embeddings import embed_and_save
from db.search import search


def main():
    print("=== Phase 2 Test: Datenbank + Embeddings ===\n")

    # 1. DB initialisieren
    print("Initialisiere Datenbank...")
    init_db()
    print("Datenbank bereit.\n")

    # 2. Erste 5 Dokumente aus Drive holen und indexieren
    print("Lade Dokumente aus Google Drive...")
    service = build_service()
    files = list_all_files(service)
    to_index = files[:5]
    print(f"{len(files)} Dokumente gefunden, indexiere die ersten {len(to_index)}.\n")

    for file in to_index:
        print(f"Verarbeite: {file['name']}")
        with tempfile.NamedTemporaryFile(delete=False, suffix=".tmp") as tmp:
            tmp_path = tmp.name

        try:
            success = download_file(service, file["id"], file["mimeType"], tmp_path)
            if not success:
                print("  Download fehlgeschlagen, überspringe.\n")
                continue

            text = extract_text(tmp_path, file["mimeType"])
            doc_id = upsert_document(
                file_id=file["id"],
                name=file["name"],
                mime_type=file["mimeType"],
                size=int(file.get("size", 0) or 0),
                created_at=file.get("createdTime"),
                modified_at=file.get("modifiedTime"),
                text=text,
            )
            embed_and_save(doc_id, text, name=file["name"])
            print(f"  Indexiert (ID: {doc_id}, {len(text)} Zeichen)\n")
        finally:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)

    # 3. Statistik
    stats = get_stats()
    print(f"DB-Status: {stats['total']} Dokumente, letzter Sync: {stats['last_sync']}\n")

    # 4. Suchtest
    query = input("Suchbegriff eingeben (z.B. 'Rechnung' oder 'Vertrag'): ").strip()
    if query:
        print(f"\nSuche nach: '{query}'\n")
        results = search(query, limit=5)
        if results:
            for i, r in enumerate(results, 1):
                print(f"{i}. [{r['source']}] {r['name']}")
                print(f"   Vorschau: {r['preview'][:150]}...")
                print()
        else:
            print("Keine Ergebnisse gefunden.")

    print("=== Test abgeschlossen ===")


if __name__ == "__main__":
    main()
