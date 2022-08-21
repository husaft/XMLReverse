using System.Collections.Generic;

// ReSharper disable CollectionNeverQueried.Global

namespace XMLReverse.Lib
{
    public class PathStat
    {
        public SortedDictionary<int, long> NodeFreq { get; } = new();

        public SortedDictionary<string, long> AttrFreq { get; } = new();
    }
}