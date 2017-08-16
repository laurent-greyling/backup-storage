using System.Collections.Generic;
using System.Linq;

namespace Final.BackupTool.Common.Strategy
{
    public class Batch
    {
        public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> source, int chunksize)
        {
            while (source.Any())
            {
                yield return source.Take(chunksize);
                source = source.Skip(chunksize);
            }
        }
    }
}