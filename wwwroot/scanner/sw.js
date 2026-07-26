const CACHE_NAME = 'church-scanner-v2';
const APP_SHELL = [
    '/scanner/index.html',
    '/scanner/style.css',
    '/scanner/app.js',
    '/scanner/manifest.json',
    '/scanner/lib/html5-qrcode.min.js',
    '/card/icon-192.png',
    '/card/icon-512.png'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(APP_SHELL))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(
                keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    if (url.pathname === '/api/checkin') {
        return;
    }

    if (event.request.method !== 'GET' || !url.pathname.startsWith('/scanner/')) {
        return;
    }

    event.respondWith(
        caches.match(event.request).then((cached) => cached || fetch(event.request))
    );
});
