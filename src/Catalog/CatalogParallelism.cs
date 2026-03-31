// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETFRAMEWORK
using System.Net;
#endif

namespace NuGet.Services.Metadata.Catalog
{
	/// <summary>
	/// Provides a cross-platform parallelism degree setting.
	/// On .NET Framework, delegates to <see cref="System.Net.ServicePointManager.DefaultConnectionLimit"/>.
	/// On modern .NET, uses a standalone static property.
    /// TODO: remove this class and propagate parallelism settings across the codebase instead.
	/// </summary>
	public static class CatalogParallelism
	{
		private const int DefaultDegree = 10;

#if NETFRAMEWORK
		public static int Degree
		{
			get => ServicePointManager.DefaultConnectionLimit;
			set => ServicePointManager.DefaultConnectionLimit = value;
		}
#else
		public static int Degree { get; set; } = DefaultDegree;
#endif
	}
}

