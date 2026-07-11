const https = require('https');

const FIREBASE_URL = 'https://klyzegg-default-rtdb.firebaseio.com';

const THREAT_PATTERNS = {
  sql_injection: [
    /\bUNION\b.*\bSELECT\b/i, /\bSELECT\b.*\bFROM\b/i, /\bDROP\b.*\bTABLE\b/i,
    /\bDELETE\b.*\bFROM\b/i, /\bINSERT\b.*\bINTO\b/i, /\bUPDATE\b.*\bSET\b/i,
    /OR\s+['"]?\d+['"]?\s*=\s*['"]?\d+['"]?/i, /AND\s+['"]?\d+['"]?\s*=\s*['"]?\d+['"]?/i,
    /admin['"]?\s*--/, /['"];\s*--/, /1=1/, /1=2/, /'.*OR.*'/, /sleep\s*\(/i,
    /exec\s*\(/i, /xp_cmdshell/i, /@@version/i, /LOAD_FILE/i,
    /into\s+outfile/i, /CHAR\s*\(/i, /CONVERT\s*\(/i,
  ],
  xss: [
    /<script[\s>]/i, /alert\s*\(/i, /onerror\s*=/i, /onload\s*=/i,
    /prompt\s*\(/i, /confirm\s*\(/i, /javascript\s*:/i, /<iframe[\s>]/i,
    /onclick\s*=/i, /onfocus\s*=/i, /<svg[\s>]/i, /<img[\s>][^>]*on/i,
    /expression\s*\(/i, /<embed[\s>]/i, /<object[\s>]/i, /<style[\s>]/i,
  ],
  path_traversal: [
    /\.\.\/|\.\.\\|%2e%2e%2f|%2e%2e\\/i, /\/etc\/passwd/i, /\/etc\/shadow/i,
    /\/\.env/i, /\/wp-admin/i, /\/wp-login/i, /\/administrator/i,
    /\/phpmyadmin/i, /\/\.git/i, /\/\.svn/i, /\/aws\/credentials/i,
    /\/config\./i, /\/backup/i, /\/\.htaccess/i, /\/boot\.ini/i,
    /\/windows\/system32/i, /\/proc\/self\/environ/i,
  ],
  scanner: [
    /sqlmap/i, /nmap/i, /gobuster/i, /dirbuster/i, /nikto/i, /acunetix/i,
    /netsparker/i, /nessus/i, /openvas/i, /burpsuite/i, /w3af/i, /zap/i,
    /arachni/i, /wpscan/i, /joomscan/i, /masscan/i,
  ],
  bad_ua: [
    /^curl\/|^wget\/|^python-requests|^Go-http-client|^Java\//i,
    /^libwww-perl|^http-client|^HTTP_Request|^PycURL|^Ruby$/i,
    /^LWP::Simple|^WWW-Mechanize|^HTTP::Engine/i,
  ],
  large_payload: [/^.{10000,}$/],
};

const THREAT_SEVERITY = {
  sql_injection: 'critical',
  xss: 'critical',
  path_traversal: 'high',
  scanner: 'high',
  bad_ua: 'medium',
  large_payload: 'medium',
};

let requestCounts = {};

function detectThreats(ip, path, query, body, userAgent, method) {
  const threats = [];
  const combined = `${path} ${JSON.stringify(query || {})} ${body || ''}`;

  for (const [type, patterns] of Object.entries(THREAT_PATTERNS)) {
    if (type === 'bad_ua') {
      for (const pattern of patterns) {
        if (pattern.test(userAgent)) {
          threats.push({ type, severity: 'medium', detail: `Şüpheli User-Agent: ${userAgent.slice(0, 50)}` });
          break;
        }
      }
      continue;
    }

    if (type === 'large_payload') {
      if (body && body.length > 10000) {
        threats.push({ type, severity: 'medium', detail: `Büyük payload: ${body.length} bytes` });
      }
      continue;
    }

    for (const pattern of patterns) {
      if (pattern.test(combined)) {
        threats.push({
          type,
          severity: THREAT_SEVERITY[type] || 'medium',
          detail: `${type === 'sql_injection' ? 'SQL Enjeksiyon' : type === 'xss' ? 'XSS' : type === 'path_traversal' ? 'Path Tarama' : type === 'scanner' ? 'Güvenlik Tarayıcı' : type} tespit edildi: ${pattern.toString().slice(0, 60)}`,
        });
        break;
      }
    }
  }

  const bodyStr = JSON.stringify(body || '');
  if (bodyStr.includes('constructor') && bodyStr.includes('prototype')) {
    threats.push({ type: 'prototype_pollution', severity: 'high', detail: 'Prototype pollution saldırısı' });
  }

  if (!['GET', 'POST', 'OPTIONS', 'HEAD'].includes(method)) {
    threats.push({ type: 'bad_method', severity: 'low', detail: `Anormal HTTP metodu: ${method}` });
  }

  const now = Date.now();
  const minute = Math.floor(now / 60000);
  const key = `${ip}_${minute}`;
  requestCounts[key] = (requestCounts[key] || 0) + 1;
  setTimeout(() => { delete requestCounts[key]; }, 60000).unref();
  if (requestCounts[key] > 30) {
    threats.push({ type: 'rate_limit', severity: 'medium', detail: `Rate-limit aşımı: ${requestCounts[key]} istek/dk` });
  }

  return threats;
}

function putFirebase(path, data) {
  return new Promise((resolve, reject) => {
    const url = new URL(`${FIREBASE_URL}${path}`);
    const body = JSON.stringify(data);
    const options = {
      hostname: url.hostname,
      path: url.pathname + url.search,
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(body),
      },
    };
    const req = https.request(options, (res) => {
      let resp = '';
      res.on('data', (chunk) => resp += chunk);
      res.on('end', () => {
        if (res.statusCode === 200) resolve();
        else reject(new Error(`${res.statusCode}: ${resp.slice(0, 100)}`));
      });
    });
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

module.exports = async (req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, GET, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') return res.status(200).end();

  try {
    const ip = req.headers['x-forwarded-for']?.split(',')[0]?.trim()
      || req.headers['x-real-ip']
      || req.connection?.remoteAddress
      || '0.0.0.0';

    const timestamp = Date.now();
    const path = req.body?.path || req.headers['referer'] || '/';
    const userAgent = req.headers['user-agent'] || '';
    const method = req.method || 'POST';
    const bodyRaw = req.body ? JSON.stringify(req.body) : '';
    const queryParams = req.query || {};
    const id = `${timestamp}_${ip.replace(/\./g, '_')}`;

    const threats = detectThreats(ip, path, queryParams, bodyRaw, userAgent, method);
    const isThreat = threats.length > 0;
    const maxSeverity = isThreat ? threats.reduce((max, t) => {
      const levels = { low: 0, medium: 1, high: 2, critical: 3 };
      return levels[t.severity] > levels[max] ? t.severity : max;
    }, 'low') : null;

    const record = {
      ip, timestamp, path, userAgent, method,
      isThreat, blocked: isThreat,
      threatCount: threats.length,
      maxSeverity,
    };

    await putFirebase(`/tracking/ips/${id}.json`, record);

    if (isThreat) {
      const threatId = `threat_${id}`;
      const threatRecord = {
        ip, timestamp, path, userAgent, method,
        threats,
        severity: maxSeverity,
        blocked: true,
      };
      await putFirebase(`/tracking/threats/${threatId}.json`, threatRecord);
    }

    return res.status(200).json({
      success: true, ip,
      threat: isThreat ? { count: threats.length, severity: maxSeverity } : null,
    });
  } catch (error) {
    console.error('Hata:', error);
    return res.status(500).json({ error: 'Islem basarisiz' });
  }
};
