function copyToClipboard(button) {
    const url = button.dataset.url;
    const originalText = button.dataset.originalText || button.textContent;
    button.dataset.originalText = originalText;

    const showCopied = () => {
        button.textContent = 'Copié !';
        setTimeout(() => {
            button.textContent = originalText;
        }, 1500);
    };

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(url).then(showCopied).catch(() => fallbackCopy(url, showCopied));
    } else {
        fallbackCopy(url, showCopied);
    }
}

function fallbackCopy(url, onSuccess) {
    const textarea = document.createElement('textarea');
    textarea.value = url;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    try {
        document.execCommand('copy');
        onSuccess();
    } finally {
        document.body.removeChild(textarea);
    }
}
