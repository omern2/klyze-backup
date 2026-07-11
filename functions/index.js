const functions = require('firebase-functions');
const admin = require('firebase-admin');
admin.initializeApp();

const db = admin.database();

const CRITICAL_NODES = ['config', 'apiKeys', 'killswitch'];
const IGNORE_NODES = ['tracking/ips', 'audit', 'bildirimler'];

function severityFor(node, action, auth, before, after, path) {
  if (CRITICAL_NODES.some(n => path.includes(n))) return 'critical';
  if (action === 'delete') return 'critical';
  if (!auth) return 'critical';
  if (node === 'users' && action !== 'create') return 'warning';
  if (node === 'reviews') return 'info';
  if (node === 'tracking/threats') return 'warning';
  if (node === 'lobbies' || node === 'rooms' || node === 'matchmaking') return 'info';
  if (node === 'bildirimler' || node === 'audit') return null;
  return 'info';
}

function messageFor(node, action, key, prevVal, newVal, authUid, path) {
  const uid = authUid ? authUid.substring(0, 12) + '...' : 'Anonim';
  if (action === 'sil') {
    if (prevVal && typeof prevVal === 'object') return `🔴 ${node}/${key} silindi (${uid})`;
    return `🔴 ${node}/${key}: "${prevVal}" silindi (${uid})`;
  }
  if (action === 'create') {
    if (key) return `📝 ${node}'a yeni kayit: ${key} (${uid})`;
    return `📝 Yeni ${node} kaydi (${uid})`;
  }
  if (prevVal !== undefined && prevVal !== null) {
    const p = typeof prevVal === 'string' ? prevVal.substring(0, 30) : JSON.stringify(prevVal).substring(0, 30);
    const n = typeof newVal === 'string' ? newVal.substring(0, 30) : JSON.stringify(newVal).substring(0, 30);
    return `✏️ ${node}/${key}: "${p}" → "${n}" (${uid})`;
  }
  return `✏️ ${node}/${key} guncellendi (${uid})`;
}

exports.databaseAudit = functions.database
  .ref('/{node}/{childId}/{rest=}')
  .onWrite(async (change, context) => {
    const node = context.params.node;
    const childId = context.params.childId || '';
    const fullPath = context.resource.name;
    const pathParts = fullPath.split('/');
    const relevantPath = pathParts.slice(pathParts.indexOf(node)).join('/');

    if (IGNORE_NODES.includes(node)) return null;

    const before = change.before.val();
    const after = change.after.val();
    const beforeExists = change.before.exists();
    const afterExists = change.after.exists();

    let action;
    if (!beforeExists && afterExists) action = 'create';
    else if (beforeExists && !afterExists) action = 'sil';
    else if (beforeExists && afterExists) action = 'update';
    else return null;

    const auth = context.auth;
    const authUid = auth ? (auth.uid || 'anon') : null;
    const severity = severityFor(node, action, auth, before, after, relevantPath);
    if (severity === null) return null;

    const prevVal = before ? (typeof before === 'object' ? '[obje]' : before) : null;
    const newVal = after ? (typeof after === 'object' ? '[obje]' : after) : null;
    const key = childId || node;

    const msg = messageFor(node, action, key, prevVal, newVal, authUid, relevantPath);

    const auditEntry = {
      action,
      node,
      childId,
      path: relevantPath,
      key,
      previousValue: prevVal ? String(prevVal).substring(0, 200) : null,
      newValue: newVal ? String(newVal).substring(0, 200) : null,
      authUid,
      timestamp: Date.now(),
      severity,
      message: msg.substring(0, 200),
    };

    try {
      await db.ref('audit').push().set(auditEntry);
    } catch (e) {
      console.error('Audit yazma hatasi:', e);
    }

    try {
      await db.ref('bildirimler').push().set({
        ip: 'firebase',
        timestamp: Date.now(),
        type: 'firebase_audit',
        severity,
        message: msg.substring(0, 200),
        path: relevantPath,
        okundu: false,
        details: JSON.stringify({ node, action, key, authUid }),
      });
    } catch (e) {
      console.error('Bildirim yazma hatasi:', e);
    }

    return null;
  });

exports.auditAll = functions.database
  .ref('/{node}')
  .onWrite(async (change, context) => {
    const node = context.params.node;
    if (IGNORE_NODES.includes(node) || node === 'audit' || node === 'bildirimler') return null;

    const before = change.before.val();
    const after = change.after.val();
    if (!before && !after) return null;

    const beforeExists = change.before.exists();
    const afterExists = change.after.exists();

    if (afterExists && beforeExists) {
      if (typeof after === 'object' && typeof before === 'object') {
        const addedKeys = Object.keys(after).filter(k => before[k] === undefined);
        const removedKeys = Object.keys(before).filter(k => after[k] === undefined);
        const changedKeys = Object.keys(after).filter(k => before[k] !== undefined && after[k] !== undefined && JSON.stringify(before[k]) !== JSON.stringify(after[k]));

        if (changedKeys.length > 5 || removedKeys.length > 3) {
          const auth = context.auth;
          const authUid = auth ? (auth.uid || 'anon') : null;
          const msg = `⚠️ ${node} toplu degisiklik: +${addedKeys.length} -${removedKeys.length} ~${changedKeys.length}`;

          try {
            await db.ref('audit').push().set({
              action: 'toplu',
              node, childId: '', path: node, key: node,
              previousValue: null, newValue: null,
              authUid, timestamp: Date.now(),
              severity: 'critical',
              message: msg,
            });
            await db.ref('bildirimler').push().set({
              ip: 'firebase', timestamp: Date.now(),
              type: 'firebase_audit', severity: 'critical',
              message: msg, path: node, okundu: false,
              details: JSON.stringify({ added: addedKeys.length, removed: removedKeys.length, changed: changedKeys.length }),
            });
          } catch (e) { console.error('Bulk audit error:', e); }
        }
      }
    }

    return null;
  });