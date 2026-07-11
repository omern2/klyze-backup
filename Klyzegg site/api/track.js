const FIREBASE_URL = 'https://klyzegg-default-rtdb.firebaseio.com';

const MALICIOUS_PATTERNS = [
  { regex: /union.*select|select.*from|insert.*into|drop\s+table|delete\s+from|update.*set/i, type: 'sql_injection', severity: 'critical', detail: 'SQL komutu tespit edildi' },
  { regex: /or\s+['"]?\d+['"]?\s*=\s*['"]?\d+/i, type: 'sql_injection', severity: 'critical', detail: 'SQL tautoloji girişimi' },
  { regex: /sleep\s*\(\s*\d+\s*\)/i, type: 'sql_injection', severity: 'critical', detail: 'SQL time-based injection' },
  { regex: /pg_sleep|waitfor\s+delay|benchmark/i, type: 'sql_injection', severity: 'critical', detail: 'SQL time-based injection' },
  { regex: /<script[^>]*>/i, type: 'xss', severity: 'critical', detail: 'Script tag injection' },
  { regex: /onerror\s*=|onload\s*=|onclick\s*=|onmouseover\s*=/i, type: 'xss', severity: 'critical', detail: 'XSS event handler' },
  { regex: /javascript\s*:/i, type: 'xss', severity: 'high', detail: 'javascript: URI XSS' },
  { regex: /alert\s*\(|prompt\s*\(|confirm\s*\(/i, type: 'xss', severity: 'high', detail: 'XSS popup fonksiyonu' },
  { regex: /\.\.\/|\.\.\\|%2e%2e|%2e%2f/i, type: 'path_traversal', severity: 'high', detail: 'Path traversal girişimi' },
  { regex: /etc\/passwd|etc\/shadow|boot\.ini/i, type: 'path_traversal', severity: 'high', detail: 'Hassas dosya erişimi' },
  { regex: /__proto__|prototype\[|constructor\[/i, type: 'prototype_pollution', severity: 'medium', detail: 'Prototype pollution' },
  { regex: /\.env|wp-config|config\.php/i, type: 'sensitive_file', severity: 'high', detail: 'Hassas dosya tarama' },
];

const SCANNER_AGENTS = ['sqlmap', 'nmap', 'nikto', 'nessus', 'openvas', 'acunetix', 'dirbuster', 'gobuster', 'wpscan', 'burpsuite', 'zap', 'hydra', 'metasploit', 'zgrab'];
const ADMIN_PATHS = ['/admin', '/wp-admin', '/administrator', '/panel', '/.env', '/config', '/backup', '/wp-config.php', '/.git/config', '/db', '/phpmyadmin', '/shell', '/cmd', '/.htaccess'];

function detectThreats(ip, method, path, ua, queryString) {
  const threats = [];
  const combined = (path + ' ' + (queryString || '')).toLowerCase();

  for (const p of MALICIOUS_PATTERNS) {
    if (p.regex.test(combined)) threats.push({ type: p.type, severity: p.severity, detail: p.detail });
  }

  const lower = ua.toLowerCase();
  if (SCANNER_AGENTS.some(a => lower.includes(a))) threats.push({ type: 'scanner', severity: 'high', detail: 'Güvenlik tarayıcı tespit edildi' });
  if (ADMIN_PATHS.some(p => path.toLowerCase().includes(p))) threats.push({ type: 'scanner', severity: 'high', detail: 'Admin/hassas yol taraması: ' + path });
  if (method !== 'GET' && method !== 'POST' && method !== 'HEAD') threats.push({ type: 'bad_method', severity: 'medium', detail: 'Anormal HTTP metodu: ' + method });

  return threats;
}

function getSeverity(threats) {
  if (threats.some(t => t.severity === 'critical')) return 'critical';
  if (threats.some(t => t.severity === 'high')) return 'high';
  if (threats.some(t => t.severity === 'medium')) return 'medium';
  return 'low';
}

async function saveThreatAndNotify(ip, method, path, ua, threats) {
  const threatData = {
    ip, timestamp: Date.now(), path: path || '/',
    userAgent: (ua || '').substring(0, 300), method: method || 'GET',
    severity: getSeverity(threats), blocked: true, threats: threats.slice(0, 10),
  };
  try {
    await fetch(`${FIREBASE_URL}/tracking/threats.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(threatData),
    }).catch(() => {});
    const msg = threats.map(t => t.detail).join(', ');
    await fetch(`${FIREBASE_URL}/bildirimler.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ip, timestamp: Date.now(), type: threats[0]?.type || 'unknown',
        severity: getSeverity(threats), message: msg.substring(0, 200),
        path: path || '/', okundu: false, details: JSON.stringify(threats.slice(0, 5)),
      }),
    }).catch(() => {});
  } catch (e) { console.error('saveThreat error:', e); }
}

module.exports = async (req, res) => {
  if (req.method !== 'POST') return res.status(405).json({ error: 'Yalnızca POST' });

  const ip = req.headers['x-forwarded-for']?.split(',')[0]?.trim()
    || req.headers['x-real-ip']
    || req.socket?.remoteAddress
    || '0.0.0.0';

  const data = {
    ip, timestamp: Date.now(),
    userAgent: (req.headers['user-agent'] || '').substring(0, 200),
    path: req.query?.path || '/',
  };

  const threats = detectThreats(ip, req.method, data.path, data.userAgent, JSON.stringify(req.query));
  if (threats.length > 0) {
    await saveThreatAndNotify(ip, req.method, data.path, data.userAgent, threats);
  }

  try {
    const fbRes = await fetch(`${FIREBASE_URL}/tracking/ips.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    if (!fbRes.ok) {
      const errText = await fbRes.text();
      console.error('Firebase hata:', errText);
      return res.status(200).json({ success: true, threats });
    }
    return res.status(200).json({ success: true, threats: threats.length > 0 });
  } catch (err) {
    console.error('Sunucu hatasi:', err);
    return res.status(200).json({ success: true, threats: threats.length > 0 });
  }
};
