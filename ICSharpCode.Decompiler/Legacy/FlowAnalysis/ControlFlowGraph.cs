// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.Collections.Generic;
using System.Threading;

namespace ICSharpCode.Decompiler.Legacy.FlowAnalysis {
	/// <summary>
	/// Contains the control flow graph.
	/// </summary>
	/// <remarks>Use ControlFlowGraph builder to create instances of the ControlFlowGraph.</remarks>
	public sealed class ControlFlowGraph
	{
		readonly List<ControlFlowNode> nodes;

		public ControlFlowNode EntryPoint => nodes[0];

		public ControlFlowNode RegularExit => nodes[1];

		public ControlFlowNode ExceptionalExit => nodes[2];

		public List<ControlFlowNode> Nodes => nodes;

		internal ControlFlowGraph() => this.nodes = new List<ControlFlowNode>();

		/// <summary>
		/// Resets "Visited" to false for all nodes in this graph.
		/// </summary>
		public void ResetVisited() {
			for (int i = 0; i < nodes.Count; i++)
				nodes[i].Visited = false;
		}

		/// <summary>
		/// Computes the dominator tree.
		/// </summary>
		public void ComputeDominance(CancellationToken cancellationToken = default(CancellationToken))
		{
			// A Simple, Fast Dominance Algorithm
			// Keith D. Cooper, Timothy J. Harvey and Ken Kennedy

			EntryPoint.ImmediateDominator = EntryPoint;
			bool changed = true;
			while (changed) {
				changed = false;
				ResetVisited();

				cancellationToken.ThrowIfCancellationRequested();

				// for all nodes b except the entry point
				EntryPoint.TraversePreOrder(
					b => b.Successors,
					b => {
						if (b != EntryPoint) {
							ControlFlowNode newIdom = null;
							for (int i = 0; i < b.Incoming.Count; i++) {
								var block = b.Incoming[i].Source;
								if (block.Visited && block != b) {
									newIdom = block;
									break;
								}
							}
							if (newIdom == null)
								throw new InvalidOperationException();
							// for all other predecessors p of b
							for (int i = 0; i < b.Incoming.Count; i++) {
								var p = b.Incoming[i].Source;
								if (p != b && p.ImmediateDominator != null) {
									newIdom = FindCommonDominator(p, newIdom);
								}
							}
							if (b.ImmediateDominator != newIdom) {
								b.ImmediateDominator = newIdom;
								changed = true;
							}
						}
					});
			}
			EntryPoint.ImmediateDominator = null;
			for (int i = 0; i < nodes.Count; i++) {
				var node = nodes[i];
				node.ImmediateDominator?.DominatorTreeChildren.Add(node);
			}
		}

		readonly HashSet<ControlFlowNode> FindCommonDominator_path1 = new HashSet<ControlFlowNode>();
		ControlFlowNode FindCommonDominator(ControlFlowNode b1, ControlFlowNode b2)
		{
			// Here we could use the postorder numbers to get rid of the hashset, see "A Simple, Fast Dominance Algorithm"
			FindCommonDominator_path1.Clear();
			while (b1 != null && FindCommonDominator_path1.Add(b1))
				b1 = b1.ImmediateDominator;
			while (b2 != null) {
				if (FindCommonDominator_path1.Contains(b2))
					return b2;
				b2 = b2.ImmediateDominator;
			}
			throw new Exception("No common dominator found!");
		}

		/// <summary>
		/// Computes dominance frontiers.
		/// This method requires that the dominator tree is already computed!
		/// </summary>
		public void ComputeDominanceFrontier()
		{
			ResetVisited();

			EntryPoint.TraversePostOrder(
				b => b.DominatorTreeChildren,
				n => {
					//logger.WriteLine("Calculating dominance frontier for " + n.Name);
					n.DominanceFrontier = new HashSet<ControlFlowNode>();
					// DF_local computation
					for (int i = 0; i < n.Outgoing.Count; i++) {
						var succ = n.Outgoing[i].Target;
						if (succ.ImmediateDominator != n) {
							//logger.WriteLine("  local: " + succ.Name);
							n.DominanceFrontier.Add(succ);
						}
					}
					// DF_up computation
					for (int i = 0; i < n.DominatorTreeChildren.Count; i++) {
						var child = n.DominatorTreeChildren[i];
						foreach (ControlFlowNode p in child.DominanceFrontier) {
							if (p.ImmediateDominator != n) {
								//logger.WriteLine("  DF_up: " + p.Name + " (child=" + child.Name);
								n.DominanceFrontier.Add(p);
							}
						}
					}
				});
		}
	}
}
