"""
Dokumente ohne extrahierten Text neu indexieren (Dateiname als Embedding-Fallback).
"""
from db.database import _plain_connection
from db.embeddings import embed_and_save

conn = _plain_connection()
rows = conn.execute(
    "SELECT id, name FROM documents WHERE text IS NULL OR text = ''"
).fetchall()
conn.close()

print(f"{len(rows)} Dokumente ohne Text werden neu indexiert...")
for row in rows:
    embed_and_save(row["id"], "", name=row["name"])
    print(f"  {row['name']}")

print("Fertig.")
