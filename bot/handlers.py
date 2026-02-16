"""
Telegram Bot Handler: /search, /download, /status, /recent
"""
import os
import logging
import tempfile

from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup
from telegram.ext import ContextTypes
from telegram.constants import ParseMode

import config
from db.search import search
from db.database import get_stats, get_document_by_file_id, _plain_connection
from bot.formatter import format_search_results, format_status, format_recent
from sync.drive_client import build_service, download_file

log = logging.getLogger(__name__)


def _is_allowed(update: Update) -> bool:
    return update.effective_user.id == config.TELEGRAM_ALLOWED_USER_ID


async def cmd_start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if not _is_allowed(update):
        return
    await update.message.reply_text(
        "👋 <b>Drive Search Bot</b>\n\n"
        "/search &lt;Begriff&gt; — Dokumente suchen\n"
        "/status — Statistik\n"
        "/recent — Zuletzt indexiert\n"
        "/help — Diese Hilfe",
        parse_mode=ParseMode.HTML,
    )


async def cmd_help(update: Update, context: ContextTypes.DEFAULT_TYPE):
    await cmd_start(update, context)


async def cmd_search(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if not _is_allowed(update):
        return

    query = " ".join(context.args).strip()
    if not query:
        await update.message.reply_text("Verwendung: /search <Suchbegriff>")
        return

    msg = await update.message.reply_text(f'Suche nach "{query}"...')

    results = search(query, limit=5)
    text = format_search_results(results, query)

    await msg.edit_text(text, parse_mode=ParseMode.HTML)


async def cmd_download(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if not _is_allowed(update):
        return

    # Kommando: /dl_<doc_id>  oder  /download <doc_id>
    text = update.message.text or ""
    doc_id = None

    if text.startswith("/dl_"):
        try:
            doc_id = int(text.split("_", 1)[1].split()[0])
        except (ValueError, IndexError):
            pass
    elif context.args:
        try:
            doc_id = int(context.args[0])
        except ValueError:
            pass

    if not doc_id:
        await update.message.reply_text("Verwendung: /dl_<ID>  (ID aus /search Ergebnissen)")
        return

    # Dokument aus DB laden
    conn = _plain_connection()
    row = conn.execute("SELECT * FROM documents WHERE id = ?", (doc_id,)).fetchone()
    conn.close()

    if not row:
        await update.message.reply_text(f"Kein Dokument mit ID {doc_id} gefunden.")
        return

    msg = await update.message.reply_text(f"⬇️ Lade {row['name']} herunter…")

    with tempfile.NamedTemporaryFile(delete=False, suffix=".tmp") as tmp:
        tmp_path = tmp.name

    try:
        service = build_service()
        success = download_file(service, row["file_id"], row["mime_type"], tmp_path)

        if not success:
            await msg.edit_text("❌ Download fehlgeschlagen.")
            return

        # Dateiname mit richtiger Endung
        ext_map = {
            "application/pdf": ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": ".xlsx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation": ".pptx",
            "application/vnd.google-apps.document": ".pdf",
            "application/vnd.google-apps.spreadsheet": ".pdf",
            "application/vnd.google-apps.presentation": ".pdf",
        }
        ext = ext_map.get(row["mime_type"], "")
        filename = row["name"] if row["name"].endswith(ext) else row["name"] + ext

        await update.message.reply_document(
            document=open(tmp_path, "rb"),
            filename=filename,
            caption=f"📄 {row['name']}",
        )
        await msg.delete()

    except Exception as e:
        log.error("Download-Fehler: %s", e)
        await msg.edit_text(f"❌ Fehler: {e}")
    finally:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)


async def cmd_status(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if not _is_allowed(update):
        return
    stats = get_stats()
    await update.message.reply_text(
        format_status(stats),
        parse_mode=ParseMode.HTML,
    )


async def cmd_recent(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if not _is_allowed(update):
        return
    conn = _plain_connection()
    rows = conn.execute(
        "SELECT id, name, mime_type, indexed_at FROM documents ORDER BY indexed_at DESC LIMIT 10"
    ).fetchall()
    conn.close()
    docs = [dict(r) for r in rows]
    await update.message.reply_text(
        format_recent(docs),
        parse_mode=ParseMode.HTML,
    )


async def handle_text(update: Update, context: ContextTypes.DEFAULT_TYPE):
    """Freitext-Nachrichten als Suche behandeln."""
    if not _is_allowed(update):
        return

    query = (update.message.text or "").strip()
    if not query:
        return

    msg = await update.message.reply_text(f'Suche nach "{query}"...')

    results = search(query, limit=5)
    text = format_search_results(results, query)

    await msg.edit_text(text, parse_mode=ParseMode.HTML)
