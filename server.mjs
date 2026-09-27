import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('.', import.meta.url));
const types = {'.html':'text/html; charset=utf-8','.css':'text/css','.js':'text/javascript','.svg':'image/svg+xml'};
http.createServer(async (req,res) => {
  try {
    const url = new URL(req.url,'http://localhost');
    const file = path.resolve(root,'.'+decodeURIComponent(url.pathname === '/' ? '/index.html' : url.pathname));
    if (!file.startsWith(root) || !['GET','HEAD'].includes(req.method)) {res.writeHead(403).end();return;}
    const data = await readFile(file);
    res.writeHead(200,{'Content-Type':types[path.extname(file)] || 'application/octet-stream','Cache-Control':'no-store'});
    res.end(req.method === 'HEAD' ? undefined : data);
  } catch {res.writeHead(404).end('Not found');}
}).listen(4173,'127.0.0.1',()=>console.log('Message Replier: http://127.0.0.1:4173'));
