﻿using Newtonsoft.Json;
using Shockah.Kokoro.Stardew;
using StardewValley;
using System;

namespace Shockah.EarlyGingerIsland
{
	public struct UnlockCondition : IComparable<UnlockCondition>
	{
		public int Year { get; set; }
		public int SeasonIndex { get; set; }
		public int DayOfMonth { get; set; }
		public int HeartsWithWilly { get; init; }

		[JsonIgnore]
		public WorldDate Date
			=> WorldDateExt.New(Year, SeasonIndex, DayOfMonth);

		public UnlockCondition(int year, int seasonIndex, int dayOfMonth, int heartsWithWilly)
		{
			this.Year = year;
			this.SeasonIndex = seasonIndex;
			this.DayOfMonth = dayOfMonth;
			this.HeartsWithWilly = heartsWithWilly;
		}

		public UnlockCondition(WorldDate date, int heartsWithWilly)
			: this(date.Year, date.SeasonIndex, date.DayOfMonth, heartsWithWilly)
		{
		}

		public void Deconstruct(out WorldDate date, out int heartsWithWilly)
		{
			date = Date;
			heartsWithWilly = HeartsWithWilly;
		}

		public int CompareTo(UnlockCondition other)
		{
			if (Date.TotalDays != other.Date.TotalDays)
				return Date.TotalDays.CompareTo(other.Date.TotalDays);
			if (HeartsWithWilly != other.HeartsWithWilly)
				return HeartsWithWilly.CompareTo(other.HeartsWithWilly);
			return 0;
		}
	}
}