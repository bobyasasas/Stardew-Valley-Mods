﻿using StardewValley.TerrainFeatures;

namespace Shockah.PredictableRetainingSoil
{
	public interface IPredictableRetainingSoilApi
	{
		#region HoeDirt
		/// <summary>Returns whether the soil has retaining soil on it.</summary>
		bool HasRetainingSoil(HoeDirt soil);

		/// <summary>Returns the type of retaining soil the soil has on it.</summary>
		string? GetRetainingSoilType(HoeDirt soil);

		/// <summary>Returns the number of days until the retaining soil on the soil runs out.</summary>
		int? GetRetainingSoilDaysLeft(HoeDirt soil);

		/// <summary>Sets the number of days until the retaining soil on the soil runs out.</summary>
		/// <remarks>This will only work if the soil actually has retaining soil on it.</remarks>
		void SetRetainingSoilDaysLeft(HoeDirt soil, int days);

		/// <summary>Refreshes the number of days until the retaining soil on the soil runs out (usually when watered).</summary>
		/// <remarks>This will only work if the soil actually has retaining soil on it.</remarks>
		void RefreshRetainingSoilDaysLeft(HoeDirt soil);
		#endregion

		#region Object
		/// <summary>Returns whether the object is a retaining soil.</summary>
		bool IsRetainingSoil(string qualifiedItemId);

		/// <summary>Returns the number of days the retaining soil works for.</summary>
		/// <returns>
		/// `null`: if the object is not a retaining soil.<br />
		/// `-1`: if the retaining soil doesn't run out.<br />
		/// any other int: the number of days the retaining soil works for.
		/// </returns>
		int? GetRetainingSoilDays(string qualifiedItemId);
		#endregion
	}
}