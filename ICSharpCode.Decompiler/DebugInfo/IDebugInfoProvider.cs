using System;
using System.Collections.Generic;
using System.Text;

using dnlib.DotNet;

namespace ICSharpCode.Decompiler.DebugInfo
{
	public struct Variable
	{
		public Variable(int index, string name)
		{
			Index = index;
			Name = name;
		}

		public int Index { get; }
		public string Name { get; }
	}

	public struct PdbExtraTypeInfo
	{
		public string[] TupleElementNames;
		public bool[] DynamicFlags;
	}

	public interface IDebugInfoProvider
	{
		string Description { get; }
		IList<SequencePoint> GetSequencePoints(MethodDef method);
		IList<Variable> GetVariables(MethodDef method);
		bool TryGetName(MethodDef method, int index, out string name);
		bool TryGetExtraTypeInfo(MethodDef method, int index, out PdbExtraTypeInfo extraTypeInfo);
		string SourceFileName { get; }
	}
}
