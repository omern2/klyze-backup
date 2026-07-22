-- Klyze Supabase Schema
-- Paste this into Supabase Dashboard > SQL Editor

-- 1. PROFILES (kullanıcı profilleri)
CREATE TABLE IF NOT EXISTS profiles (
  id UUID REFERENCES auth.users(id) ON DELETE CASCADE PRIMARY KEY,
  oyuncu_adi TEXT,
  tag TEXT,
  puuid TEXT,
  bolge TEXT DEFAULT 'eu',
  elo INTEGER DEFAULT 0,
  current_tier INTEGER DEFAULT 0,
  rutbe_puani INTEGER DEFAULT 0,
  rutbe TEXT DEFAULT '',
  card_small_url TEXT DEFAULT '',
  hesap_seviyesi INTEGER DEFAULT 0,
  kazanma_orani DOUBLE PRECISION DEFAULT 0,
  en_cok_oynadigi_ajan TEXT DEFAULT '',
  en_cok_kullandigi_silah TEXT DEFAULT '',
  kd_orani DOUBLE PRECISION DEFAULT 0,
  acs DOUBLE PRECISION DEFAULT 0,
  email TEXT,
  google_uid TEXT,
  son_guncelleme BIGINT DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;

CREATE POLICY "profiles_select_own" ON profiles
  FOR SELECT USING (auth.uid() = id);

CREATE POLICY "profiles_insert_own" ON profiles
  FOR INSERT WITH CHECK (auth.uid() = id);

CREATE POLICY "profiles_update_own" ON profiles
  FOR UPDATE USING (auth.uid() = id);

-- 2. BANS (yasaklı kullanıcılar)
CREATE TABLE IF NOT EXISTS bans (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  puuid TEXT UNIQUE NOT NULL,
  oyuncu_adi TEXT,
  tag TEXT,
  sebep TEXT,
  baslangic TIMESTAMPTZ DEFAULT NOW(),
  bitis TIMESTAMPTZ,
  aktif BOOLEAN DEFAULT TRUE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE bans ENABLE ROW LEVEL SECURITY;

CREATE POLICY "bans_select_all" ON bans
  FOR SELECT USING (true); -- herkes okuyabilir (client side filter)

-- 3. REPORTS (şikayetler)
CREATE TABLE IF NOT EXISTS reports (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  uid TEXT NOT NULL,
  rapor TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE reports ENABLE ROW LEVEL SECURITY;

CREATE POLICY "reports_insert_all" ON reports
  FOR INSERT WITH CHECK (true);

-- 4. LOBBIES (aktif lobiler)
CREATE TABLE IF NOT EXISTS lobbies (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  olusturan_uid TEXT NOT NULL,
  lobi_kodu TEXT,
  oyun_modu TEXT,
  max_players INTEGER DEFAULT 5,
  mevcut_oyuncu INTEGER DEFAULT 0,
  durum TEXT DEFAULT 'waiting', -- waiting, playing, closed
  host_name TEXT DEFAULT '',
  host_tag TEXT DEFAULT '',
  host_elo INTEGER DEFAULT 0,
  host_tier INTEGER DEFAULT 0,
  host_rank TEXT DEFAULT '',
  host_card_url TEXT DEFAULT '',
  group_code TEXT DEFAULT '',
  region TEXT DEFAULT 'eu',
  status TEXT DEFAULT 'waiting',
  game_mode TEXT DEFAULT '',
  min_rank_tier INTEGER DEFAULT 0,
  max_rank_tier INTEGER DEFAULT 0,
  players JSONB DEFAULT '[]',
  created_at TIMESTAMPTZ DEFAULT NOW(),
  expires_at TIMESTAMPTZ DEFAULT (NOW() + INTERVAL '30 minutes')
);

-- Add missing columns for existing tables (migration)
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_name TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_tag TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_elo INTEGER DEFAULT 0;
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_tier INTEGER DEFAULT 0;
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_rank TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS host_card_url TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS group_code TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS region TEXT DEFAULT 'eu';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS status TEXT DEFAULT 'waiting';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS game_mode TEXT DEFAULT '';
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS min_rank_tier INTEGER DEFAULT 0;
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS max_rank_tier INTEGER DEFAULT 0;
ALTER TABLE lobbies ADD COLUMN IF NOT EXISTS players JSONB DEFAULT '[]';

ALTER TABLE lobbies ENABLE ROW LEVEL SECURITY;

CREATE POLICY "lobbies_select_all" ON lobbies
  FOR SELECT USING (true);

CREATE POLICY "lobbies_insert_auth" ON lobbies
  FOR INSERT WITH CHECK (true);

CREATE POLICY "lobbies_update_own" ON lobbies
  FOR UPDATE USING (olusturan_uid = auth.uid()::text);

CREATE POLICY "lobbies_delete_own" ON lobbies
  FOR DELETE USING (olusturan_uid = auth.uid()::text);

CREATE POLICY "lobbies_delete_empty" ON lobbies
  FOR DELETE USING (mevcut_oyuncu <= 1);

-- 5. MATCHMAKING QUEUE
CREATE TABLE IF NOT EXISTS matchmaking_queue (
  uid TEXT PRIMARY KEY,
  oyuncu_adi TEXT NOT NULL,
  tag TEXT NOT NULL,
  elo INTEGER DEFAULT 0,
  bolge TEXT DEFAULT 'eu',
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE matchmaking_queue ENABLE ROW LEVEL SECURITY;

CREATE POLICY "queue_select_all" ON matchmaking_queue
  FOR SELECT USING (true);

CREATE POLICY "queue_insert_own" ON matchmaking_queue
  FOR INSERT WITH CHECK (uid = auth.uid()::text);

CREATE POLICY "queue_delete_own" ON matchmaking_queue
  FOR DELETE USING (uid = auth.uid()::text);

-- 6. ROOMS (eşleşen odalar)
CREATE TABLE IF NOT EXISTS rooms (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  oyuncu1_uid TEXT NOT NULL,
  oyuncu2_uid TEXT,
  oyuncu1_adi TEXT,
  oyuncu2_adi TEXT,
  oyuncu1_tag TEXT,
  oyuncu2_tag TEXT,
  grup_kodu TEXT,
  durum TEXT DEFAULT 'bekliyor',
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE rooms ENABLE ROW LEVEL SECURITY;

CREATE POLICY "rooms_select_all" ON rooms
  FOR SELECT USING (true);

CREATE POLICY "rooms_insert_all" ON rooms
  FOR INSERT WITH CHECK (true);

CREATE POLICY "rooms_update_own" ON rooms
  FOR UPDATE USING (oyuncu1_uid = auth.uid()::text OR oyuncu2_uid = auth.uid()::text);

-- 7. ACTIVE USERS (heartbeat/online durumu)
CREATE TABLE IF NOT EXISTS active_users (
  uid TEXT PRIMARY KEY,
  oyuncu_adi TEXT,
  tag TEXT,
  puuid TEXT,
  elo INTEGER DEFAULT 0,
  last_seen TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE active_users ENABLE ROW LEVEL SECURITY;

CREATE POLICY "active_users_select_all" ON active_users
  FOR SELECT USING (true);

CREATE POLICY "active_users_upsert_own" ON active_users
  FOR INSERT WITH CHECK (uid = auth.uid()::text);

CREATE POLICY "active_users_update_own" ON active_users
  FOR UPDATE USING (uid = auth.uid()::text);

CREATE POLICY "active_users_delete_own" ON active_users
  FOR DELETE USING (uid = auth.uid()::text);

-- 8. PROFILE DATA (ek profil verileri)
CREATE TABLE IF NOT EXISTS profile_data (
  uid TEXT PRIMARY KEY,
  email TEXT,
  data JSONB DEFAULT '{}',
  last_login TIMESTAMPTZ,
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE profile_data ENABLE ROW LEVEL SECURITY;

CREATE POLICY "profile_data_select_own" ON profile_data
  FOR SELECT USING (uid = auth.uid()::text);

CREATE POLICY "profile_data_upsert_own" ON profile_data
  FOR INSERT WITH CHECK (uid = auth.uid()::text);

CREATE POLICY "profile_data_update_own" ON profile_data
  FOR UPDATE USING (uid = auth.uid()::text);

-- 9. NOTIFICATIONS (bildirimler)
CREATE TABLE IF NOT EXISTS notifications (
  id TEXT PRIMARY KEY,
  baslik TEXT,
  mesaj TEXT,
  tip TEXT DEFAULT 'info',
  aktif BOOLEAN DEFAULT TRUE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;

CREATE POLICY "notifications_select_all" ON notifications
  FOR SELECT USING (true);

-- 10. APP UPDATES (güncelleme kontrolü)
CREATE TABLE IF NOT EXISTS app_updates (
  id SERIAL PRIMARY KEY,
  version TEXT NOT NULL,
  download_url TEXT,
  changelog TEXT,
  zorunlu BOOLEAN DEFAULT FALSE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

ALTER TABLE app_updates ENABLE ROW LEVEL SECURITY;

CREATE POLICY "updates_select_all" ON app_updates
  FOR SELECT USING (true);

-- 11. CONFIG (uygulama yapılandırması)
CREATE TABLE IF NOT EXISTS app_config (
  key TEXT PRIMARY KEY,
  value TEXT
);

ALTER TABLE app_config ENABLE ROW LEVEL SECURITY;

CREATE POLICY "config_select_all" ON app_config
  FOR SELECT USING (true);

-- 12. ONLINE STATUS (daha ince online takibi)
CREATE TABLE IF NOT EXISTS online_status (
  uid TEXT PRIMARY KEY,
  is_online BOOLEAN DEFAULT FALSE,
  last_seen TIMESTAMPTZ DEFAULT NOW(),
  device_id TEXT
);

ALTER TABLE online_status ENABLE ROW LEVEL SECURITY;

CREATE POLICY "online_select_all" ON online_status
  FOR SELECT USING (true);

CREATE POLICY "online_upsert_own" ON online_status
  FOR INSERT WITH CHECK (uid = auth.uid()::text);

CREATE POLICY "online_update_own" ON online_status
  FOR UPDATE USING (uid = auth.uid()::text);

-- Migration: add ranking fields to active_users for leaderboard
ALTER TABLE active_users ADD COLUMN IF NOT EXISTS current_tier INTEGER DEFAULT 0;
ALTER TABLE active_users ADD COLUMN IF NOT EXISTS card_small_url TEXT DEFAULT '';
ALTER TABLE active_users ADD COLUMN IF NOT EXISTS rutbe TEXT DEFAULT '';

-- Prevent same Riot account appearing multiple times in leaderboard
DELETE FROM active_users a USING active_users b
  WHERE a.uid < b.uid AND a.puuid = b.puuid AND a.puuid IS NOT NULL AND a.puuid != '';
ALTER TABLE active_users ADD CONSTRAINT IF NOT EXISTS unique_puuid UNIQUE (puuid);

-- INDEXES
CREATE INDEX IF NOT EXISTS idx_profiles_puuid ON profiles(puuid);
CREATE INDEX IF NOT EXISTS idx_bans_puuid ON bans(puuid);
CREATE INDEX IF NOT EXISTS idx_lobbies_durum ON lobbies(durum);
CREATE INDEX IF NOT EXISTS idx_active_users_last_seen ON active_users(last_seen);

-- Initial config data
INSERT INTO app_config (key, value) VALUES ('app_version', '1.0.0') ON CONFLICT (key) DO NOTHING;
INSERT INTO app_config (key, value) VALUES ('henrik_api_key', '') ON CONFLICT (key) DO NOTHING;
INSERT INTO app_config (key, value) VALUES ('riot_api_key', '') ON CONFLICT (key) DO NOTHING;

-- Initial update record
INSERT INTO app_updates (version, download_url, changelog, zorunlu) 
VALUES ('1.0.0', '', 'Initial release', false) ON CONFLICT DO NOTHING;
