// Copyright (c) 2018 Siegfried Pammer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Text.RegularExpressions;
using dnlib.DotNet;

using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.Decompiler.Metadata
{
	public static class DotNetCorePathFinderExtensions
	{
		static readonly string PathPattern =
			@"(Reference Assemblies[/\\]Microsoft[/\\]Framework[/\\](?<type>.NETFramework)[/\\]v(?<version>[^/\\]+)[/\\])" +
			@"|((?<type>Microsoft\.NET)[/\\]assembly[/\\]GAC_(MSIL|32|64)[/\\])" +
			@"|((?<type>Microsoft\.NET)[/\\]Framework(64)?[/\\](?<version>[^/\\]+)[/\\])" +
			@"|(NuGetFallbackFolder[/\\](?<type>[^/\\]+)\\(?<version>[^/\\]+)([/\\].*)?[/\\]ref[/\\])" +
			@"|(shared[/\\](?<type>[^/\\]+)\\(?<version>[^/\\]+)([/\\].*)?[/\\])" +
			@"|(packs[/\\](?<type>[^/\\]+)\\(?<version>[^/\\]+)\\ref([/\\].*)?[/\\])";

		static readonly string RefPathPattern =
			@"(Reference Assemblies[/\\]Microsoft[/\\]Framework[/\\](?<type>.NETFramework)[/\\]v(?<version>[^/\\]+)[/\\])" +
			@"|(NuGetFallbackFolder[/\\](?<type>[^/\\]+)\\(?<version>[^/\\]+)([/\\].*)?[/\\]ref[/\\])" +
			@"|(packs[/\\](?<type>[^/\\]+)\\(?<version>[^/\\]+)\\ref([/\\].*)?[/\\])";

		public static string DetectTargetFrameworkId(this ModuleDef module, string assemblyPath = null)
		{
			if (module.Assembly != null) {
				if (module.Assembly.TryGetOriginalTargetFrameworkAttribute(out var fw, out var version2, out var profile)) {
					if (profile is null)
						return fw + ",Version=v" + version2;
					return fw + ",Version=v" + version2 + ",Profile=" + profile;
				}

				switch (module.Assembly.Name)
				{
					case "mscorlib":
						return $".NETFramework,Version=v{module.Assembly.Version.ToString(2)}";
					case "netstandard":
						return $".NETStandard,Version=v{module.Assembly.Version.ToString(2)}";
					case "System.Runtime":
					case "System.Private.CoreLib":
					{
						string version = GetDotNetCoreVersion(module.Assembly.Version);
						if (version != null)
						{
							return $".NETCoreApp,Version=v{version}";
						}
						else
						{
							break;
						}
					}
				}
			}

			foreach (var r in module.GetAssemblyRefs())
			{
				try
				{
					if (r.PublicKeyOrToken.IsNullOrEmpty)
						continue;
					string version;
					switch (r.Name)
					{
						case "mscorlib":
							version = r.Version.ToString(2);
							return $".NETFramework,Version=v{version}";
						case "System.Runtime":
						case "System.Private.CoreLib":
							version = GetDotNetCoreVersion(r.Version);
							if (version != null)
							{
								return $".NETCoreApp,Version=v{version}";
							}
							else
							{
								continue;
							}
					}
				}
				catch (BadImageFormatException)
				{
					// ignore malformed references
				}
			}

			// We check for netstandard separately because .NET Core/Framework assemblies can reference it.
			foreach (var r in module.GetAssemblyRefs())
			{
				try
				{
					if (r.PublicKeyOrToken.IsNullOrEmpty || r.Name != "netstandard")
						continue;

					string version = r.Version.ToString(2);
					return $".NETStandard,Version=v{version}";
				}
				catch (BadImageFormatException)
				{
					// ignore malformed references
				}
			}

			// Optionally try to detect target version through assembly path as a fallback (use case: reference assemblies)
			if (assemblyPath != null)
			{
				/*
				 * Detected path patterns (examples):
				 *
				 * - .NETFramework -> C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\mscorlib.dll
				 * - .NETCore      -> C:\Program Files\dotnet\sdk\NuGetFallbackFolder\microsoft.netcore.app\2.1.0\ref\netcoreapp2.1\System.Console.dll
				 * - .NETStandard  -> C:\Program Files\dotnet\sdk\NuGetFallbackFolder\netstandard.library\2.0.3\build\netstandard2.0\ref\netstandard.dll
				 */
				var pathMatch = Regex.Match(assemblyPath, PathPattern,
					RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture);
				if (pathMatch.Success)
				{
					var type = pathMatch.Groups["type"].Value;
					var version = pathMatch.Groups["version"].Value;
					if (string.IsNullOrEmpty(version))
						version = module.RuntimeVersion;

					if (type == "Microsoft.NET" || type == ".NETFramework")
					{
						return $".NETFramework,Version=v{version.TrimStart('v').Substring(0, 3)}";
					}
					else if (type.IndexOf("netcore", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return $".NETCoreApp,Version=v{version}";
					}
					else if (type.IndexOf("netstandard", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return $".NETStandard,Version=v{version}";
					}
				}
				else
				{
					var version = module.RuntimeVersion;
					if (string.IsNullOrEmpty(version))
						version = "4.0";
					version = version.TrimStart('v');
					return $".NETFramework,Version=v{version.Substring(0, Math.Min(3, version.Length))}";
				}
			}

			return string.Empty;
		}

		static string GetDotNetCoreVersion(Version assemblyVersion)
		{
			// System.Runtime.dll and System.Private.CoreLib.dll use the following scheme:
			// 4.1.0 => .NET Core 1.0 / 1.1
			// 4.2.0 => .NET Core 2.0
			// 4.2.1 => .NET Core 2.1 / 3.0
			// 4.2.2 => .NET Core 3.1
			// 5.0.0+ => .NET 5+
			return (assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build) switch {
				(4, 1, 0) => "1.1",
				(4, 2, 0) => "2.0",
				(4, 2, 1) => "3.0",
				(4, 2, 2) => "3.1",
				( >= 5, _, _) => assemblyVersion.ToString(2),
				_ => null
			};
		}

		public static bool IsReferenceAssembly(this MetadataFile assembly)
		{
			return IsReferenceAssembly(assembly.Metadata, assembly.FileName);
		}

		public static bool IsReferenceAssembly(this ModuleDef metadata, string assemblyPath)
		{
			if (metadata == null)
				throw new ArgumentNullException(nameof(metadata));

			if (metadata.Assembly.CustomAttributes.HasKnownAttribute(KnownAttribute.ReferenceAssembly))
				return true;

			// Try to detect reference assembly through specific path pattern
			var refPathMatch = Regex.Match(assemblyPath, RefPathPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
			return refPathMatch.Success;
		}

		public static string DetectRuntimePack(this MetadataFile assembly)
		{
			if (assembly is null)
			{
				throw new ArgumentNullException(nameof(assembly));
			}

			var metadata = assembly.Metadata;

			foreach (var reference in metadata.GetAssemblyRefs())
			{
				if (reference.PublicKeyOrToken.IsNullOrEmpty)
					continue;

				if ((reference.Name == "WindowsBase"))
				{
					return "Microsoft.WindowsDesktop.App";
				}

				if ((reference.Name == "PresentationFramework"))
				{
					return "Microsoft.WindowsDesktop.App";
				}

				if ((reference.Name == "PresentationCore"))
				{
					return "Microsoft.WindowsDesktop.App";
				}

				// TODO add support for ASP.NET Core
			}

			return "Microsoft.NETCore.App";
		}
	}
}
