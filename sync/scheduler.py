"""
Sync-Scheduler: Polling alle N Minuten, Delta-Sync via modifiedTime.
"""
import os
import logging
import tempfile
import schedule
import time

from sync.drive_client import build_service, list_all_files, download_file
from sync.extractor import extract_text
from db.database import (
    init_db, upsert_document, delete_document,
    get_all_file_ids, get_document_by_file_id, get_stats,
)
from db.embeddings import embed_and_save
import config

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
log = logging.getLogger(__name__)


def sync_once():
    """Einen Sync-Durchlauf ausführen: neue/geänderte Dokumente indexieren,
    gelöschte aus der DB entfernen."""
    log.info("Sync gestartet...")
    try:
        service = build_service()
        drive_files = list_all_files(service)
    except Exception as e:
        log.error("Drive-Verbindung fehlgeschlagen: %s", e)
        return

    drive_ids = {f["id"] for f in drive_files}
    db_ids = get_all_file_ids()

    # Gelöschte Dateien aus DB entfernen
    removed = db_ids - drive_ids
    for file_id in removed:
        delete_document(file_id)
        log.info("Entfernt: %s", file_id)

    # Neue + geänderte Dateien verarbeiten
    new_count = 0
    updated_count = 0
    skipped_count = 0

    for file in drive_files:
        file_id = file["id"]
        modified_at = file.get("modifiedTime", "")

        # Prüfen ob Dokument neu oder geändert ist
        existing = get_document_by_file_id(file_id)
        if existing and existing["modified_at"] == modified_at:
            skipped_count += 1
            continue

        is_new = existing is None

        with tempfile.NamedTemporaryFile(delete=False, suffix=".tmp") as tmp:
            tmp_path = tmp.name

        try:
            success = download_file(service, file_id, file["mimeType"], tmp_path)
            if not success:
                log.warning("Download fehlgeschlagen: %s", file["name"])
                continue

            text = extract_text(tmp_path, file["mimeType"])
            doc_id = upsert_document(
                file_id=file_id,
                name=file["name"],
                mime_type=file["mimeType"],
                size=int(file.get("size", 0) or 0),
                created_at=file.get("createdTime"),
                modified_at=modified_at,
                text=text,
            )
            embed_and_save(doc_id, text, name=file["name"])

            if is_new:
                new_count += 1
                log.info("Neu indexiert: %s (ID: %d)", file["name"], doc_id)
            else:
                updated_count += 1
                log.info("Aktualisiert: %s (ID: %d)", file["name"], doc_id)

        except Exception as e:
            log.error("Fehler bei %s: %s", file["name"], e)
        finally:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)

    stats = get_stats()
    log.info(
        "Sync abgeschlossen — neu: %d, aktualisiert: %d, übersprungen: %d, "
        "entfernt: %d | DB gesamt: %d Dokumente",
        new_count, updated_count, skipped_count, len(removed), stats["total"],
    )


def run():
    """Scheduler starten: sofort einmal syncen, dann alle N Minuten."""
    init_db()
    log.info(
        "Scheduler gestartet (Intervall: %d Minuten)",
        config.SYNC_INTERVAL_MINUTES,
    )

    sync_once()  # Sofortiger erster Lauf

    schedule.every(config.SYNC_INTERVAL_MINUTES).minutes.do(sync_once)

    while True:
        schedule.run_pending()
        time.sleep(30)


if __name__ == "__main__":
    run()
