/**
 * lobbyService.js — Lobi veri katmanı
 * Şu an: localStorage tabanlı (JSON dosyası simülasyonu)
 * İleride: sadece bu dosya değişecek, Firebase'e geçiş burada yapılacak
 */

const STORAGE_KEY = 'klyze_lobiler';

// ─── Başlangıç verisi ─────────────────────────────────────────────────────────
function _getStore() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { lobiler: [] };
    return JSON.parse(raw);
  } catch {
    return { lobiler: [] };
  }
}

function _setStore(data) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

// ─── Yardımcı: benzersiz ID ───────────────────────────────────────────────────
function _uid() {
  return Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
}

// ─── Public API ───────────────────────────────────────────────────────────────

/**
 * Tüm lobileri döner.
 * @returns {Array}
 */
export function getLobiler() {
  return _getStore().lobiler;
}

/**
 * Yeni lobi oluşturur.
 * @param {{ grupKodu: string, olusturan: string, rutbe: string }} data
 * @returns {Object} oluşturulan lobi
 */
export function createLobi(data) {
  const store = _getStore();
  const lobi = {
    id: _uid(),
    grupKodu: data.grupKodu,
    olusturan: data.olusturan,
    rutbe: data.rutbe,
    oyuncular: [{ ad: data.olusturan, rutbe: data.rutbe, ping: Math.floor(Math.random() * 60) + 10 }],
    durum: 'bekliyor',
    olusturmaZamani: new Date().toISOString()
  };
  store.lobiler.push(lobi);
  _setStore(store);
  return lobi;
}

/**
 * Lobiye oyuncu ekler.
 * @param {string} lobiId
 * @param {{ ad: string, rutbe: string }} oyuncuData
 * @returns {Object|null} güncellenmiş lobi
 */
export function joinLobi(lobiId, oyuncuData) {
  const store = _getStore();
  const lobi = store.lobiler.find(l => l.id === lobiId);
  if (!lobi) return null;
  lobi.oyuncular.push({
    ad: oyuncuData.ad,
    rutbe: oyuncuData.rutbe,
    ping: Math.floor(Math.random() * 80) + 10
  });
  if (lobi.oyuncular.length >= 5) lobi.durum = 'dolu';
  _setStore(store);
  return lobi;
}

/**
 * Lobi siler.
 * @param {string} lobiId
 */
export function deleteLobi(lobiId) {
  const store = _getStore();
  store.lobiler = store.lobiler.filter(l => l.id !== lobiId);
  _setStore(store);
}

/**
 * Rütbeye göre uyumlu lobileri filtreler (±1 tolerans).
 * @param {string} rutbe
 * @param {number} tolerans  kaç rütbe yukarı/aşağı bakılacak (varsayılan 1)
 * @returns {Array}
 */
export function getLobilerByRutbe(rutbe, tolerans = 1) {
  const RUTBELER = ['Demir', 'Bronz', 'Gümüş', 'Altın', 'Platin', 'Elmas', 'Ölümsüz', 'Radyant'];
  const idx = RUTBELER.indexOf(rutbe);
  if (idx === -1) return [];
  const min = Math.max(0, idx - tolerans);
  const max = Math.min(RUTBELER.length - 1, idx + tolerans);
  const uygunRutbeler = RUTBELER.slice(min, max + 1);
  return getLobiler().filter(l => uygunRutbeler.includes(l.rutbe) && l.durum !== 'dolu');
}
