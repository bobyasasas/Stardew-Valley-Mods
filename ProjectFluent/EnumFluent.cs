﻿using System;
using System.Collections.Generic;

namespace Shockah.ProjectFluent
{
	internal class EnumFluent<EnumType>: IEnumFluent<EnumType> where EnumType: Enum
	{
		private readonly IFluent<string> Wrapped;
		private readonly string KeyPrefix;

		public EnumFluent(IFluent<string> wrapped, string keyPrefix)
		{
			if (!typeof(EnumType).IsEnum)
				throw new ArgumentException($"{typeof(EnumType)} is not an enum.");
			this.Wrapped = wrapped;
			this.KeyPrefix = keyPrefix;
		}

		private string GetUnderlyingKey(EnumType key)
			=> $"{KeyPrefix}{Enum.GetName(typeof(EnumType), key)}";

		public bool ContainsKey(EnumType key)
			=> Wrapped.ContainsKey(GetUnderlyingKey(key));

		public string Get(EnumType key, object? tokens)
			=> Wrapped.Get(GetUnderlyingKey(key), tokens);

		public EnumType GetFromLocalizedName(string localizedName)
		{
			foreach (var value in Enum.GetValues(typeof(EnumType)))
			{
				var valueLocalizedName = ((IFluent<EnumType>)this)[(EnumType)value];
				if (valueLocalizedName == localizedName)
					return (EnumType)value;
			}
			throw new ArgumentException($"{typeof(EnumType)} is not an enum.");
		}

		public IEnumerable<string> GetAllLocalizedNames()
		{
			if (!typeof(EnumType).IsEnum)
				throw new ArgumentException($"{typeof(EnumType)} is not an enum.");
			foreach (var value in Enum.GetValues(typeof(EnumType)))
				yield return ((IFluent<EnumType>)this)[(EnumType)value];
		}
	}
}