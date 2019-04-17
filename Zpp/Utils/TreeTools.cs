using System.Collections.Generic;
using System.Linq;
using Master40.DB.DataModel;

namespace Zpp.Utils
{
    public static class TreeTools<Node>
    {
        // 
        // TODO: This must be revisited under following aspect: a article node can be existing multiple times in tree,
        // it must be ensured, that every multiple existing object have its own instance
        ///
        /// <summary>
        ///     A depth-first-search (DFS) traversal of given tree
        /// </summary>
        /// <param name="tree">to traverse</param>
        /// <returns>
        ///    The List of the traversed nodes in exact order
        /// </returns>
        public static List<Node> traverseDepthFirst(ITree<Node> tree)
        {
            var stack = new Stack<Node>();
            
            Dictionary<Node, bool> discovered = new Dictionary<Node, bool>();
            List<Node> traversed = new List<Node>();
            
            stack.Push(tree.getRootNode());
            while (stack.Any())
            {
                Node poppedNode = stack.Pop();
                traversed.Add(poppedNode);
                
                // init dict if node not yet exists
                if (! discovered.ContainsKey(poppedNode) )
                {
                    discovered[poppedNode] = false;
                } 
                
                // if node is not discovered
                if (! discovered[poppedNode] )
                {
                    discovered[poppedNode] = true;
                    foreach (Node node in tree.getChildNodes(poppedNode))
                    {
                        stack.Push(node);
                    }
                }
            }
            return traversed;
        }
    }
}