// WordApp PWA Service Worker
// Strategy:
//  - Static assets (same-origin): Stale-While-Revalidate
//  - HTML navigations: Network-First with /offline.html fallback (never caches authenticated HTML)
//  - Dynamic API/Auth/POST & cross-origin requests: Network-Only (untouched)

const CACHE_NAME = 'wordapp-static-v1';
const OFFLINE_URL = '/offline.html';

const PRECACHE_ASSETS = [
    OFFLINE_URL,
    '/manifest.json',
    '/icons/apple-touch-icon.png',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(PRECACHE_ASSETS))
            .catch(() => { /* Ignore individual precache failures */ })
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(
                keys
                    .filter((key) => key !== CACHE_NAME)
                    .map((key) => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

function isStaticAssetPath(pathname) {
    return (
        pathname.startsWith('/css/') ||
        pathname.startsWith('/js/') ||
        pathname.startsWith('/lib/') ||
        pathname.startsWith('/icons/') ||
        pathname === '/manifest.json' ||
        pathname === '/favicon.ico' ||
        pathname === OFFLINE_URL
    );
}

self.addEventListener('fetch', (event) => {
    const req = event.request;

    // Never intercept non-GET requests (POST/PUT/DELETE, Login, 2FA, SRS updates)
    if (req.method !== 'GET') return;

    const url = new URL(req.url);

    // Never intercept cross-origin requests (free dictionary APIs, CDNs, external audio)
    if (url.origin !== self.location.origin) return;

    // HTML navigations: always Network-First so authenticated data & CSRF tokens stay fresh
    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req).catch(() => caches.match(OFFLINE_URL))
        );
        return;
    }

    // Same-origin static assets: Stale-While-Revalidate
    if (isStaticAssetPath(url.pathname)) {
        event.respondWith(
            caches.open(CACHE_NAME).then((cache) =>
                cache.match(req).then((cached) => {
                    const networkFetch = fetch(req)
                        .then((response) => {
                            if (response && response.ok && response.type === 'basic') {
                                cache.put(req, response.clone());
                            }
                            return response;
                        })
                        .catch(() => cached);

                    return cached || networkFetch;
                })
            )
        );
    }
});
