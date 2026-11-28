const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, 'dist', 'vitalq-web', 'browser');
const port = 4200;

const mime = {
  '.html': 'text/html',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2'
};

/**
 * Pre-indexes static build artifacts into a safe in-memory allowlist map at startup
 * so request paths never touch filesystem resolution APIs directly.
 */
function buildAssetAllowlist(dir, baseUrl = '') {
  const assets = new Map();
  if (!fs.existsSync(dir)) return assets;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    const urlPath = `${baseUrl}/${entry.name}`;
    if (entry.isDirectory()) {
      for (const [childUrl, childAsset] of buildAssetAllowlist(fullPath, urlPath)) {
        assets.set(childUrl, childAsset);
      }
    } else if (entry.isFile()) {
      const ext = path.extname(entry.name).toLowerCase();
      assets.set(urlPath, {
        fullPath,
        contentType: mime[ext] || 'application/octet-stream'
      });
    }
  }
  return assets;
}

const allowedAssets = buildAssetAllowlist(root);
const fallbackAsset = allowedAssets.get('/index.html');

const server = http.createServer((req, res) => {
  const reqPath = (req.url || '/').split('?')[0];
  const asset = allowedAssets.get(reqPath) || fallbackAsset;

  if (!asset) {
    res.writeHead(404, { 'Content-Type': 'text/plain' });
    res.end('Build output not found');
    return;
  }

  res.writeHead(200, { 'Content-Type': asset.contentType });
  fs.createReadStream(asset.fullPath).pipe(res);
});

server.listen(port, '127.0.0.1', () => {
  console.log(`VitalQ E2E Static Server listening on http://127.0.0.1:${port}`);
});
