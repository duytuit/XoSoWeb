using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class LotteryReferencePageViewModel
    {
        public string PageKind { get; set; }
        public string Title { get; set; }
        public string ScopeName { get; set; }
        public string ScopePath { get; set; }
        public string ParentPath { get; set; }
        public string ParentLabel { get; set; }
        public string Description { get; set; }
        public DateTime Today { get; set; }
        public int? WeekdayFilter { get; set; }
        public string StationFilter { get; set; }
        public string GameFilter { get; set; }
        public string MissingInformation { get; set; }
        public DateTime LastChecked { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string TicketPrice { get; set; }
        public string TicketUnit { get; set; }
        public List<LotteryReferenceScheduleRow> ScheduleRows { get; set; } = new List<LotteryReferenceScheduleRow>();
        public List<LotteryReferenceScheduleException> ScheduleExceptions { get; set; } = new List<LotteryReferenceScheduleException>();
        public List<LotteryReferencePrizeRow> PrizeRows { get; set; } = new List<LotteryReferencePrizeRow>();
        public List<LotteryPrizeStructureVersion> PrizeVersions { get; set; } = new List<LotteryPrizeStructureVersion>();
        public List<LotteryReferenceSource> Sources { get; set; } = new List<LotteryReferenceSource>();
        public List<LotteryReferenceGameLink> GameLinks { get; set; } = new List<LotteryReferenceGameLink>();
        public List<string> Stations { get; set; } = new List<string>();
    }

    public class LotteryReferenceScheduleRow
    {
        public string RegionOrGame { get; set; }
        public string Station { get; set; }
        public int? Weekday { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? CompletionTime { get; set; }
        public string Frequency { get; set; }
        public string Note { get; set; }
        public string ResultPath { get; set; }
    }

    public class LotteryReferencePrizeRow
    {
        public string PrizeName { get; set; }
        public string Quantity { get; set; }
        public string MatchDigits { get; set; }
        public string Value { get; set; }
        public string Condition { get; set; }
        public string ValueKind { get; set; }
        public string JackpotType { get; set; }
        public string SpecialNumberRole { get; set; }
        public string SharingRule { get; set; }
        public string Version { get; set; }
    }

    public class LotteryReferenceScheduleException
    {
        public DateTime Date { get; set; }
        public string ChangeType { get; set; }
        public string Note { get; set; }
        public string SourceUrl { get; set; }
    }

    public class LotteryPrizeStructureVersion
    {
        public string Version { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string SourceUrl { get; set; }
        public DateTime CheckedOn { get; set; }
        public List<LotteryReferencePrizeRow> Prizes { get; set; } = new List<LotteryReferencePrizeRow>();
    }

    public class LotteryReferenceSource
    {
        public string Label { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
    }

    public class LotteryReferenceGameLink
    {
        public string Name { get; set; }
        public string Slug { get; set; }
    }
}
