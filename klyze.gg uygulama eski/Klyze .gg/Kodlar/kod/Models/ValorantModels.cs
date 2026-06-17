using System;
using System.Collections.Generic;
using System.Linq;

namespace ValorantAutoClicker.Models
{
    #region Riot API Response Models

    public class ValorantAccountResponse
    {
        public string Puuid { get; set; }
        public string GameName { get; set; }
        public string TagLine { get; set; }
        public string Region { get; set; }
        public string Platform { get; set; }
        public string PlatformId { get; set; }
        public string AccountLevel { get; set; }
    }

    public class ValorantMatchReference
    {
        public string MatchId { get; set; }
        public long GameStartTimestamp { get; set; }
        public string MapId { get; set; }
        public string Mode { get; set; }
        public string QueueId { get; set; }
        public string SeasonId { get; set; }
    }

    public class ValorantMatchDetail
    {
        public Metadata Metadata { get; set; }
        public Info Info { get; set; }
    }

    public class Metadata
    {
        public string MatchId { get; set; }
        public List<string> Participants { get; set; }
    }

    public class Info
    {
        public string Map { get; set; }
        public string Mode { get; set; }
        public long GameStart { get; set; }
        public long GameLength { get; set; }
        public List<PlayerStats> Players { get; set; }
        public List<TeamStats> Teams { get; set; }
    }

    public class PlayerStats
    {
        public string Puuid { get; set; }
        public string RiotIdGameName { get; set; }
        public string RiotIdTagline { get; set; }
        public string Character { get; set; }
        public string TeamId { get; set; }
        public bool Win { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Bodyshots { get; set; }
        public int Headshots { get; set; }
        public int Legshots { get; set; }
        public int Score { get; set; }
        public int CombatScore { get; set; }
        public RoundStats Stats { get; set; }
        public Economy Economy { get; set; }
        public List<Ability> AbilityCasts { get; set; }
    }

    public class RoundStats
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Bodyshots { get; set; }
        public int Headshots { get; set; }
        public int Legshots { get; set; }
    }

    public class Economy
    {
        public int LoadoutValue { get; set; }
        public int Money { get; set; }
        public int Spent { get; set; }
        public int Residual { get; set; }
        public int Cone { get; set; }
    }

    public class Ability
    {
        public string AbilitySlot { get; set; }
        public int Casts { get; set; }
        public int Kills { get; set; }
    }

    public class TeamStats
    {
        public string TeamId { get; set; }
        public bool Win { get; set; }
        public int RoundsWon { get; set; }
        public int RoundsLost { get; set; }
        public int PlantRound { get; set; }
        public int DefuseRound { get; set; }
        public int AttackRoundWins { get; set; }
        public int DefenseRoundWins { get; set; }
    }

    #endregion

    #region View Models

    public class ValorantMatchSummary
    {
        public string MatchId { get; set; }
        public string Map { get; set; }
        public string Mode { get; set; }
        public DateTime GameStart { get; set; }
        public TimeSpan GameLength { get; set; }
        public bool Win { get; set; }
        public string Agent { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double Kda => (Kills + Assists) / Math.Max(1, Deaths);
        public double Acs { get; set; } // Average Combat Score
        public int Headshots { get; set; }
        public int Bodyshots { get; set; }
        public int Legshots { get; set; }
        public double HeadshotPercentage => Headshots > 0 ? (double)Headshots / (Kills > 0 ? Kills : 1) * 100 : 0;
    }

    public class ValorantPlayerStats
    {
        public string Puuid { get; set; }
        public string GameName { get; set; }
        public string TagLine { get; set; }
        public int AccountLevel { get; set; }
        public int TotalMatches { get; set; }
        public int Wins { get; set; }
        public double WinRate => TotalMatches > 0 ? (double)Wins / TotalMatches * 100 : 0;
        public double AverageKda { get; set; }
        public double AverageAcs { get; set; }
        public double AverageHeadshotPercentage { get; set; }
        public List<ValorantMatchSummary> RecentMatches { get; set; } = new();
        public Dictionary<string, AgentPerformance> AgentPerformance { get; set; } = new();
        public Dictionary<string, MapPerformance> MapPerformance { get; set; } = new();
        public WeeklyActivity WeeklyActivity { get; set; } = new();
    }

    public class AgentPerformance
    {
        public string AgentName { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public double WinRate => MatchesPlayed > 0 ? (double)Wins / MatchesPlayed * 100 : 0;
        public double AverageKda { get; set; }
        public double AverageAcs { get; set; }
    }

    public class MapPerformance
    {
        public string MapName { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public double WinRate => MatchesPlayed > 0 ? (double)Wins / MatchesPlayed * 100 : 0;
        public double AverageKda { get; set; }
        public double AverageAcs { get; set; }
    }

    public class WeeklyActivity
    {
        public int Monday { get; set; }
        public int Tuesday { get; set; }
        public int Wednesday { get; set; }
        public int Thursday { get; set; }
        public int Friday { get; set; }
        public int Saturday { get; set; }
        public int Sunday { get; set; }
        public int TotalMatches => Monday + Tuesday + Wednesday + Thursday + Friday + Saturday + Sunday;
        public string MostActiveDay => new Dictionary<string, int>
        {
            ["Monday"] = Monday,
            ["Tuesday"] = Tuesday,
            ["Wednesday"] = Wednesday,
            ["Thursday"] = Thursday,
            ["Friday"] = Friday,
            ["Saturday"] = Saturday,
            ["Sunday"] = Sunday
        }.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
    }

    public class CrosshairCorrelationData
    {
        public string CrosshairProfile { get; set; }
        public int MatchesWithProfile { get; set; }
        public double WinRateWithProfile { get; set; }
        public double AverageKdaWithProfile { get; set; }
        public double AverageAcsWithProfile { get; set; }
    }

    #endregion
}