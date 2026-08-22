let html5QrCode;
let isProcessing = false;
const validModes = ['Culte', 'SainteCene', 'EcoleDominicale'];
const savedMode = localStorage.getItem('scanMode');
let currentMode = validModes.includes(savedMode) ? savedMode : 'Culte';

function extractToken(decodedText) {
    try {
        const url = new URL(decodedText);
        const parts = url.pathname.split('/').filter(Boolean);
        const idx = parts.indexOf('card');
        if (idx !== -1 && parts[idx + 1]) {
            return parts[idx + 1];
        }
        return decodedText;
    } catch {
        return decodedText;
    }
}

const modeButtons = {
    Culte: () => document.getElementById('mode-culte'),
    SainteCene: () => document.getElementById('mode-saintecene'),
    EcoleDominicale: () => document.getElementById('mode-ecole')
};

const modeInstructions = {
    Culte: 'Présentez le QR code à la caméra',
    SainteCene: 'Scan Sainte Cène — présentez le QR code',
    EcoleDominicale: 'Scan École Dominicale — présentez le QR code'
};

function applyMode() {
    const instructions = document.getElementById('scan-instructions');

    for (const mode of validModes) {
        modeButtons[mode]().classList.toggle('active', mode === currentMode);
        document.body.classList.toggle(`mode-${mode.toLowerCase()}-active`, mode === currentMode);
    }
    instructions.textContent = modeInstructions[currentMode];

    localStorage.setItem('scanMode', currentMode);
}

function showResult(status, fullName) {
    const overlay = document.getElementById('result-overlay');
    const icon = overlay.querySelector('.icon');
    const name = overlay.querySelector('.name');
    const message = overlay.querySelector('.message');

    overlay.className = `show ${status}`;

    const okMessages = {
        Culte: 'Présence enregistrée',
        SainteCene: 'Présence Sainte Cène enregistrée',
        EcoleDominicale: 'Présence École Dominicale enregistrée'
    };
    const duplicateMessages = {
        Culte: "Déjà enregistré aujourd'hui",
        SainteCene: 'Déjà enregistré pour la Sainte Cène ce mois-ci',
        EcoleDominicale: "Déjà enregistré à l'École Dominicale aujourd'hui"
    };

    if (status === 'ok') {
        icon.textContent = '✓';
        name.textContent = fullName ?? '';
        message.textContent = okMessages[currentMode];
    } else if (status === 'duplicate') {
        icon.textContent = '⚠';
        name.textContent = fullName ?? '';
        message.textContent = duplicateMessages[currentMode];
    } else if (status === 'not_found') {
        icon.textContent = '✕';
        name.textContent = '';
        message.textContent = 'QR code inconnu';
    } else if (status === 'not_baptized') {
        icon.textContent = '✕';
        name.textContent = fullName ?? '';
        message.textContent = 'Membre non baptisé — Sainte Cène non disponible';
    } else {
        icon.textContent = '!';
        name.textContent = '';
        message.textContent = 'Erreur réseau — réessayez';
    }

    setTimeout(() => {
        overlay.className = '';
    }, 2500);
}

async function onScanSuccess(decodedText) {
    if (isProcessing) {
        return;
    }
    isProcessing = true;

    const token = extractToken(decodedText);

    try {
        const response = await fetch('/api/checkin', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token, type: currentMode })
        });
        const data = await response.json();
        showResult(data.status, data.fullName);
    } catch {
        showResult('error', null);
    }

    setTimeout(() => {
        isProcessing = false;
    }, 2500);
}

window.addEventListener('DOMContentLoaded', () => {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/scanner/sw.js', { scope: '/scanner/' });
    }

    applyMode();

    document.getElementById('mode-culte').addEventListener('click', () => {
        currentMode = 'Culte';
        applyMode();
    });
    document.getElementById('mode-saintecene').addEventListener('click', () => {
        currentMode = 'SainteCene';
        applyMode();
    });
    document.getElementById('mode-ecole').addEventListener('click', () => {
        currentMode = 'EcoleDominicale';
        applyMode();
    });

    html5QrCode = new Html5Qrcode('reader');
    html5QrCode
        .start(
            { facingMode: 'environment' },
            { fps: 10, qrbox: { width: 250, height: 250 } },
            onScanSuccess
        )
        .catch((err) => {
            const banner = document.getElementById('status-banner');
            banner.textContent = "Impossible d'accéder à la caméra : " + err;
            banner.classList.add('show');
        });
});
