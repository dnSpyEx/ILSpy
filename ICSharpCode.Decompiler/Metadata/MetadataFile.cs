// Copyright (c) 2024 Siegfried Pammer
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

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using dnlib.DotNet;

using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

using IModule = ICSharpCode.Decompiler.TypeSystem.IModule;

namespace ICSharpCode.Decompiler.Metadata
{
	/// <summary>
	/// MetadataFile is the main class the decompiler uses to represent a metadata assembly/module.
	/// Every file on disk can be loaded into a standalone MetadataFile instance.
	///
	/// A MetadataFile can be combined with its referenced assemblies/modules to form a type system,
	/// in that case the <see cref="MetadataModule"/> class is used instead.
	/// </summary>
	/// <remarks>
	/// In addition to wrapping a <c>System.Reflection.Metadata.MetadataReader</c>, this class
	/// contains a few decompiler-specific caches to allow efficiently constructing a type
	/// system from multiple MetadataFiles. This allows the caches to be shared across multiple
	/// decompiled type systems.
	/// </remarks>
	[DebuggerDisplay("{Kind}: {FileName}")]
	public class MetadataFile : TypeSystem.IModuleReference
	{
		public enum MetadataFileKind
		{
			PortableExecutable,
			ProgramDebugDatabase,
			WebCIL,
			Metadata
		}

		public string FileName { get; }
		public MetadataFileKind Kind { get; }
		public ModuleDef Metadata { get; }

		public bool IsAssembly => Metadata.Assembly is not null;

		string? name;

		public string Name {
			get {
				var value = LazyInit.VolatileRead(ref name);
				if (value == null)
				{
					var metadata = Metadata;
					value = Metadata.Assembly is not null
						? metadata.Assembly.Name
						: metadata.Name;
					value = LazyInit.GetOrSet(ref name, value);
				}
				return value;
			}
		}

		PropertyAndEventBackingFieldLookup? propertyAndEventBackingFieldLookup;

		internal PropertyAndEventBackingFieldLookup PropertyAndEventBackingFieldLookup {
			get {
				var r = LazyInit.VolatileRead(ref propertyAndEventBackingFieldLookup);
				if (r != null)
					return r;
				else
					return LazyInit.GetOrSet(ref propertyAndEventBackingFieldLookup, new PropertyAndEventBackingFieldLookup(Metadata));
			}
		}

		public MetadataFile(ModuleDef module)
		{
			Metadata = module;
			FileName = module.Location;
			Kind = MetadataFileKind.PortableExecutable;

		}

		/// <summary>
		/// Finds the top-level-type with the specified name.
		/// </summary>
		public TypeDef? GetTypeDefinition(TopLevelTypeName typeName)
		{
			return Metadata.Find(typeName.ReflectionName, true);
		}

		Dictionary<FullTypeName, ExportedType>? typeForwarderLookup;

		/// <summary>
		/// Finds the type forwarder with the specified name.
		/// </summary>
		public ExportedType? GetTypeForwarder(FullTypeName typeName)
		{
			var lookup = LazyInit.VolatileRead(ref typeForwarderLookup);
			if (lookup == null)
			{
				lookup = new Dictionary<FullTypeName, ExportedType>(Metadata.ExportedTypes.Count);
				foreach (var td in Metadata.ExportedTypes)
				{
					lookup[td.GetFullTypeName()] = td;
				}
				lookup = LazyInit.GetOrSet(ref typeForwarderLookup, lookup);
			}
			if (lookup.TryGetValue(typeName, out var resultHandle))
				return resultHandle;
			else
				return null;
		}

		public IModuleReference WithOptions(TypeSystemOptions options)
		{
			return new MetadataFileWithOptions(this, options);
		}

		IModule IModuleReference.Resolve(ITypeResolveContext context)
		{
			return new MetadataModule(context.Compilation, this, TypeSystemOptions.Default);
		}

		private class MetadataFileWithOptions : IModuleReference
		{
			readonly MetadataFile peFile;
			readonly TypeSystemOptions options;

			public MetadataFileWithOptions(MetadataFile peFile, TypeSystemOptions options)
			{
				this.peFile = peFile;
				this.options = options;
			}

			IModule IModuleReference.Resolve(ITypeResolveContext context)
			{
				return new MetadataModule(context.Compilation, peFile, options);
			}
		}
	}
}
