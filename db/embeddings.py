import struct
import sqlite3
import sqlite_vec
import requests

import config
from db.database import get_connection

EMBEDDING_DIM = 3072  # gemini-embedding-001


def _embed_gemini(text: str) -> list[float]:
    url = (
        "https://generativelanguage.googleapis.com/v1beta/models/"
        f"gemini-embedding-001:embedContent?key={config.GEMINI_API_KEY}"
    )
    payload = {
        "model": "models/gemini-embedding-001",
        "content": {"parts": [{"text": text}]},
        "taskType": "RETRIEVAL_DOCUMENT",
    }
    response = requests.post(url, json=payload, timeout=30)
    response.raise_for_status()
    return response.json()["embedding"]["values"]


def _embed_ollama(text: str) -> list[float]:
    response = requests.post(
        f"{config.OLLAMA_HOST}/api/embeddings",
        json={"model": config.OLLAMA_EMBED_MODEL, "prompt": text},
        timeout=30,
    )
    response.raise_for_status()
    return response.json()["embedding"]


def get_embedding(text: str) -> list[float]:
    if not text or not text.strip():
        return [0.0] * EMBEDDING_DIM

    # Auf max 8000 Zeichen kürzen (Token-Limit)
    text = text[:8000]

    if config.EMBEDDING_PROVIDER == "ollama":
        return _embed_ollama(text)
    return _embed_gemini(text)


def serialize(vector: list[float]) -> bytes:
    return struct.pack(f"{len(vector)}f", *vector)


def deserialize(blob: bytes) -> list[float]:
    n = len(blob) // 4
    return list(struct.unpack(f"{n}f", blob))


def save_embedding(document_id: int, vector: list[float]):
    conn = get_connection()
    blob = serialize(vector)
    with conn:
        conn.execute("""
            INSERT INTO embeddings (document_id, vector)
            VALUES (?, ?)
            ON CONFLICT(document_id) DO UPDATE SET vector = excluded.vector
        """, (document_id, blob))
    conn.close()


def embed_and_save(document_id: int, text: str):
    vector = get_embedding(text)
    save_embedding(document_id, vector)
