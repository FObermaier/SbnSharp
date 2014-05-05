﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpSbn
{
    public class SbnNode : IEnumerable<SbnFeature>
    {
        private readonly SbnTree _tree;
        private byte xmin, xmax, ymin, ymax;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tree">The tree this node belongs to</param>
        /// <param name="nid">The node's id</param>
        public SbnNode(SbnTree tree, int nid)
        {
            _tree = tree;
            Nid = nid;
            Full = nid == 1 || nid >= _tree.FirstLeafNodeId;
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="tree">The tree this node belongs to</param>
        /// <param name="nid">The node's id</param>
        /// <param name="minx">The lower x-ordinate</param>
        /// <param name="miny">The lower y-ordinate</param>
        /// <param name="maxx">The upper x-ordinate</param>
        /// <param name="maxy">The upper y-ordinate</param>
        public SbnNode(SbnTree tree, int nid, byte minx, byte miny, byte maxx, byte maxy)
            :this(tree, nid)
        {
            xmin = minx;
            ymin = miny;
            xmax = maxx;
            ymax = maxy;
        }

        /// <summary>
        /// Method to add a bin to this node
        /// </summary>
        /// <param name="addBin"></param>
        internal void AddBin(SbnBin addBin)
        {
            if (FirstBin == null)
                FirstBin = addBin;
            else
            {
                var bin = FirstBin;
                while (bin.Next != null)
                    bin = bin.Next;
                bin.Next = addBin;
            }
        }

        /// <summary>
        /// Gets the id of the current node
        /// </summary>
        public int Nid { get; private set; }

        /// <summary>
        /// The first bin associated with this node
        /// </summary>
        internal SbnBin FirstBin { get; set; }

        /// <summary>
        /// The last bin associated with this node
        /// </summary>
        internal SbnBin LastBin
        {
            get
            {
                if (FirstBin == null)
                    return null;

                var res = FirstBin;
                while (res.Next != null)
                    res = res.Next;
                return res;
            }
        }

        /// <summary>
        /// Gets the parent of this node
        /// </summary>
        public SbnNode Parent
        {
            get
            {
                if (Nid == 1)
                    return null;

                var firstSiblingId = Nid - Nid % 2;
                return _tree.Nodes[firstSiblingId / 2];
            }
        }

        /// <summary>
        /// Gets the first child of this node
        /// </summary>
        public SbnNode Child1
        {
            get
            {
                if (Nid >= _tree.FirstLeafNodeId)
                    return null;
                return _tree.Nodes[Nid * 2];
            }
        }

        /// <summary>
        /// Gets the second child of this node
        /// </summary>
        public SbnNode Child2
        {
            get
            {
                if (Nid >= _tree.FirstLeafNodeId)
                    return null;
                return _tree.Nodes[Nid * 2 + 1];
            }
        }

        /// <summary>
        /// Gets the sibling of this node
        /// </summary>
        public SbnNode Sibling
        {
            get
            {
                if (Nid == 1)
                    return null;

                if (Nid - Nid % 2 == Nid)
                    return _tree.Nodes[Nid + 1];
                return _tree.Nodes[Nid - 1];
            }
        }

        /// <summary>
        /// Property to indicate that the node is full, it has had more than 8 features once and was then split
        /// </summary>
        internal bool Full { get; private set; }

        /// <summary>
        /// Gets the node's level
        /// </summary>
        internal int Level { get { return (int) Math.Log(Nid, 2) + 1; }}

        /// <summary>
        /// Gets the number of features in this node
        /// </summary>
        public int FeatureCount
        {
            get
            {
                if (FirstBin == null)
                    return 0;

                var count = 0;
                var bin = FirstBin;
                while (bin != null)
                {
                    count += bin.NumFeatures;
                    bin = bin.Next;
                }
                return count;
            }
        }

        /// <summary>
        /// Add the child nodes
        /// </summary>
        public void AddChildren()
        {
            if (Nid >= _tree.FirstLeafNodeId) return;

            var splitBounds = GetSplitBounds(1);
            var childId = Nid*2;
            _tree.Nodes[childId] = new SbnNode(_tree, childId++, splitBounds[0], splitBounds[1], splitBounds[2], splitBounds[3]);
            Child1.AddChildren();

            splitBounds = GetSplitBounds(2);
            _tree.Nodes[childId] = new SbnNode(_tree, childId, splitBounds[0], splitBounds[1], splitBounds[2], splitBounds[3]);
            Child2.AddChildren();
        }

        /// <summary>
        /// Compute the split ordinate for a given <paramref name="splitAxis"/>
        /// </summary>
        /// <param name="splitAxis">The axis</param>
        /// <returns>The ordinate</returns>
        private byte GetSplitOridnate(int splitAxis)
        {
            var mid = (splitAxis == 1)
                ? /*(int)*/ (byte)((xmin + xmax) / 2.0 + 1)
                : /*(int)*/ (byte)((ymin + ymax) / 2.0 + 1);

            return (byte) (mid - mid%2);
        }

        /// <summary>
        /// Get the bounds for one of the child nodes
        /// </summary>
        /// <param name="childIndex">The index of the child node</param>
        /// <returns>The split bounds</returns>
        private byte[] GetSplitBounds(int childIndex)
        {
            var splitAxis = Level % 2;// == 1 ? 'x' : 'y';

            var mid = GetSplitOridnate(splitAxis);

            var res = new[] {xmin, ymin, xmax, ymax};
            switch (splitAxis)
            {
                case 1: // x-ordinate
                    switch (childIndex)
                    {
                        case 1:
                            res[0] = (byte)(mid + 1);
                            break;
                        case 2:
                            res[2] = mid;
                            break;
                    }
                    break;
                case 0: // y-ordinate
                    switch (childIndex)
                    {
                        case 1:
                            res[1] = (byte)(mid + 1);
                            break;
                        case 2:
                            res[3] = mid;
                            break;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("childIndex");
            }

            return res;
        }

        /// <summary>
        /// function to count all features in this node and all child nodes
        /// </summary>
        /// <returns>The number of features in all this and all child nodes</returns>
        public int CountAllFeatures()
        {
            var res = FeatureCount;
            if (Nid < _tree.FirstLeafNodeId)
                res += Child1.CountAllFeatures() + Child2.CountAllFeatures();
            return res;
        }

        /// <summary>
        /// Method to query all the ids of features in this node that intersect the box defined
        /// by <paramref name="minx"/>, <paramref name="miny"/>, <paramref name="maxx"/>
        /// and <paramref name="maxy"/> 
        /// </summary>
        /// <param name="minx">The lower x-ordinate</param>
        /// <param name="miny">The lower y-ordinate</param>
        /// <param name="maxx">The upper x-ordinate</param>
        /// <param name="maxy">The upper y-ordinate</param>
        /// <returns>An enumeration of feature ids</returns>
        public IEnumerable<uint> QueryFids(byte minx, byte miny, byte maxx, byte maxy)
        {
            if (ContainedBy(minx, miny, maxx, maxy))
                return GetAllFidsInNode();

            var fids = new List<uint>();
            foreach (var feature in this)
            {
                if (feature.Intersects(minx, maxx, miny, maxy))
                    fids.Add(feature.Fid);
            }

            if (Nid < _tree.FirstLeafNodeId)
            {
                fids.AddRange(Child1.QueryFids(minx, miny, maxx, maxy));
                fids.AddRange(Child2.QueryFids(minx, miny, maxx, maxy));
            }
            return fids;
        }

        /// <summary>
        /// Helper method to get all the ids of features in this node
        /// </summary>
        /// <returns>An enumeration of feature ids</returns>
        private IEnumerable<uint> GetAllFidsInNode()
        {
            var res = new List<uint>();
            var bin = FirstBin;
            while (bin != null)
            {
                res.AddRange(bin.GetAllFidsInBin());
                bin = bin.Next;
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("[Node {0}: ({1}-{2},{3}-{4})/{5}]", Nid, xmin, xmax, ymin, ymax, GetSplitOridnate(Level%2));
        }

        /// <summary>
        /// Intersection predicate function
        /// </summary>
        /// <param name="minX">lower x-ordinate</param>
        /// <param name="minY">lower y-ordinate</param>
        /// <param name="maxX">upper x-ordinate</param>
        /// <param name="maxY">upper y-ordinate</param>
        /// <returns><value>true</value> if this node's bounding box intersect with the bounding box defined by <paramref name="minX"/>, <paramref name="maxX"/>, <paramref name="minY"/> and <paramref name="maxY"/>, otherwise <value>false</value></returns>
        internal bool Intersects(byte minX, byte minY, byte maxX, byte maxY)
        {
            return !(minX > xmax || maxX < xmin || minY > ymax || maxY < ymin);
        }

        /// <summary>
        /// Contains predicate function
        /// </summary>
        /// <param name="minX">lower x-ordinate</param>
        /// <param name="minY">lower y-ordinate</param>
        /// <param name="maxX">upper x-ordinate</param>
        /// <param name="maxY">upper y-ordinate</param>
        /// <returns><value>true</value> if this node's bounding box contains the bounding box defined by <paramref name="minX"/>, <paramref name="maxX"/>, <paramref name="minY"/> and <paramref name="maxY"/>, otherwise <value>false</value></returns>
        internal bool Contains(byte minX, byte minY, byte maxX, byte maxY)
        {
            return minX >= xmin && maxX <= xmax &&
                   minY >= ymin && maxY <= ymax;
        }

        /// <summary>
        /// Contains predicate function
        /// </summary>
        /// <param name="minX">lower x-ordinate</param>
        /// <param name="minY">lower y-ordinate</param>
        /// <param name="maxX">upper x-ordinate</param>
        /// <param name="maxY">upper y-ordinate</param>
        /// <returns><value>true</value> if this node's bounding box contains the bounding box defined by <paramref name="minX"/>, <paramref name="maxX"/>, <paramref name="minY"/> and <paramref name="maxY"/>, otherwise <value>false</value></returns>
        internal bool ContainedBy(byte minX, byte minY, byte maxX, byte maxY)
        {
            return xmin >= minX && xmax <= maxX &&
                   xmin >= minY && xmax <= minY;
        }
        public IEnumerator<SbnFeature> GetEnumerator()
        {
            return new SbnFeatureEnumerator(FirstBin);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Private helper class to enumerate feature ids
        /// </summary>
        private class SbnFeatureEnumerator : IEnumerator<SbnFeature>
        {
            private SbnBin _firstBin;
            private SbnBin _currentBin;

            private int _index = -1;
            public SbnFeatureEnumerator(SbnBin firstBin)
            {
                if (firstBin != null)
                    _firstBin = firstBin.Clone();
            }

            public void Dispose()
            {
                _firstBin = null;
            }

            public bool MoveNext()
            {
                // We don't have a bin at all!
                if (_firstBin == null) return false;

                // We were resetted or havn't started
                if (_index == -1)
                    _currentBin = _firstBin;

                // did we reach the end!
                if (_index  == _currentBin.NumFeatures)
                    return false;

                // Increment
                _index++;

                // Did we reach the end of the bin now? 
                if (_index == 100)
                {
                    //If so move to next
                    _currentBin = _currentBin.Next;
                    //was there another one?
                    if (_currentBin == null) return false;
                    _index = 0;
                }

                return _index < _currentBin.NumFeatures;
            }

            public void Reset()
            {
                _index = -1;
                _currentBin = null;
            }

            public SbnFeature Current
            {
                get { return _index == -1 ? new SbnFeature() : _currentBin[_index]; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }

        public bool VerifyBins()
        {
#if DEBUG
            foreach (var feature in this)
            {
                if (!Contains(feature.MinX, feature.MinY, feature.MaxX, feature.MaxY))
                    return false;
            }
#endif
            return true;
        }

        /// <summary>
        /// Method to insert a feature at this node
        /// </summary>
        /// <param name="feature">The feature to add</param>
        public void Insert(SbnFeature feature)
        {
            // if this is leaf, just take the feature
            if (Nid >= _tree.FirstLeafNodeId)
            {
                AddFeature(feature);
                return;
            }

            // it takes 8 features to split a node
            // so we'll hold 8 features first
            if (Nid > 1)
            {
                if (!Full)
                {
                    
                    if (FeatureCount < 8)
                    {
                        AddFeature(feature);
                        return;
                    }
                    if (FeatureCount == 8)
                    {
                        var bin = FirstBin;
                        FirstBin = new SbnBin();
                        Full = true;
                        bin.AddFeature(feature);
                        for (var i = 0; i < 9; i ++)
                        {
                            Insert(bin[i]);
                        }
                        return;
                    }
                }

            }

            // The node is split so we can sort features
            int min, max; //, smin, smax;
            var splitAxis = Level%2;
            if (splitAxis == 1)
            {
                min = feature.MinX;
                max = feature.MaxX;
                //smin = feature.MinY;
                //smax = feature.MaxY;
            }
            else
            {
                min = feature.MinY;
                max = feature.MaxY;
                //smin = feature.MinX;
                //smax = feature.MaxX;
            }
            var seam = GetSplitOridnate(splitAxis);

            // Grab features on the seam we can't split
            if (min <= seam && max > seam)
            {
                AddFeature(feature);
            }

            else if (min < seam)
                Child2.Insert(feature);
            else
                Child1.Insert(feature);
        }

        /// <summary>
        /// Method to actually add a feature to this node
        /// </summary>
        /// <param name="feature"></param>
        private void AddFeature(SbnFeature feature)
        {
            if (FeatureCount % 100 == 0)
                AddBin(new SbnBin());

            var addBin = FirstBin;
            while (addBin.NumFeatures == 100)
                addBin = addBin.Next;

            addBin.AddFeature(feature);
        }

#if VERBOSE
        public string ToStringVerbose()
        {
            return string.Format("{0,5} {1,4}-{2,4} {3,4}-{4,4} {5} {6,4} {7,1}", Nid, xmin, xmax, ymin, ymax, Full ? 1 : 0, Full ? FeatureCount : 0, Full ? 0 : FeatureCount);
        }
#endif

    }
}