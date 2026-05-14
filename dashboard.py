"""
Web-Dashboard für Drive Search System
Starten: python dashboard.py
Dann öffnen: http://localhost:5000
"""
import os
import sys
import subprocess
import signal
import psutil
from flask import Flask, render_template, jsonify, request
from pathlib import Path

app = Flask(__name__)
app.config['TEMPLATES_AUTO_RELOAD'] = True
app.config['SEND_FILE_MAX_AGE_DEFAULT'] = 0

# Prozess-IDs speichern
processes = {
    "mcp_server": None,
    "telegram_bot": None,
    "sync_scheduler": None,
}

def get_process_status(name):
    """Prüft ob ein Prozess läuft."""
    proc = processes.get(name)
    if proc and proc.poll() is None:
        return "running"
    return "stopped"

def start_process(name, command):
    """Startet einen Prozess."""
    if get_process_status(name) == "running":
        return {"success": False, "message": f"{name} läuft bereits"}

    try:
        # Windows: CREATE_NEW_PROCESS_GROUP für sauberes Beenden
        if sys.platform == "win32":
            proc = subprocess.Popen(
                command,
                shell=True,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
        else:
            proc = subprocess.Popen(
                command,
                shell=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
        processes[name] = proc
        return {"success": True, "message": f"{name} gestartet (PID: {proc.pid})"}
    except Exception as e:
        return {"success": False, "message": f"Fehler: {str(e)}"}

def stop_process(name):
    """Stoppt einen Prozess."""
    proc = processes.get(name)
    if not proc or proc.poll() is not None:
        return {"success": False, "message": f"{name} läuft nicht"}

    try:
        # Beende den Prozess und alle Child-Prozesse
        parent = psutil.Process(proc.pid)
        for child in parent.children(recursive=True):
            child.terminate()
        parent.terminate()
        parent.wait(timeout=5)
        processes[name] = None
        return {"success": True, "message": f"{name} gestoppt"}
    except Exception as e:
        return {"success": False, "message": f"Fehler: {str(e)}"}

@app.route('/')
def index():
    """Dashboard-Startseite."""
    return render_template('dashboard.html')

@app.route('/api/status')
def status():
    """Status aller Services."""
    return jsonify({
        "mcp_server": get_process_status("mcp_server"),
        "telegram_bot": get_process_status("telegram_bot"),
        "sync_scheduler": get_process_status("sync_scheduler"),
    })

@app.route('/api/start/<service>', methods=['POST'])
def start_service(service):
    """Startet einen Service."""
    commands = {
        "mcp_server": "python drive_mcp/server.py",
        "telegram_bot": "python -m bot.main",
        "sync_scheduler": "python -m sync.scheduler",
    }

    if service not in commands:
        return jsonify({"success": False, "message": "Unbekannter Service"})

    result = start_process(service, commands[service])
    return jsonify(result)

@app.route('/api/stop/<service>', methods=['POST'])
def stop_service(service):
    """Stoppt einen Service."""
    result = stop_process(service)
    return jsonify(result)

@app.route('/api/run_sync', methods=['POST'])
def run_sync():
    """Führt einen einmaligen Sync durch."""
    try:
        result = subprocess.run(
            ["python", "run_sync.py"],
            capture_output=True,
            text=True,
            timeout=300
        )
        return jsonify({
            "success": result.returncode == 0,
            "output": result.stdout,
            "error": result.stderr
        })
    except subprocess.TimeoutExpired:
        return jsonify({"success": False, "message": "Timeout (>5 Min)"})
    except Exception as e:
        return jsonify({"success": False, "message": str(e)})

@app.route('/api/restart_all', methods=['POST'])
def restart_all():
    """Startet alle Services neu."""
    import time

    commands = {
        "mcp_server": "python drive_mcp/server.py",
        "telegram_bot": "python -m bot.main",
        "sync_scheduler": "python -m sync.scheduler",
    }

    results = []

    # 1. Alle Services stoppen
    for service in ["mcp_server", "telegram_bot", "sync_scheduler"]:
        result = stop_process(service)
        results.append(f"Stop {service}: {result['message']}")

    # 2. Kurz warten für sauberes Herunterfahren
    time.sleep(2)

    # 3. Alle Services wieder starten
    for service, command in commands.items():
        result = start_process(service, command)
        results.append(f"Start {service}: {result['message']}")

    return jsonify({
        "success": True,
        "message": "Alle Services wurden neu gestartet",
        "details": results
    })

@app.route('/api/config')
def get_config():
    """Zeigt aktuelle Konfiguration."""
    import config as cfg
    return jsonify({
        "llm_provider": cfg.LLM_PROVIDER,
        "embedding_provider": cfg.EMBEDDING_PROVIDER,
        "lmstudio_host": cfg.LMSTUDIO_HOST,
        "sync_interval": cfg.SYNC_INTERVAL_MINUTES,
    })

if __name__ == '__main__':
    print("=" * 60)
    print("  Drive Search Dashboard")
    print("=" * 60)
    print(f"  URL: http://localhost:5000")
    print("  Drücke Ctrl+C zum Beenden")
    print("=" * 60)

    try:
        app.run(host='0.0.0.0', port=5000, debug=False)
    finally:
        # Cleanup: Alle Prozesse beenden
        for name in processes:
            if processes[name]:
                stop_process(name)
