"""
MCP Server Entry Point.
Starten: python drive_mcp/server.py
"""
import json
import sys
import os

# Projektverzeichnis zum Python-Pfad hinzufügen
project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, project_root)

from mcp.server.fastmcp import FastMCP
from drive_mcp.tools import search_documents, get_document, list_documents, get_db_stats

mcp = FastMCP("drive-search")


@mcp.tool()
def search_docs(query: str, limit: int = 5) -> str:
    """
    Google Drive Dokumente semantisch und per Volltext durchsuchen.
    Gibt die relevantesten Dokumente mit Vorschau zurück.

    Args:
        query: Suchanfrage auf Deutsch oder Englisch
        limit: Maximale Anzahl Ergebnisse (Standard: 5)
    """
    result = search_documents(query, limit=limit)
    return json.dumps(result, ensure_ascii=False, indent=2)


@mcp.tool()
def get_doc(document_id: int) -> str:
    """
    Vollständigen Inhalt eines Dokuments anhand seiner ID abrufen.
    Die ID kommt aus den Suchergebnissen von search_docs.

    Args:
        document_id: Die numerische ID des Dokuments
    """
    result = get_document(document_id)
    return json.dumps(result, ensure_ascii=False, indent=2)


@mcp.tool()
def list_docs(limit: int = 20, offset: int = 0, mime_filter: str = "") -> str:
    """
    Alle indexierten Google Drive Dokumente auflisten.

    Args:
        limit: Anzahl Dokumente pro Seite (Standard: 20)
        offset: Seiten-Offset für Paginierung
        mime_filter: Optionaler Filter, z.B. 'pdf', 'word', 'spreadsheet'
    """
    result = list_documents(limit=limit, offset=offset, mime_filter=mime_filter)
    return json.dumps(result, ensure_ascii=False, indent=2)


@mcp.tool()
def drive_status() -> str:
    """
    Statistik der indizierten Dokumente anzeigen (Anzahl, letzter Sync).
    """
    result = get_db_stats()
    return json.dumps(result, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    mcp.run()
