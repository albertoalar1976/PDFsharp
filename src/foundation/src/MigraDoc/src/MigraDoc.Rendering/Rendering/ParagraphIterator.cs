// MigraDoc - Creating Documents on the Fly
// See the LICENSE file in the solution root for more information.

using MigraDoc.DocumentObjectModel;

namespace MigraDoc.Rendering
{
    /// <summary>
    /// Iterates sequentially through the elements of a paragraph.
    /// </summary>
    class ParagraphIterator
    {
        /// <summary>
        /// Initializes a paragraph iterator pointing on the given paragraph elements object.
        /// Paragraph iterators received from this paragraph iterator relate to this root node.
        /// </summary>
        /// <param name="rootNode">The root node for the paragraph iterator.</param>
        internal ParagraphIterator(ParagraphElements rootNode)
        {
            _rootNode = rootNode;
            _current = rootNode;

        }

        /// <summary>
        /// Initializes a paragraph iterator given the root node, its position in the object tree and the current object.
        /// </summary>
        /// <param name="rootNode">The node the position indices relate to.</param>
        /// <param name="current">The element the iterator shall point to.</param>
        /// <param name="path">The indices of the elements from the root to the current element, or null for the root.</param>
        ParagraphIterator(ParagraphElements rootNode, DocumentObject current, IndexPath? path)
        {
            _rootNode = rootNode;
            _path = path;
            _current = current;
        }

        /// <summary>
        /// Determines whether this iterator is the first leaf of the root node.
        /// </summary>
        internal bool IsFirstLeaf
        {
            get
            {
                if (_current is not DocumentElements)
                {
                    var prevIter = GetPreviousLeaf();
                    return prevIter == null;
                }
                return false;
            }
        }

        /// <summary>
        /// Determines whether this iterator is the last leaf of the document object tree.
        /// </summary>
        internal bool IsLastLeaf
        {
            get
            {
                if (_current is not DocumentElements)
                {
                    var nextIter = GetNextLeaf();
                    return nextIter == null;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the document object this instance points to.
        /// </summary>
        internal DocumentObject Current => _current;

        /// <summary>
        /// Gets the last leaf of the document object tree.
        /// </summary>
        /// <returns>The paragraph iterator pointing to the last leaf in the document object tree.</returns>
        internal ParagraphIterator? GetLastLeaf()
        {
            if (_rootNode.Count == 0)
                return null;
            return SeekLastLeaf();
        }

        /// <summary>
        /// Gets the first leaf of the element tree.
        /// </summary>
        /// <returns>The paragraph iterator pointing to the first leaf in the element tree.</returns>
        internal ParagraphIterator? GetFirstLeaf()
        {
            if (_rootNode.Count == 0)
                return null;
            return SeekFirstLeaf();
        }

        /// <summary>
        /// Returns the next iterator in the tree pointing to a leaf.
        /// </summary>
        /// <remarks>This function is intended to receive the renderable objects of a paragraph.
        /// Thus, empty ParagraphElement objects (which are collections) don’t count as leafs.</remarks>
        internal ParagraphIterator? GetNextLeaf()
        {
            // Move up to appropriate parent element.
            var parIterator = GetParentIterator();
            if (parIterator == null)
                return null;

            int elementIndex = LastIndex;
            var parEls = (ParagraphElements)parIterator._current;
            while (elementIndex == parEls.Count - 1)
            {
                elementIndex = parIterator.LastIndex;
                parIterator = parIterator.GetParentIterator();
                if (parIterator == null)
                    break;

                parEls = (ParagraphElements)parIterator._current;
            }
            if (parIterator == null)
                return null;
            int newIndex = elementIndex + 1;
            if (newIndex >= parEls.Count)
                return null;

            var indices = new IndexPath(parIterator._path, newIndex);
            var obj = GetNodeObject(parEls[newIndex] ?? NRT.ThrowOnNull<DocumentObject>());
            var iterator = new ParagraphIterator(_rootNode, obj, indices);
            return iterator.SeekFirstLeaf();
        }

        /// <summary>
        /// Gets the object a paragraph iterator shall point to.
        /// Only ParagraphElements and renderable objects are allowed.
        /// </summary>
        /// <param name="obj">The object to select the node object for.</param>
        /// <returns>The object a paragraph iterator shall point to.</returns>
        DocumentObject GetNodeObject(DocumentObject obj)
        {
            if (obj is FormattedText text)
                return text.Elements;
            if (obj is Hyperlink hyperlink)
                return hyperlink.Elements;
            return obj;
        }

        /// <summary>
        /// Returns the previous iterator to a leaf in the document object tree pointing.
        /// </summary>
        /// <returns>The previous leaf, null if none exists.</returns>
        internal ParagraphIterator? GetPreviousLeaf()
        {
            // Move up to appropriate parent element.
            var parIterator = GetParentIterator();
            if (parIterator == null)
                return null;

            int elementIndex = LastIndex;
            var parEls = (ParagraphElements)parIterator._current;
            while (elementIndex == 0)
            {
                elementIndex = parIterator.LastIndex;
                parIterator = parIterator.GetParentIterator();
                if (parIterator == null)
                    break;

                parEls = (ParagraphElements)parIterator._current;
            }
            if (parIterator == null)
                return null;

            int newIndex = elementIndex - 1;
            if (newIndex < 0)
                return null;

            var indices = new IndexPath(parIterator._path, newIndex);

            var obj = GetNodeObject(parEls[newIndex] ?? NRT.ThrowOnNull<DocumentObject>());
            var iterator = new ParagraphIterator(_rootNode, obj, indices);
            return iterator.SeekLastLeaf();
        }

        ParagraphIterator SeekLastLeaf()  // TODO_OLD NOTE ReSharper has a bug here (use original code). Introducing pattern variable breaks the code. Should report that to JetBrains.
        {
            var obj = Current;
            if (obj is not ParagraphElements)
                return this;

            var indices = _path;
            while (obj is ParagraphElements elements)
            {
                if (elements.Count == 0)
                    return new ParagraphIterator(_rootNode, elements, indices);

                int idx = elements.Count - 1;
                indices = new IndexPath(indices, idx);
                obj = GetNodeObject(elements[idx] ?? NRT.ThrowOnNull<DocumentObject>());
            }
            return new ParagraphIterator(_rootNode, obj, indices);
        }

        /// <summary>
        /// Gets the leftmost leaf within the hierarchy.
        /// </summary>
        /// <returns>The searched leaf.</returns>
        ParagraphIterator SeekFirstLeaf() // TODO_OLD NOTE ReSharper has a bug here (see above).
        {
            var obj = Current;
            if (obj is not ParagraphElements)
                return this;

            var indices = _path;
            while (obj is ParagraphElements parEls)
            {
                if (parEls.Count == 0)
                    return new ParagraphIterator(_rootNode, obj, indices);

                indices = new IndexPath(indices, 0);
                obj = GetNodeObject(parEls[0] ?? NRT.ThrowOnNull<DocumentObject>());
            }
            return new ParagraphIterator(_rootNode, obj, indices);
        }

        ParagraphIterator? GetParentIterator()
        {
            if (_path == null)
                return null;

            var parent = DocumentRelations.GetParentOfType(_current, typeof(ParagraphElements))
                ?? NRT.ThrowOnNull<DocumentObject>();
            return new ParagraphIterator(_rootNode, parent, _path.Parent);
        }

        int LastIndex
        {
            get
            {
                return _path?.Index ?? -1;
            }
        }

        readonly ParagraphElements _rootNode;

        /// <summary>
        /// The indices of the elements from the root to the current element, or null for the root.
        /// </summary>
        readonly IndexPath? _path;

        readonly DocumentObject _current;

        /// <summary>
        /// An immutable path of element indices. Each step shares the path of its parent. Formerly, every
        /// iterator step copied a List&lt;int&gt; of all indices, which was the largest source of allocations
        /// when rendering large tables.
        /// </summary>
        sealed class IndexPath(IndexPath? parent, int index)
        {
            public IndexPath? Parent { get; } = parent;
            public int Index { get; } = index;
        }
    }
}
