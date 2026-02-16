"""
Phase 1 Test-Script
Listet alle Google Drive Dokumente und extrahiert Text aus den ersten 3 Dateien.

Ausführen: python test_phase1.py
"""
import os
import tempfile
from sync.drive_client import build_service, list_all_files, download_file
from sync.extractor import extract_text
import config


def main():
    print("=== Phase 1 Test: Google Drive Verbindung ===\n")

    # 1. Verbindung herstellen (öffnet Browser für OAuth2 beim ersten Mal)
    print("Verbinde mit Google Drive...")
    service = build_service()
    print("Verbindung erfolgreich!\n")

    # 2. Alle Dateien listen
    print("Lade Dateiliste...")
    files = list_all_files(service)
    print(f"{len(files)} Dokumente gefunden:\n")

    for f in files:
        size = int(f.get("size", 0)) if f.get("size") else 0
        print(f"  [{config.SUPPORTED_MIME_TYPES.get(f['mimeType'], '?')}] {f['name']} ({size // 1024} KB)")

    # 3. Text aus ersten 3 Dateien extrahieren
    print(f"\n=== Text-Extraktion (erste 3 Dateien) ===\n")
    for file in files[:3]:
        print(f"Verarbeite: {file['name']}")
        with tempfile.NamedTemporaryFile(delete=False, suffix=".tmp") as tmp:
            tmp_path = tmp.name

        try:
            success = download_file(service, file["id"], file["mimeType"], tmp_path)
            if success:
                text = extract_text(tmp_path, file["mimeType"])
                preview = text[:300].replace("\n", " ") if text else "(kein Text extrahiert)"
                print(f"  Vorschau: {preview}...\n")
            else:
                print(f"  Download fehlgeschlagen\n")
        finally:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)

    print("=== Test abgeschlossen ===")


if __name__ == "__main__":
    main()
