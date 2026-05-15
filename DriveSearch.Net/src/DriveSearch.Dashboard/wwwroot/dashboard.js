// API base URL
const API_BASE = '/api';

// Current configuration
let currentConfig = null;

// Track if initial render is done
let servicesRendered = false;
let lastServicesData = null;
let lastStatsData = null;

// Documents state
let docsPage = 1;
let docsTotal = 0;
const PAGE_SIZE = 20;
let searchDebounceTimer = null;

// Initialize dashboard
document.addEventListener('DOMContentLoaded', () => {
    loadServices();
    loadSyncStats();
    loadConfiguration();

    // Auto-refresh services and stats every 5 seconds
    setInterval(() => {
        if (servicesRendered) {
            updateServicesOnly();
            updateStatsOnly();
        }
    }, 5000);
});

// Tab switching
function showTab(tabName) {
    // Hide all tabs
    document.querySelectorAll('.tab-content').forEach(tab => {
        tab.classList.remove('active');
    });

    // Remove active from all tab buttons
    document.querySelectorAll('.tab').forEach(btn => {
        btn.classList.remove('active');
    });

    // Show selected tab
    document.getElementById(`${tabName}-tab`).classList.add('active');

    // Activate tab button
    event.target.classList.add('active');
}

// Load services status
async function loadServices() {
    try {
        const response = await fetch(`${API_BASE}/status`);
        const data = await response.json();

        lastServicesData = data.services;
        renderServices(data.services);
        servicesRendered = true;
    } catch (error) {
        console.error('Failed to load services:', error);
    }
}

// Update services status only (no rebuild)
async function updateServicesOnly() {
    try {
        const response = await fetch(`${API_BASE}/status`);
        const data = await response.json();

        // Only update if data changed
        if (JSON.stringify(data.services) === JSON.stringify(lastServicesData)) return;

        lastServicesData = data.services;

        // Update each service card
        Object.entries(data.services).forEach(([key, service]) => {
            const card = document.querySelector(`.service-card[data-service="${key}"]`);
            if (!card) return;

            // Update status badge
            const statusBadge = card.querySelector('.service-status');
            if (statusBadge) {
                statusBadge.className = `service-status status-${service.status}`;
                const icon = service.status === 'running' ? 'check_circle' : service.status === 'stopped' ? 'cancel' : 'error';
                statusBadge.innerHTML = `
                    <span class="material-symbols-rounded">${icon}</span>
                    ${service.status.toUpperCase()}
                `;
            }

            // Update PID info
            const pidInfo = card.querySelector('.service-info.pid');
            if (service.pid) {
                if (pidInfo) {
                    pidInfo.querySelector('span:last-child').textContent = `PID: ${service.pid}`;
                } else {
                    const statusBadge = card.querySelector('.service-status');
                    const newPidInfo = document.createElement('div');
                    newPidInfo.className = 'service-info pid';
                    newPidInfo.innerHTML = `<span class="material-symbols-rounded">tag</span><span>PID: ${service.pid}</span>`;
                    statusBadge.after(newPidInfo);
                }
            } else if (pidInfo) {
                pidInfo.remove();
            }

            // Update timestamp
            const timeInfo = card.querySelector('.service-info:not(.pid)');
            if (timeInfo) {
                const timeSpan = timeInfo.querySelector('span:last-child');
                if (timeSpan) {
                    updateValueSmooth(timeSpan, formatDateTime(service.lastUpdated));
                }
            }
        });
    } catch (error) {
        console.error('Failed to update services:', error);
    }
}

// Load sync statistics
async function loadSyncStats() {
    try {
        const response = await fetch(`${API_BASE}/sync/stats`);
        const data = await response.json();

        lastStatsData = data;
        renderSyncStats(data);
    } catch (error) {
        console.error('Failed to load sync stats:', error);
    }
}

// Update stats values only (no rebuild)
async function updateStatsOnly() {
    try {
        const response = await fetch(`${API_BASE}/sync/stats`);
        const data = await response.json();

        // Only update if data changed
        if (JSON.stringify(data) === JSON.stringify(lastStatsData)) return;

        lastStatsData = data;

        // Update individual values with smooth transition
        const docCount = document.querySelector('.stat-item.primary .stat-value');
        const embCount = document.querySelector('.stat-item.success .stat-value');
        const syncTime = document.querySelector('.stat-item.info .stat-value');

        if (docCount && data.documentCount !== undefined) {
            updateValueSmooth(docCount, data.documentCount.toLocaleString('de-DE'));
        }
        if (embCount && data.embeddingCount !== undefined) {
            updateValueSmooth(embCount, data.embeddingCount.toLocaleString('de-DE'));
        }
        if (syncTime && data.lastSyncTime) {
            updateValueSmooth(syncTime, formatDateTime(data.lastSyncTime));
        }
    } catch (error) {
        console.error('Failed to update sync stats:', error);
    }
}

// Update value with smooth fade transition
function updateValueSmooth(element, newValue) {
    if (element.textContent === newValue) return;

    element.style.transition = 'opacity 0.3s ease-out';
    element.style.opacity = '0.3';

    setTimeout(() => {
        element.textContent = newValue;
        element.style.opacity = '1';
    }, 150);
}

// Format DateTime with proper timezone handling
function formatDateTime(dateTimeString) {
    if (!dateTimeString) return 'Nie';

    // Parse the UTC datetime string and convert to local time
    const date = new Date(dateTimeString);

    // Format with German locale and timezone
    return date.toLocaleString('de-DE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        timeZone: 'Europe/Berlin'  // Explizit deutsche Zeitzone
    });
}

// Trigger manual sync
async function triggerSync() {
    if (!confirm('Möchten Sie einen manuellen Sync starten?')) return;

    try {
        const button = event.target;
        button.disabled = true;
        button.innerHTML = '⏳ Syncing...';

        const response = await fetch(`${API_BASE}/sync/trigger`, {
            method: 'POST'
        });

        const result = await response.json();
        alert('Sync erfolgreich gestartet!');

        // Reload stats after sync
        setTimeout(() => {
            loadSyncStats();
            button.disabled = false;
            button.innerHTML = '🔄 Sync Now';
        }, 2000);
    } catch (error) {
        alert('Sync fehlgeschlagen: ' + error.message);
        event.target.disabled = false;
        event.target.innerHTML = '🔄 Sync Now';
    }
}

// Render sync statistics
function renderSyncStats(stats) {
    const statsContainer = document.getElementById('sync-stats');
    if (!statsContainer) return;

    const lastSync = stats.lastSyncTime
        ? formatDateTime(stats.lastSyncTime)
        : 'Nie';

    statsContainer.innerHTML = `
        <div class="stats-card">
            <h3>
                <span class="material-symbols-rounded">analytics</span>
                Sync-Statistiken
            </h3>
            <div class="stats-grid">
                <div class="stat-item primary">
                    <div class="stat-label">Dokumente</div>
                    <div class="stat-value">${stats.documentCount.toLocaleString('de-DE')}</div>
                </div>
                <div class="stat-item success">
                    <div class="stat-label">Embeddings</div>
                    <div class="stat-value">${stats.embeddingCount.toLocaleString('de-DE')}</div>
                </div>
                <div class="stat-item info">
                    <div class="stat-label">Letzter Sync</div>
                    <div class="stat-value" style="font-size: 1.3em;">${lastSync}</div>
                </div>
            </div>
            <button class="btn btn-success btn-large" onclick="triggerSync()">
                <span class="material-symbols-rounded">sync</span>
                Sync Now
            </button>
        </div>
    `;
}

// Render services
function renderServices(services) {
    const servicesTab = document.getElementById('services-tab');

    const html = `
        <div id="sync-stats"></div>

        <div class="service-grid">
            ${Object.entries(services).map(([key, service]) => `
                <div class="service-card" data-service="${key}">
                    <h3>${service.name}</h3>
                    <span class="service-status status-${service.status}">
                        <span class="material-symbols-rounded">${service.status === 'running' ? 'check_circle' : service.status === 'stopped' ? 'cancel' : 'error'}</span>
                        ${service.status.toUpperCase()}
                    </span>
                    ${service.pid ? `<div class="service-info pid"><span class="material-symbols-rounded">tag</span><span>PID: ${service.pid}</span></div>` : ''}
                    <div class="service-info">
                        <span class="material-symbols-rounded">schedule</span>
                        <span>${formatDateTime(service.lastUpdated)}</span>
                    </div>
                    <div class="button-group">
                        <button class="btn btn-success" onclick="startService('${key}')">
                            <span class="material-symbols-rounded">play_arrow</span>
                        </button>
                        <button class="btn btn-danger" onclick="stopService('${key}')">
                            <span class="material-symbols-rounded">stop</span>
                        </button>
                        <button class="btn btn-primary" onclick="restartService('${key}')">
                            <span class="material-symbols-rounded">restart_alt</span>
                        </button>
                    </div>
                </div>
            `).join('')}
        </div>

        <div class="action-buttons">
            <button class="btn btn-success btn-large" onclick="startAll()">
                <span class="material-symbols-rounded">play_arrow</span>
                Alle starten
            </button>
            <button class="btn btn-danger btn-large" onclick="stopAll()">
                <span class="material-symbols-rounded">stop</span>
                Alle stoppen
            </button>
            <button class="btn btn-primary btn-large" onclick="restartAll()">
                <span class="material-symbols-rounded">restart_alt</span>
                Alle neu starten
            </button>
        </div>
    `;

    servicesTab.innerHTML = html;

    // Re-render stats after services are rendered
    loadSyncStats();
}

// Service control functions
async function startService(serviceName) {
    try {
        const response = await fetch(`${API_BASE}/services/${serviceName}/start`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Service gestartet', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Starten', 'error');
    }
}

async function stopService(serviceName) {
    try {
        const response = await fetch(`${API_BASE}/services/${serviceName}/stop`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Service gestoppt', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Stoppen', 'error');
    }
}

async function restartService(serviceName) {
    try {
        const response = await fetch(`${API_BASE}/services/${serviceName}/restart`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Service neu gestartet', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Neustart', 'error');
    }
}

async function startAll() {
    try {
        const response = await fetch(`${API_BASE}/start-all`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Alle Services gestartet', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Starten', 'error');
    }
}

async function stopAll() {
    try {
        const response = await fetch(`${API_BASE}/stop-all`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Alle Services gestoppt', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Stoppen', 'error');
    }
}

async function restartAll() {
    try {
        const response = await fetch(`${API_BASE}/restart-all`, {
            method: 'POST'
        });

        if (response.ok) {
            showNotification('Alle Services neu gestartet', 'success');
            loadServices();
        }
    } catch (error) {
        showNotification('Fehler beim Neustart', 'error');
    }
}

// Load configuration
async function loadConfiguration() {
    try {
        const response = await fetch(`${API_BASE}/config`);
        currentConfig = await response.json();

        renderConfiguration(currentConfig);
    } catch (error) {
        console.error('Failed to load configuration:', error);
        document.getElementById('config-tab').innerHTML = `
            <p style="color: #dc3545; text-align: center;">
                Fehler beim Laden der Konfiguration
            </p>
        `;
    }
}

// Render configuration form
function renderConfiguration(config) {
    const configTab = document.getElementById('config-tab');

    const html = `
        <form id="config-form" onsubmit="saveConfiguration(event)">
            <!-- Database Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">database</span>Datenbank</h3>
                <div class="form-group">
                    <label>Datenbank-Pfad</label>
                    <input type="text" name="database.path" value="${config.database.path}" required>
                </div>
            </div>

            <!-- Google Drive Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">cloud</span>Google Drive</h3>
                <div class="form-group">
                    <label>Credentials-Datei</label>
                    <input type="text" name="google.credentialsFile" value="${config.google.credentialsFile}" required>
                </div>
                <div class="form-group">
                    <label>Token-Datei</label>
                    <input type="text" name="google.tokenFile" value="${config.google.tokenFile}" required>
                </div>
            </div>

            <!-- Embeddings Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">psychology</span>Embeddings</h3>
                <div class="form-group">
                    <label>Provider</label>
                    <select name="embeddings.provider" required>
                        <option value="gemini" ${config.embeddings.provider === 'gemini' ? 'selected' : ''}>Gemini</option>
                        <option value="ollama" ${config.embeddings.provider === 'ollama' ? 'selected' : ''}>Ollama</option>
                        <option value="lmstudio" ${config.embeddings.provider === 'lmstudio' ? 'selected' : ''}>LM Studio</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Gemini API Key</label>
                    <input type="password" name="embeddings.geminiApiKey" value="${config.embeddings.geminiApiKey || ''}" placeholder="Optional">
                </div>
                <div class="form-group">
                    <label>Ollama URL</label>
                    <input type="text" name="embeddings.ollamaUrl" value="${config.embeddings.ollamaUrl}">
                </div>
                <div class="form-group">
                    <label>Ollama Model</label>
                    <input type="text" name="embeddings.ollamaModel" value="${config.embeddings.ollamaModel}">
                </div>
                <div class="form-group">
                    <label>LM Studio URL</label>
                    <input type="text" name="embeddings.lmStudioUrl" value="${config.embeddings.lmStudioUrl}">
                </div>
            </div>

            <!-- LLM Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">smart_toy</span>LLM (Language Model)</h3>
                <div class="form-group">
                    <label>Provider</label>
                    <select name="llm.provider" required>
                        <option value="gemini" ${config.llm.provider === 'gemini' ? 'selected' : ''}>Gemini</option>
                        <option value="claude" ${config.llm.provider === 'claude' ? 'selected' : ''}>Claude</option>
                        <option value="ollama" ${config.llm.provider === 'ollama' ? 'selected' : ''}>Ollama</option>
                        <option value="lmstudio" ${config.llm.provider === 'lmstudio' ? 'selected' : ''}>LM Studio</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Gemini API Key</label>
                    <input type="password" name="llm.geminiApiKey" value="${config.llm.geminiApiKey || ''}" placeholder="Optional">
                </div>
                <div class="form-group">
                    <label>Anthropic API Key (Claude)</label>
                    <input type="password" name="llm.anthropicApiKey" value="${config.llm.anthropicApiKey || ''}" placeholder="Optional">
                </div>
                <div class="form-group">
                    <label>Claude Model</label>
                    <input type="text" name="llm.claudeModel" value="${config.llm.claudeModel}">
                </div>
                <div class="form-group">
                    <label>Ollama URL</label>
                    <input type="text" name="llm.ollamaUrl" value="${config.llm.ollamaUrl}">
                </div>
                <div class="form-group">
                    <label>Ollama Model</label>
                    <input type="text" name="llm.ollamaModel" value="${config.llm.ollamaModel}">
                </div>
            </div>

            <!-- Sync Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">sync</span>Synchronisation</h3>
                <div class="form-group">
                    <label>Sync-Intervall (Minuten)</label>
                    <input type="number" name="sync.intervalMinutes" value="${config.sync.intervalMinutes}" min="1" required>
                </div>
            </div>

            <!-- Telegram Configuration -->
            <div class="form-section">
                <h3><span class="material-symbols-rounded">telegram</span>Telegram Bot</h3>
                <div class="form-group">
                    <label>Bot Token</label>
                    <input type="password" name="telegram.botToken" value="${config.telegram.botToken || ''}" placeholder="Optional">
                </div>
                <div class="form-group">
                    <label>Erlaubte User ID</label>
                    <input type="number" name="telegram.allowedUserId" value="${config.telegram.allowedUserId}" placeholder="0">
                </div>
            </div>

            <button type="submit" class="btn btn-success btn-large">
                <span class="material-symbols-rounded">save</span>
                Konfiguration speichern
            </button>
        </form>
    `;

    configTab.innerHTML = html;
}

// Save configuration
async function saveConfiguration(event) {
    event.preventDefault();

    const formData = new FormData(event.target);
    const config = {
        database: {
            path: formData.get('database.path')
        },
        google: {
            credentialsFile: formData.get('google.credentialsFile'),
            tokenFile: formData.get('google.tokenFile')
        },
        embeddings: {
            provider: formData.get('embeddings.provider'),
            geminiApiKey: formData.get('embeddings.geminiApiKey'),
            ollamaUrl: formData.get('embeddings.ollamaUrl'),
            ollamaModel: formData.get('embeddings.ollamaModel'),
            ollamaDimensions: 768,
            lmStudioUrl: formData.get('embeddings.lmStudioUrl'),
            lmStudioModel: formData.get('embeddings.lmStudioModel'),
            lmStudioDimensions: 768
        },
        llm: {
            provider: formData.get('llm.provider'),
            geminiApiKey: formData.get('llm.geminiApiKey'),
            anthropicApiKey: formData.get('llm.anthropicApiKey'),
            claudeModel: formData.get('llm.claudeModel'),
            ollamaUrl: formData.get('llm.ollamaUrl'),
            ollamaModel: formData.get('llm.ollamaModel'),
            lmStudioUrl: formData.get('llm.lmStudioUrl'),
            lmStudioModel: formData.get('llm.lmStudioModel')
        },
        sync: {
            intervalMinutes: parseInt(formData.get('sync.intervalMinutes'))
        },
        telegram: {
            botToken: formData.get('telegram.botToken'),
            allowedUserId: parseInt(formData.get('telegram.allowedUserId'))
        }
    };

    try {
        const response = await fetch(`${API_BASE}/config`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(config)
        });

        if (response.ok) {
            showNotification('Konfiguration gespeichert! Bitte Services neu starten.', 'success');
            currentConfig = config;
        } else {
            showNotification('Fehler beim Speichern', 'error');
        }
    } catch (error) {
        showNotification('Fehler beim Speichern', 'error');
    }
}

// Documents tab
function debounceSearch() {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => loadDocuments(1), 350);
}

async function loadDocuments(page) {
    if (page) docsPage = page;

    const search = document.getElementById('doc-search')?.value ?? '';
    const url = `${API_BASE}/documents?page=${docsPage}&pageSize=${PAGE_SIZE}` +
        (search ? `&search=${encodeURIComponent(search)}` : '');

    try {
        const response = await fetch(url);
        const data = await response.json();
        docsTotal = data.total;
        renderDocsTable(data);
    } catch (error) {
        document.getElementById('docs-table-container').innerHTML =
            `<p style="color:var(--danger);text-align:center">Fehler beim Laden der Dokumente</p>`;
    }
}

function mimeLabel(mimeType) {
    if (mimeType.includes('pdf')) return 'PDF';
    if (mimeType.includes('wordprocessingml') || mimeType.includes('google-apps.document')) return 'Docs';
    if (mimeType.includes('spreadsheetml') || mimeType.includes('google-apps.spreadsheet')) return 'Sheets';
    if (mimeType.includes('presentationml') || mimeType.includes('google-apps.presentation')) return 'Slides';
    return mimeType.split('/').pop().substring(0, 8);
}

function renderDocsTable(data) {
    const totalPages = Math.ceil(data.total / PAGE_SIZE);
    const from = (docsPage - 1) * PAGE_SIZE + 1;
    const to = Math.min(docsPage * PAGE_SIZE, data.total);

    const rows = data.documents.map(doc => {
        const driveUrl = `https://drive.google.com/open?id=${doc.fileId}`;
        const modified = doc.modifiedAt ? new Date(doc.modifiedAt).toLocaleDateString('de-DE') : '—';
        const folder = doc.folderPath
            ? `<span class="material-symbols-rounded" style="font-size:14px;vertical-align:middle;margin-right:4px">folder</span>${escapeHtml(doc.folderPath)}`
            : '<span style="opacity:0.4">—</span>';

        return `
            <tr>
                <td><a href="${driveUrl}" target="_blank" rel="noopener">${escapeHtml(doc.name)}</a></td>
                <td class="folder-cell">${folder}</td>
                <td><span class="mime-badge">${mimeLabel(doc.mimeType)}</span></td>
                <td style="color:var(--on-surface-variant)">${modified}</td>
            </tr>`;
    }).join('');

    document.getElementById('docs-table-container').innerHTML = `
        <table class="docs-table">
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Ordner</th>
                    <th>Typ</th>
                    <th>Geändert</th>
                </tr>
            </thead>
            <tbody>${rows || '<tr><td colspan="4" style="text-align:center;opacity:0.5;padding:30px">Keine Dokumente gefunden</td></tr>'}</tbody>
        </table>
        <div class="pagination">
            <span>${data.total > 0 ? `${from}–${to} von ${data.total}` : '0 Dokumente'}</span>
            <div class="pagination-buttons">
                <button class="btn btn-primary btn-sm" onclick="loadDocuments(${docsPage - 1})" ${docsPage <= 1 ? 'disabled' : ''}>
                    <span class="material-symbols-rounded">chevron_left</span>
                </button>
                <span style="padding:8px 4px">${docsPage} / ${totalPages || 1}</span>
                <button class="btn btn-primary btn-sm" onclick="loadDocuments(${docsPage + 1})" ${docsPage >= totalPages ? 'disabled' : ''}>
                    <span class="material-symbols-rounded">chevron_right</span>
                </button>
            </div>
        </div>`;
}

function escapeHtml(text) {
    if (!text) return '';
    return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// Show notification
function showNotification(message, type) {
    const notification = document.createElement('div');
    notification.className = `notification ${type}`;

    const icon = type === 'success' ? 'check_circle' : 'error';
    notification.innerHTML = `
        <span class="material-symbols-rounded">${icon}</span>
        <span>${message}</span>
    `;

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.style.animation = 'slideInRight 0.4s ease-out reverse';
        setTimeout(() => notification.remove(), 400);
    }, 3000);
}
