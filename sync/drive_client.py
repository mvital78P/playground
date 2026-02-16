import os
import io
from pathlib import Path
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload

import config


def get_credentials() -> Credentials:
    creds = None
    token_path = config.GOOGLE_TOKEN_FILE

    if os.path.exists(token_path):
        creds = Credentials.from_authorized_user_file(token_path, config.GOOGLE_SCOPES)

    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(
                config.GOOGLE_CREDENTIALS_FILE, config.GOOGLE_SCOPES
            )
            creds = flow.run_local_server(port=0)
        with open(token_path, "w") as f:
            f.write(creds.to_json())

    return creds


def build_service():
    creds = get_credentials()
    return build("drive", "v3", credentials=creds)


def list_all_files(service) -> list[dict]:
    """Liste alle Dateien (eigene + geteilte) mit unterstützten MIME-Types."""
    mime_filter = " or ".join(
        [f"mimeType='{m}'" for m in config.SUPPORTED_MIME_TYPES.keys()]
    )
    query = f"trashed=false and ({mime_filter})"

    files = []
    page_token = None

    while True:
        response = service.files().list(
            q=query,
            spaces="drive",
            fields="nextPageToken, files(id, name, mimeType, size, createdTime, modifiedTime, owners)",
            pageToken=page_token,
            includeItemsFromAllDrives=True,
            supportsAllDrives=True,
            corpora="allDrives",
        ).execute()

        files.extend(response.get("files", []))
        page_token = response.get("nextPageToken")
        if not page_token:
            break

    return files


def download_file(service, file_id: str, mime_type: str, dest_path: str) -> bool:
    """Datei herunterladen. Google Docs werden als PDF exportiert."""
    Path(dest_path).parent.mkdir(parents=True, exist_ok=True)

    google_export_types = {
        "application/vnd.google-apps.document": "application/pdf",
        "application/vnd.google-apps.spreadsheet": "application/pdf",
        "application/vnd.google-apps.presentation": "application/pdf",
    }

    try:
        if mime_type in google_export_types:
            export_mime = google_export_types[mime_type]
            request = service.files().export_media(
                fileId=file_id, mimeType=export_mime
            )
        else:
            request = service.files().get_media(fileId=file_id)

        with open(dest_path, "wb") as f:
            downloader = MediaIoBaseDownload(f, request)
            done = False
            while not done:
                _, done = downloader.next_chunk()

        return True
    except Exception as e:
        print(f"Fehler beim Herunterladen von {file_id}: {e}")
        return False
