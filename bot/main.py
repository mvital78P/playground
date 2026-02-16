"""
Telegram Bot Entry Point.
Starten: python -m bot.main
"""
import logging

from telegram.ext import Application, CommandHandler, MessageHandler, filters

import config
from bot.handlers import (
    cmd_start, cmd_help, cmd_search, cmd_download,
    cmd_status, cmd_recent, handle_unknown,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
log = logging.getLogger(__name__)


def main():
    if not config.TELEGRAM_BOT_TOKEN:
        raise RuntimeError("TELEGRAM_BOT_TOKEN ist nicht gesetzt (.env prüfen)")
    if not config.TELEGRAM_ALLOWED_USER_ID:
        raise RuntimeError("TELEGRAM_ALLOWED_USER_ID ist nicht gesetzt (.env prüfen)")

    log.info("Bot startet (erlaubte User-ID: %d)…", config.TELEGRAM_ALLOWED_USER_ID)

    app = Application.builder().token(config.TELEGRAM_BOT_TOKEN).build()

    app.add_handler(CommandHandler("start", cmd_start))
    app.add_handler(CommandHandler("help", cmd_help))
    app.add_handler(CommandHandler("search", cmd_search))
    app.add_handler(CommandHandler("download", cmd_download))
    app.add_handler(CommandHandler("status", cmd_status))
    app.add_handler(CommandHandler("recent", cmd_recent))

    # /dl_<id> Links aus Suchergebnissen abfangen
    app.add_handler(MessageHandler(filters.Regex(r"^/dl_\d+"), cmd_download))

    # Unbekannte Kommandos
    app.add_handler(MessageHandler(filters.COMMAND, handle_unknown))

    log.info("Bot läuft. Ctrl+C zum Beenden.")
    app.run_polling(drop_pending_updates=True)


if __name__ == "__main__":
    main()
