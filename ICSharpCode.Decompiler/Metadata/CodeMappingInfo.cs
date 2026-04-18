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

using System.Collections.Generic;

using dnlib.DotNet;

namespace ICSharpCode.Decompiler.Metadata
{
	/// <summary>
	/// Describes which parts of the (compiler-generated) code belong to which user code.
	/// A part could be:
	/// - the body (method) of a lambda.
	/// - the MoveNext method of async/yield state machines.
	/// </summary>
	public class CodeMappingInfo
	{
		/// <summary>
		/// The module containing the code.
		/// </summary>
		public MetadataFile Module { get; }

		/// <summary>
		/// The (parent) TypeDef containing the code.
		/// </summary>
		public TypeDef TypeDefinition { get; }

		readonly Dictionary<MethodDef, List<MethodDef>> parts;
		readonly Dictionary<MethodDef, MethodDef> parents;

		/// <summary>
		/// Creates a <see cref="CodeMappingInfo"/> instance using the given <paramref name="type"/>.
		/// </summary>
		public CodeMappingInfo(TypeDef type)
		{
			this.TypeDefinition = type;
			this.parts = new Dictionary<MethodDef, List<MethodDef>>();
			this.parents = new Dictionary<MethodDef, MethodDef>();
		}

		/// <summary>
		/// Returns all parts of a method.
		/// A method has at least one part, that is, the method itself.
		/// If no parts are found, only the method itself is returned.
		/// </summary>
		public IEnumerable<MethodDef> GetMethodParts(MethodDef method)
		{
			if (parts.TryGetValue(method, out var p))
				return p;
			return new[] { method };
		}

		/// <summary>
		/// Returns the parent of a part.
		/// The parent is usually the "calling method" of lambdas, async and yield state machines.
		/// The "calling method" has itself as parent.
		/// If no parent is found, the method itself is returned.
		/// </summary>
		public MethodDef GetParentMethod(MethodDef method)
		{
			if (parents.TryGetValue(method, out var p))
				return p;
			return method;
		}

		/// <summary>
		/// Adds a bidirectional mapping between <paramref name="parent"/> and <paramref name="part"/>.
		/// </summary>
		public void AddMapping(MethodDef parent, MethodDef part)
		{
			//Debug.Print("Parent: " + MetadataTokens.GetRowNumber(parent) + " Part: " + MetadataTokens.GetRowNumber(part));
			if (parents.ContainsKey(part))
				return;
			parents.Add(part, parent);
			if (!parts.TryGetValue(parent, out var list)) {
				list = new List<MethodDef>();
				parts.Add(parent, list);
			}
			list.Add(part);
		}
	}
}
