const CACHE_NAME = 'church-card-v3';
const STATIC_CARD_ASSETS = new Set(['style.css', 'sw.js', 'icon-192.png', 'icon-512.png']);

self.addEventListener('install', () => {
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

function isStaticCardAsset(pathname) {
    const parts = pathname.split('/').filter(Boolean);
    return parts.length === 2 && parts[0] === 'card' && STATIC_CARD_ASSETS.has(parts[1]);
}

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);
    const isCardRequest = url.pathname.startsWith('/card/');
    const isQrRequest = /^\/api\/members\/[^/]+\/qr\.png$/.test(url.pathname);

    if (event.request.method !== 'GET' || !(isCardRequest || isQrRequest)) {
        return;
    }

    if (isQrRequest || isStaticCardAsset(url.pathname)) {
        // The QR image and static assets never change for a given token — safe to
        // serve from cache indefinitely once fetched once.
        event.respondWith(
            caches.open(CACHE_NAME).then(async (cache) => {
                const cached = await cache.match(event.request);
                if (cached) {
                    return cached;
                }
                const response = await fetch(event.request);
                if (response.ok) {
                    cache.put(event.request, response.clone());
                }
                return response;
            })
        );
    } else {
        // The card page and its per-member manifest can change (an admin can edit the
        // member's name) — always try the network first so edits show up immediately,
        // and only fall back to the last cached copy when there's no connection.
        event.respondWith(
            fetch(event.request)
                .then((response) => {
                    if (response.ok) {
                        caches.open(CACHE_NAME).then((cache) => cache.put(event.request, response.clone()));
                    }
                    return response;
                })
                .catch(() => caches.match(event.request))
        );
    }
});
