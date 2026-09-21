using System;
using System.Collections.Generic;
using Gsplat;
using Unity.Mathematics;
using UnityEngine;

namespace Hypocycloid.Splats
{
    // UnitySplats has no opacity threshold. Pruning selects the passing splats through
    // GsplatRenderer.SetActiveRanges, leaving the package and the source data unchanged.
    public static class SplatOpacity
    {
        public static long CountPassing(
            GsplatAsset asset,
            IReadOnlyList<GsplatActiveRange> ranges,
            float threshold
        ) => Scan(asset, ranges, threshold, null);

        /// <summary>Appends the maximal runs of passing splats, in source order.</summary>
        public static void AppendPassing(
            GsplatAsset asset,
            IReadOnlyList<GsplatActiveRange> ranges,
            float threshold,
            List<GsplatActiveRange> output
        ) => Scan(asset, ranges, threshold, output);

        // Match source opacity as the package shaders unpack it, before antialiasing or pixel falloff.
        static long Scan(
            GsplatAsset asset,
            IReadOnlyList<GsplatActiveRange> ranges,
            float threshold,
            List<GsplatActiveRange> runs
        )
        {
            long count = 0;
            if (threshold <= 0f)
            {
                foreach (var range in ranges)
                {
                    count += range.Count;
                    runs?.Add(range);
                }
                return count;
            }
            uint4[] packed = null;
            Vector4[] colors = null;
            if (asset is GsplatAssetSpark spark)
                packed = spark.PackedSplats;
            else if (asset is GsplatAssetUncompressed uncompressed)
                colors = uncompressed.Colors;
            else
                throw new NotSupportedException(
                    "Opacity pruning requires Spark or uncompressed splats."
                );
            foreach (var range in ranges)
            {
                uint start = range.Offset;
                uint end = range.Offset + range.Count;
                for (uint i = range.Offset; i < end; ++i)
                {
                    float opacity = packed != null
                        ? (packed[i].x >> 24) * (1f / 255f)
                        : colors[i].w;
                    if (opacity >= threshold)
                    {
                        ++count;
                        continue;
                    }
                    if (runs != null && i > start)
                        runs.Add(new GsplatActiveRange(start, i - start));
                    start = i + 1;
                }
                if (runs != null && end > start)
                    runs.Add(new GsplatActiveRange(start, end - start));
            }
            return count;
        }
    }
}
