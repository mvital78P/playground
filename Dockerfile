FROM python:3.12-slim

# Build-Dependencies für sqlite-vec
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    python3-dev \
    supervisor \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Dependencies zuerst (Docker Layer Cache)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# App-Code kopieren
COPY config.py .
COPY bot/ bot/
COPY sync/ sync/
COPY db/ db/
COPY drive_mcp/ drive_mcp/

# Supervisord-Konfiguration
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Data-Verzeichnis anlegen
RUN mkdir -p /app/data/downloads

CMD ["supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
