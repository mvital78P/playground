import requests
import config

def generate_answer(query: str, context: str) -> str:
    """
    Generiert eine Antwort basierend auf der Suchanfrage und dem gefundenen Kontext.
    Nutzt Gemini, Ollama, LM Studio oder Claude (je nach LLM_PROVIDER in config).
    """
    if config.LLM_PROVIDER == "ollama":
        return _generate_with_ollama(query, context)
    elif config.LLM_PROVIDER == "lmstudio":
        return _generate_with_lmstudio(query, context)
    elif config.LLM_PROVIDER == "claude":
        return _generate_with_claude(query, context)
    else:
        return _generate_with_gemini(query, context)


def _generate_with_gemini(query: str, context: str) -> str:
    """Generiert Antwort mit Gemini API."""
    if not config.GEMINI_API_KEY:
        return "Fehler: GEMINI_API_KEY nicht konfiguriert."

    # Verwende Gemini 2.5 Flash (aktuelles stabiles Modell, Juni 2025)
    url = (
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?"
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


def _generate_with_ollama(query: str, context: str) -> str:
    """Generiert Antwort mit Ollama (lokal, ohne Rate Limits)."""
    prompt = f"""Du bist ein hilfreicher Assistent, der Fragen basierend auf Dokumenten aus einem Google Drive beantwortet.
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
        "model": config.OLLAMA_LLM_MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {
            "temperature": 0.3,
            "top_p": 0.95,
            "num_predict": 1024,
        }
    }

    try:
        response = requests.post(
            f"{config.OLLAMA_HOST}/api/generate",
            json=payload,
            timeout=120
        )
        response.raise_for_status()
        data = response.json()

        if "response" in data:
            return data["response"]

        return "Entschuldigung, ich konnte keine Antwort generieren."
    except Exception as e:
        return f"Fehler bei der Kommunikation mit Ollama: {str(e)}"


def _generate_with_lmstudio(query: str, context: str) -> str:
    """Generiert Antwort mit LM Studio (OpenAI-kompatible API)."""
    prompt = f"""Du bist ein hilfreicher Assistent, der Fragen basierend auf Dokumenten aus einem Google Drive beantwortet.
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

    # OpenAI-kompatibles Format (LM Studio nutzt das gleiche)
    payload = {
        "messages": [
            {"role": "user", "content": prompt}
        ],
        "temperature": 0.3,
        "max_tokens": 1024,
        "stream": False
    }

    try:
        response = requests.post(
            f"{config.LMSTUDIO_HOST}/chat/completions",
            json=payload,
            timeout=120
        )
        response.raise_for_status()
        data = response.json()

        # OpenAI-Format: choices[0].message.content
        if "choices" in data and len(data["choices"]) > 0:
            return data["choices"][0]["message"]["content"]

        return "Entschuldigung, ich konnte keine Antwort generieren."
    except Exception as e:
        return f"Fehler bei der Kommunikation mit LM Studio: {str(e)}"


def _generate_with_claude(query: str, context: str) -> str:
    """Generiert Antwort mit Claude API (Anthropic)."""
    if not config.ANTHROPIC_API_KEY:
        return "Fehler: ANTHROPIC_API_KEY nicht konfiguriert."

    try:
        from anthropic import Anthropic

        client = Anthropic(api_key=config.ANTHROPIC_API_KEY)

        prompt = f"""Du bist ein hilfreicher Assistent, der Fragen basierend auf Dokumenten aus einem Google Drive beantwortet.

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

ANTWORT:"""

        message = client.messages.create(
            model="claude-sonnet-4-6",  # Claude 4 Sonnet - beste Balance
            max_tokens=1024,
            temperature=0.3,
            messages=[
                {"role": "user", "content": prompt}
            ]
        )

        return message.content[0].text

    except ImportError:
        return "Fehler: anthropic-Paket nicht installiert. Führe aus: pip install anthropic"
    except Exception as e:
        return f"Fehler bei der Kommunikation mit Claude: {str(e)}"
