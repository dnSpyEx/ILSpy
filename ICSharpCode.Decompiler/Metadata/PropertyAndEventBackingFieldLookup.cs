// Copyright (c) 2025 Siegfried Pammer
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

using System.Collections.Generic;

using dnlib.DotNet;

using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.Metadata
{
	class PropertyAndEventBackingFieldLookup
	{
		private readonly ModuleDef metadata;
		private readonly Dictionary<FieldDef, PropertyDef> propertyLookup
			= new();
		private readonly Dictionary<FieldDef, EventDef> eventLookup
			= new();

		public PropertyAndEventBackingFieldLookup(ModuleDef metadata)
		{
			this.metadata = metadata;

			var nameToFieldMap = new MultiDictionary<string, FieldDef>();

			HashSet<string> eventNames = new();

			foreach (var type in metadata.GetTypes())
			{
				foreach (var field in type.Fields)
				{
					var name = (field.Name);
					nameToFieldMap.Add(name, field);
				}

				foreach (var property in type.Properties)
				{
					var name = (property.Name);
					// default C# property backing field name is "<PropertyName>k__BackingField"
					if (nameToFieldMap.TryGetValues($"<{name}>k__BackingField", out var fieldHandles))
					{
						foreach (var fieldHandle in fieldHandles)
						{
							propertyLookup[fieldHandle] = property;
						}
					}
					else if (nameToFieldMap.TryGetValues($"_{name}", out fieldHandles))
					{
						foreach (var fieldHandle in fieldHandles)
						{
							if (fieldHandle.IsCompilerGenerated())
							{
								propertyLookup[fieldHandle] = property;
							}
						}
					}
				}

				// first get all names of events defined, so that we can make sure we don't accidentally
				// associate the wrong backing field with the event, in case there is an event called "Something"
				// without a backing field (i.e., custom event) as well as an auto/field event called "SomethingEvent"
				// declared in the same type.
				foreach (var ev in type.Events)
				{
					eventNames.Add((ev.Name));
				}

				foreach (var ev in type.Events)
				{
					var name = (ev.Name);
					if (nameToFieldMap.TryGetValues(name, out var fieldHandles))
					{
						foreach (var fieldHandle in fieldHandles)
						{
							eventLookup[fieldHandle] = ev;
						}
					}
					else
					{
						var nameWithSuffix = $"{name}Event";
						if (!eventNames.Contains(nameWithSuffix) && nameToFieldMap.TryGetValues(nameWithSuffix, out fieldHandles))
						{
							foreach (var fieldHandle in fieldHandles)
							{
								eventLookup[fieldHandle] = ev;
							}
						}
					}
				}

				eventNames.Clear();
				nameToFieldMap.Clear();
			}
		}

		public bool IsPropertyBackingField(FieldDef field, out PropertyDef handle)
		{
			return propertyLookup.TryGetValue(field, out handle);
		}

		public bool IsEventBackingField(FieldDef field, out EventDef handle)
		{
			return eventLookup.TryGetValue(field, out handle);
		}
	}
}
