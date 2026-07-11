const FIREBASE_URL = 'https://klyzegg-default-rtdb.firebaseio.com';

const MALICIOUS_PATTERNS = [
  { regex: /union.*select|select.*from|insert.*into|drop\s+table|delete\s+from|update.*set/i, type: 'sql_injection', severity: 'critical', detail: 'SQL komutu tespit edildi' },
  { regex: /or\s+['"]?\d+['"]?\s*=\s*['"]?\d+/i, type: 'sql_injection', severity: 'critical', detail: 'SQL tautoloji girişimi (OR 1=1)' },
  { regex: /sleep\s*\(\s*\d+\s*\)/i, type: 'sql_injection', severity: 'critical', detail: 'SQL time-based injection (sleep())' },
  { regex: /pg_sleep|waitfor\s+delay|benchmark/i, type: 'sql_injection', severity: 'critical', detail: 'SQL time-based injection' },
  { regex: /'\s*--|\/\*.*\*\//, type: 'sql_injection', severity: 'high', detail: 'SQL yorum satırı injection' },
  { regex: /union\s*all\s*select/i, type: 'sql_injection', severity: 'critical', detail: 'SQL UNION injection' },
  { regex: /<script[^>]*>/i, type: 'xss', severity: 'critical', detail: 'Script tag injection' },
  { regex: /onerror\s*=|onload\s*=|onclick\s*=|onmouseover\s*=/i, type: 'xss', severity: 'critical', detail: 'XSS event handler' },
  { regex: /javascript\s*:/i, type: 'xss', severity: 'high', detail: 'javascript: URI XSS' },
  { regex: /alert\s*\(|prompt\s*\(|confirm\s*\(/i, type: 'xss', severity: 'high', detail: 'XSS popup fonksiyonu' },
  { regex: /eval\s*\(|document\.write|innerHTML/i, type: 'xss', severity: 'high', detail: 'XSS DOM manipulation' },
  { regex: /\.\.\/|\.\.\\|%2e%2e|%2e%2f|\.\.%2f/i, type: 'path_traversal', severity: 'high', detail: 'Path traversal girişimi' },
  { regex: /etc\/passwd|etc\/shadow|boot\.ini|windows\/system32/i, type: 'path_traversal', severity: 'high', detail: 'Hassas dosya erişimi' },
  { regex: /__proto__|prototype\[|constructor\[/i, type: 'prototype_pollution', severity: 'medium', detail: 'Prototype pollution girişimi' },
  { regex: /\.env|wp-config|config\.php|config\.json/i, type: 'sensitive_file', severity: 'high', detail: 'Hassas dosya tarama' },
];

const SCANNER_AGENTS = [
  'sqlmap', 'nmap', 'nikto', 'nessus', 'openvas', 'acunetix', 'netsparker',
  'dirbuster', 'gobuster', 'wpscan', 'zmeu', 'burpsuite', 'masscan',
  'zap', 'whatweb', 'wappalyzer', 'hydra', 'medusa', 'aircrack',
  'metasploit', 'dorkbot', 'scanner', 'zgrab', 'jael'
];

const BAD_BOT_AGENTS = [
  'curl', 'wget', 'python-requests', 'python-urllib', 'go-http-client',
  'java/', 'libwww', 'perl-', 'ruby', 'php', 'lwp-', 'winhttp',
];

const ADMIN_PATHS = [
  '/admin', '/wp-admin', '/administrator', '/panel', '/login',
  '/.env', '/config', '/backup', '/wp-config.php', '/.git/config',
  '/db', '/database', '/sql', '/phpmyadmin', '/pma',
  '/wp-content', '/wp-includes', '/shell', '/cmd',
  '/.htaccess', '/config.php', '/config.json',
  '/api/users', '/api/admin', '/api/config', '/api/db',
  '/test', '/debug', '/info', '/status', '.php'
];

function isAdminPath(path) {
  return ADMIN_PATHS.some(p => path.toLowerCase().includes(p));
}

function isScanner(ua) {
  const lower = ua.toLowerCase();
  return SCANNER_AGENTS.some(a => lower.includes(a));
}

function isBadBot(ua) {
  const lower = ua.toLowerCase();
  if (lower.includes('mozilla') || lower.includes('chrome') || lower.includes('safari') || lower.includes('firefox') || lower.includes('edge') || lower.includes('opera')) return false;
  return BAD_BOT_AGENTS.some(a => lower.startsWith(a));
}

function detectThreats(ip, method, path, ua, queryString, bodySize) {
  const threats = [];
  const lowerPath = path.toLowerCase();
  const combined = lowerPath + ' ' + (queryString || '').toLowerCase();

  for (const pattern of MALICIOUS_PATTERNS) {
    if (pattern.regex.test(combined)) {
      threats.push({ type: pattern.type, severity: pattern.severity, detail: pattern.detail });
    }
  }

  if (isScanner(ua)) {
    threats.push({ type: 'scanner', severity: 'high', detail: 'Güvenlik tarayıcı/bot tespit edildi' });
  }

  if (isBadBot(ua) && !isScanner(ua)) {
    threats.push({ type: 'bad_ua', severity: 'medium', detail: 'Şüpheli kullanıcı ajanı: ' + ua.substring(0, 60) });
  }

  if (isAdminPath(path)) {
    threats.push({ type: 'scanner', severity: 'high', detail: 'Admin/hassas yol taraması' });
  }

  if (method !== 'GET' && method !== 'POST' && method !== 'HEAD' && method !== 'OPTIONS') {
    threats.push({ type: 'bad_method', severity: 'medium', detail: 'Anormal HTTP metodu: ' + method });
  }

  const qsLen = (queryString || '').length;
  if (qsLen > 500) {
    threats.push({ type: 'large_payload', severity: 'low', detail: 'Büyük query string: ' + qsLen + ' karakter' });
  }

  if (bodySize > 10240) {
    threats.push({ type: 'large_payload', severity: 'low', detail: 'Büyük body: ' + bodySize + ' bytes' });
  }

  return threats;
}

function getOverallSeverity(threats) {
  if (threats.some(t => t.severity === 'critical')) return 'critical';
  if (threats.some(t => t.severity === 'high')) return 'high';
  if (threats.some(t => t.severity === 'medium')) return 'medium';
  return 'low';
}

async function saveThreat(ip, method, path, ua, threats) {
  try {
    const threatData = {
      ip,
      timestamp: Date.now(),
      path: path || '/',
      userAgent: (ua || '').substring(0, 300),
      method: method || 'GET',
      severity: getOverallSeverity(threats),
      blocked: true,
      threats: threats.slice(0, 10),
    };

    const fbRes = await fetch(`${FIREBASE_URL}/tracking/threats.json`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(threatData),
    });

    if (!fbRes.ok) {
      const errText = await fbRes.text();
      console.error('Firebase threat error:', errText);
    }

    const notificationMsg = threats.map(t => t.detail).join(', ');
    const notifData = {
      ip,
      timestamp: Date.now(),
      type: threats[0]?.type || 'unknown',
      severity: getOverallSeverity(threats),
      message: notificationMsg.substring(0, 200),
      path: path || '/',
      okundu: false,
      details: JSON.stringify(threats.slice(0, 5)),
    };

    await fetch(`${FIREBASE_URL}/bildirimler.json`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(notifData),
    }).catch(() => {});

    return true;
  } catch (err) {
    console.error('saveThreat hatasi:', err);
    return false;
  }
}

module.exports = async (req, res) => {
  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Yalnızca POST' });
  }

  const ip = req.headers['x-forwarded-for']?.split(',')[0]?.trim()
    || req.headers['x-real-ip']
    || req.socket?.remoteAddress
    || '0.0.0.0';

  const method = req.method;
  const path = req.query?.path || '/';
  const ua = req.headers['user-agent'] || '';
  const queryString = req.query ? JSON.stringify(req.query) : '';
  let bodySize = 0;
  if (req.body) {
    bodySize = JSON.stringify(req.body).length;
  }

  const threats = detectThreats(ip, method, path, ua, queryString, bodySize);

  if (threats.length > 0) {
    await saveThreat(ip, method, path, ua, threats);
    return res.status(200).json({ threat: true, threats, message: 'Tehdit kaydedildi' });
  }

  return res.status(200).json({ threat: false });
};
