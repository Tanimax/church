// Logs the admin out after a period with no mouse/keyboard/touch activity — the admin
// section often runs on a shared device (e.g. the check-in desk), so a session left open
// unattended shouldn't stay signed in indefinitely.
(function () {
    const IDLE_TIMEOUT_MS = 30 * 60 * 1000;
    let timer;

    function logout() {
        fetch('/admin/logout', { method: 'POST' }).finally(() => {
            window.location.href = '/admin/login?idle=1';
        });
    }

    function resetTimer() {
        clearTimeout(timer);
        timer = setTimeout(logout, IDLE_TIMEOUT_MS);
    }

    ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart'].forEach(evt =>
        document.addEventListener(evt, resetTimer, { passive: true }));

    resetTimer();
})();
