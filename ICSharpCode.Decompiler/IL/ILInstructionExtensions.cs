#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.Decompiler.IL
{
	internal static class ILInstructionExtensions
	{
		public static T WithILRange<T>(this T target, ILInstruction sourceInstruction) where T : ILInstruction
		{
			target.AddILRange(sourceInstruction);
			return target;
		}

		public static T WithILRange<T>(this T target, Interval range) where T : ILInstruction
		{
			target.AddILRange(range);
			return target;
		}

		public static ILInstruction? GetNextSibling(this ILInstruction? instruction)
		{
			if (instruction?.Parent == null)
				return null;
			if (instruction.ChildIndex + 1 >= instruction.Parent.Children.Count)
				return null;
			return instruction.Parent.Children[instruction.ChildIndex + 1];
		}

		public static bool IsPrefixed(this ILInstruction instruction)
		{
			if (instruction is CallInstruction callInstruction)
				return callInstruction.IsTail;
			if (instruction is LdElema ldElema)
				return ldElema.IsReadOnly;
			if (instruction is ISupportsUnalignedPrefix unalignedPrefix)
				return unalignedPrefix.UnalignedPrefix != 0;
			if (instruction is ISupportsVolatilePrefix volatilePrefix)
				return volatilePrefix.IsVolatile;
			return false;
		}
	}
}
