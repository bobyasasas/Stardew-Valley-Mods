﻿using StardewModdingAPI;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shockah.ProjectFluent
{
	internal class FileResolvingFluent : IFluent<string>
	{
		private IList<IFluent<string>> Wrapped { get; set; }

		public FileResolvingFluent(IEnumerable<(string name, ContextfulFluentFunction function)> functions, IMonitor monitor, IGameLocale locale, IEnumerable<string> filePathCandidates, IFluent<string> fallback)
		{
			Wrapped = filePathCandidates
				.Where(path => File.Exists(path))
				.Select(path => new FileFluent(functions, monitor, locale, path, fallback))
				.DefaultIfEmpty(fallback)
				.ToList();
		}

		public bool ContainsKey(string key)
		{
			foreach (var fluent in Wrapped)
				if (fluent.ContainsKey(key))
					return true;
			return false;
		}

		public string Get(string key, object? tokens)
		{
			foreach (var fluent in Wrapped)
				if (fluent.ContainsKey(key))
					return fluent.Get(key, tokens);
			return Wrapped.Last().Get(key, tokens);
		}
	}
}