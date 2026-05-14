import requests
import config

def generate_answer(query: str, context: str) -> str:
    """
    Generiert eine Antwort basierend auf der Suchanfrage und dem gefundenen Kontext.
    Nutzt Gemini via REST API.
    """
    if not config.GEMINI_API_KEY:
        return "Fehler: GEMINI_API_KEY nicht konfiguriert."

    url = (
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?"
        f"key={config.GEMINI_API_KEY}"
    )

    prompt = f"""
Du bist ein hilfreicher Assistent, der Fragen basierend auf Dokumenten aus einem Google Drive beantwortet.
Hier ist der relevante Kontext aus den gefundenen Dokumenten:

--- KONTEXT START ---
{context}
--- KONTEXT ENDE ---

FRAGE DES NUTZERS: {query}

Anweisungen:
1. Beantworte die Frage NUR basierend auf dem oben genannten Kontext.
2. Wenn die Antwort im Kontext nicht enthalten ist, sage höflich, dass du die Information in den Dokumenten nicht finden konntest.
3. Deine Antwort sollte präzise und gut strukturiert sein.
4. Nenne nach Möglichkeit, aus welchem Dokument die Information stammt, falls der Kontext Dateinamen enthält.

ANTWORT:
"""

    payload = {
        "contents": [{
            "parts": [{"text": prompt}]
        }],
        "generationConfig": {
            "temperature": 0.3,
            "topK": 40,
            "topP": 0.95,
            "maxOutputTokens": 1024,
        }
    }

    try:
        response = requests.post(url, json=payload, timeout=60)
        response.raise_for_status()
        data = response.json()
        
        # Extrahiere die Antwort aus der Gemini-Struktur
        if "candidates" in data and len(data["candidates"]) > 0:
            candidate = data["candidates"][0]
            if "content" in candidate and "parts" in candidate["content"]:
                return candidate["content"]["parts"][0]["text"]
        
        return "Entschuldigung, ich konnte keine Antwort generieren."
    except Exception as e:
        return f"Fehler bei der Kommunikation mit Gemini: {str(e)}"
