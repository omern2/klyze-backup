using System;
using System.Collections.Generic;
using System.Windows.Media;
using Newtonsoft.Json;
using ValorantAutoClicker.Helpers;

namespace ValorantAutoClicker.Models
{
    // ─── Tracker.gg API Response Root ───────────────────────────────────────────

    public class TrackerProfileResponse
    {
        [JsonProperty("data")]
        public TrackerProfileData Data { get; set; }

        [JsonProperty("errors")]
        public List<TrackerError> Errors { get; set; }
    }

    // standard endpoint için alias — aynı yapı
    public class TrackerStandardProfileResponse
    {
        [JsonProperty("data")]
        public TrackerStandardData Data { get; set; }

        [JsonProperty("errors")]
        public List<TrackerError> Errors { get; set; }
    }

    // TrackerStandardData = TrackerProfileData ile aynı yapı
    public class TrackerStandardData
    {
        [JsonProperty("platformInfo")]
        public TrackerPlatformInfo PlatformInfo { get; set; }

        [JsonProperty("segments")]
        public List<TrackerSegment> Segments { get; set; }
    }

    public class TrackerError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class TrackerProfileData
    {
        [JsonProperty("platformInfo")]
        public TrackerPlatformInfo PlatformInfo { get; set; }

        [JsonProperty("userInfo")]
        public TrackerUserInfo UserInfo { get; set; }

        [JsonProperty("metadata")]
        public TrackerProfileMetadata Metadata { get; set; }

        [JsonProperty("segments")]
        public List<TrackerSegment> Segments { get; set; }

        [JsonProperty("availableSegments")]
        public List<TrackerAvailableSegment> AvailableSegments { get; set; }

        [JsonProperty("expiryDate")]
        public DateTime? ExpiryDate { get; set; }
    }

    public class TrackerPlatformInfo
    {
        [JsonProperty("platformSlug")]
        public string PlatformSlug { get; set; }

        [JsonProperty("platformUserId")]
        public string PlatformUserId { get; set; }

        [JsonProperty("platformUserHandle")]
        public string PlatformUserHandle { get; set; }

        [JsonProperty("platformUserIdentifier")]
        public string PlatformUserIdentifier { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }
    }

    public class TrackerUserInfo
    {
        [JsonProperty("userId")]
        public object UserId { get; set; }

        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }

        [JsonProperty("isVerified")]
        public bool IsVerified { get; set; }

        [JsonProperty("isInfluencer")]
        public bool IsInfluencer { get; set; }

        [JsonProperty("isPartner")]
        public bool IsPartner { get; set; }

        [JsonProperty("countryCode")]
        public string CountryCode { get; set; }

        [JsonProperty("customAvatarUrl")]
        public string CustomAvatarUrl { get; set; }

        [JsonProperty("customHeroUrl")]
        public string CustomHeroUrl { get; set; }

        [JsonProperty("socialAccounts")]
        public List<object> SocialAccounts { get; set; }

        [JsonProperty("pageviews")]
        public int Pageviews { get; set; }
    }

    public class TrackerProfileMetadata
    {
        [JsonProperty("currentSeason")]
        public int CurrentSeason { get; set; }

        [JsonProperty("activeShard")]
        public string ActiveShard { get; set; }

        [JsonProperty("schema")]
        public int Schema { get; set; }
    }

    public class TrackerAvailableSegment
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("attributes")]
        public TrackerSegmentAttributes Attributes { get; set; }
    }

    // ─── Segment ────────────────────────────────────────────────────────────────

    public class TrackerSegment
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("attributes")]
        public TrackerSegmentAttributes Attributes { get; set; }

        [JsonProperty("metadata")]
        public TrackerSegmentMetadata Metadata { get; set; }

        [JsonProperty("expiryDate")]
        public DateTime? ExpiryDate { get; set; }

        [JsonProperty("stats")]
        public Dictionary<string, TrackerStat> Stats { get; set; }
    }

    public class TrackerSegmentAttributes
    {
        [JsonProperty("season")]
        public object Season { get; set; }

        [JsonProperty("playlist")]
        public string Playlist { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
    }

    public class TrackerSegmentMetadata
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("tallImageUrl")]
        public string TallImageUrl { get; set; }

        [JsonProperty("backgroundImageUrl")]
        public string BackgroundImageUrl { get; set; }

        [JsonProperty("portraitImageUrl")]
        public string PortraitImageUrl { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("schema")]
        public int Schema { get; set; }

        [JsonProperty("actId")]
        public string ActId { get; set; }

        [JsonProperty("actName")]
        public string ActName { get; set; }

        [JsonProperty("seasonId")]
        public string SeasonId { get; set; }

        [JsonProperty("seasonName")]
        public string SeasonName { get; set; }
    }

    // ─── Stat Value ─────────────────────────────────────────────────────────────

    public class TrackerStat
    {
        [JsonProperty("rank")]
        public int? Rank { get; set; }

        [JsonProperty("percentile")]
        public double? Percentile { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("displayCategory")]
        public string DisplayCategory { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("metadata")]
        public TrackerStatMetadata Metadata { get; set; }

        [JsonProperty("value")]
        public double? Value { get; set; }

        [JsonProperty("displayValue")]
        public string DisplayValue { get; set; }

        [JsonProperty("displayType")]
        public string DisplayType { get; set; }
    }

    public class TrackerStatMetadata
    {
        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; }

        [JsonProperty("tierName")]
        public string TierName { get; set; }

        [JsonProperty("divisionName")]
        public string DivisionName { get; set; }

        [JsonProperty("actId")]
        public string ActId { get; set; }

        [JsonProperty("actName")]
        public string ActName { get; set; }

        [JsonProperty("seasonId")]
        public string SeasonId { get; set; }

        [JsonProperty("seasonName")]
        public string SeasonName { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    // ─── Match History Response ──────────────────────────────────────────────────

    public class TrackerMatchesResponse
    {
        [JsonProperty("data")]
        public TrackerMatchesData Data { get; set; }

        [JsonProperty("errors")]
        public List<TrackerError> Errors { get; set; }
    }

    public class TrackerMatchesData
    {
        [JsonProperty("matches")]
        public List<TrackerMatch> Matches { get; set; }

        [JsonProperty("metadata")]
        public TrackerMatchesMetadata Metadata { get; set; }
    }

    public class TrackerMatchesMetadata
    {
        [JsonProperty("next")]
        public string Next { get; set; }
    }

    public class TrackerMatch
    {
        [JsonProperty("attributes")]
        public TrackerMatchAttributes Attributes { get; set; }

        [JsonProperty("metadata")]
        public TrackerMatchMetadata Metadata { get; set; }

        [JsonProperty("segments")]
        public List<TrackerMatchSegment> Segments { get; set; }

        [JsonProperty("streams")]
        public object Streams { get; set; }
    }

    public class TrackerMatchAttributes
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class TrackerMatchMetadata
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty("gameStart")]
        public DateTime GameStart { get; set; }

        [JsonProperty("gameLength")]
        public int GameLength { get; set; }

        [JsonProperty("queue")]
        public TrackerQueue Queue { get; set; }

        [JsonProperty("seasonId")]
        public string SeasonId { get; set; }

        [JsonProperty("map")]
        public TrackerMap Map { get; set; }

        [JsonProperty("isRanked")]
        public bool IsRanked { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("cluster")]
        public string Cluster { get; set; }

        [JsonProperty("modeKey")]
        public string ModeKey { get; set; }

        [JsonProperty("modeName")]
        public string ModeName { get; set; }

        [JsonProperty("modeImageUrl")]
        public string ModeImageUrl { get; set; }
    }

    public class TrackerQueue
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    public class TrackerMap
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }
    }

    public class TrackerMatchSegment
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("attributes")]
        public TrackerMatchSegmentAttributes Attributes { get; set; }

        [JsonProperty("metadata")]
        public TrackerMatchSegmentMetadata Metadata { get; set; }

        [JsonProperty("expiryDate")]
        public DateTime? ExpiryDate { get; set; }

        [JsonProperty("stats")]
        public Dictionary<string, TrackerStat> Stats { get; set; }
    }

    public class TrackerMatchSegmentAttributes
    {
        [JsonProperty("platformUserIdentifier")]
        public string PlatformUserIdentifier { get; set; }
    }

    public class TrackerMatchSegmentMetadata
    {
        [JsonProperty("hasWon")]
        public bool HasWon { get; set; }

        [JsonProperty("agent")]
        public TrackerAgent Agent { get; set; }

        [JsonProperty("team")]
        public string Team { get; set; }
    }

    public class TrackerAgent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    // ─── Processed / View Models ─────────────────────────────────────────────────

    /// <summary>Tracker.gg'den işlenmiş oyuncu profili</summary>
    public class TrackerPlayerProfile
    {
        public string RiotId { get; set; }          // "TenZ#000"
        public string GameName { get; set; }
        public string TagLine { get; set; }
        public string AvatarUrl { get; set; }
        public int AccountLevel { get; set; }

        // Rank
        public string CurrentRankName { get; set; }
        public string CurrentRankIconUrl { get; set; }
        public int CurrentRankRating { get; set; }   // RR puanı
        public string PeakRankName { get; set; }
        public string PeakRankIconUrl { get; set; }

        public ImageSource RankIkonKaynak => RankIkonHelper.RankIkon(CurrentRankName);
        public ImageSource PeakRankIkonKaynak => RankIkonHelper.RankIkon(PeakRankName);

        // Genel istatistikler
        public double KdRatio { get; set; }
        public double WinRate { get; set; }
        public double Acs { get; set; }
        public double HeadshotPct { get; set; }
        public int TotalMatches { get; set; }
        public double TotalPlaytimeHours { get; set; }
        public double DamagePerRound { get; set; }
        public double KillsPerMatch { get; set; }

        // Ajan istatistikleri (en çok oynanan 5)
        public List<TrackerAgentStat> TopAgents { get; set; } = new();

        // Son maçlar
        public List<TrackerMatchSummary> RecentMatches { get; set; } = new();

        // Önbellekten mi yüklendi?
        public bool FromCache { get; set; }
        public DateTime FetchedAt { get; set; } = DateTime.Now;
    }

    public class TrackerAgentStat
    {
        public string AgentName { get; set; }
        public string AgentImageUrl { get; set; }
        public int MatchesPlayed { get; set; }
        public double WinRate { get; set; }
        public double KdRatio { get; set; }
        public double Acs { get; set; }
        public double HeadshotPct { get; set; }
        public double DamagePerRound { get; set; }
    }

    public class TrackerMatchSummary
    {
        public string MatchId { get; set; }
        public string MapName { get; set; }
        public string MapImageUrl { get; set; }
        public string ModeName { get; set; }
        public DateTime GameStart { get; set; }
        public int GameLengthSeconds { get; set; }
        public bool HasWon { get; set; }
        public string AgentName { get; set; }
        public string AgentImageUrl { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double Acs { get; set; }
        public double HeadshotPct { get; set; }
        public int RoundsWon { get; set; }
        public int RoundsLost { get; set; }

        public string KdaDisplay => $"{Kills}/{Deaths}/{Assists}";
        public string ScoreDisplay => $"{RoundsWon} - {RoundsLost}";
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - GameStart;
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dakika önce";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat önce";
                return $"{(int)diff.TotalDays} gün önce";
            }
        }
    }
}
