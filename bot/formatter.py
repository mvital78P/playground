"""
Ergebnisse für Telegram formatieren (HTML parse_mode).
"""

MIME_ICONS = {
    "application/pdf": "📄",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "📝",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "📊",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation": "📋",
    "application/vnd.google-apps.document": "📝",
    "application/vnd.google-apps.spreadsheet": "📊",
    "application/vnd.google-apps.presentation": "📋",
}


def _e(text: str) -> str:
    """HTML-Sonderzeichen escapen."""
    return str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def format_search_results(results: list[dict], query: str) -> str:
    if not results:
        return f'🔍 Keine Ergebnisse für "<b>{_e(query)}</b>"'

    lines = [f'🔍 Ergebnisse für "<b>{_e(query)}</b>":\n']
    for i, r in enumerate(results, 1):
        icon = MIME_ICONS.get(r["mime_type"], "📄")
        date = r["modified_at"][:10] if r.get("modified_at") else "—"
        source_tag = "🧠" if r["source"] == "vector" else "🔤"
        preview = _e(r["preview"].strip()[:120])

        lines.append(
            f'{i}. {source_tag} {icon} <b>{_e(r["name"])}</b>\n'
            f'   📅 {date}\n'
            f'   <i>{preview}</i>\n'
            f'   /dl_{r["id"]}'
        )

    lines.append("\n<i>🧠 = semantisch · 🔤 = Volltext</i>")
    return "\n".join(lines)


def format_status(stats: dict) -> str:
    total = stats.get("total", 0)
    last_sync = stats.get("last_sync") or "noch kein Sync"
    if last_sync and len(last_sync) > 19:
        last_sync = last_sync[:19].replace("T", " ")
    return (
        f"📊 <b>Status</b>\n\n"
        f"📁 Indexierte Dokumente: <b>{total}</b>\n"
        f"🕐 Letzter Sync: <code>{_e(last_sync)}</code>"
    )


def format_recent(docs: list[dict]) -> str:
    if not docs:
        return "📂 Noch keine Dokumente indexiert."

    lines = ["📂 <b>Zuletzt indexierte Dokumente:</b>\n"]
    for doc in docs:
        icon = MIME_ICONS.get(doc["mime_type"], "📄")
        date = doc["indexed_at"][:10] if doc.get("indexed_at") else "—"
        lines.append(
            f'{icon} <b>{_e(doc["name"])}</b>  <i>{date}</i>\n'
            f'   /dl_{doc["id"]}'
        )

    return "\n".join(lines)
