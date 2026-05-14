"""
Einmaliger Sync-Durchlauf: Indexiert alle neuen/geänderten Dokumente und generiert Embeddings.
Starten: python run_sync.py
"""
import logging
from db.database import init_db
from sync.scheduler import sync_once

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)

if __name__ == "__main__":
    print("Initialisiere Datenbank...")
    init_db()
    print("Starte Sync...")
    sync_once()
    print("Sync abgeschlossen!")
