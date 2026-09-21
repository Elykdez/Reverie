using System;
using System.Threading;
using System.Threading.Tasks;
using Gsplat;
using UnityEngine;

namespace Hypocycloid.Reverie.Splats
{
    public static class SplatChunkPacking
    {
        const int BatchSize = 8192;

        public static async Task LoadFromDecodedAsync(GsplatAssetSpark asset,
            GsplatDecodedData data, CancellationToken token)
        {
            if (!asset) throw new ArgumentNullException(nameof(asset));
            if (data == null) throw new ArgumentNullException(nameof(data));
            await Task.Run(data.Validate, token);
            token.ThrowIfCancellationRequested();

            asset.SplatCount = checked((uint)data.Count);
            asset.SHBands = data.SHBands;
            asset.Antialiased = data.Antialiased;
            asset.Bounds = data.Bounds;
            asset.Allocate();

            int coefficients = GsplatUtils.SHBandsToCoefficientCount(data.SHBands);
            var batch = new GsplatDecodedData(Math.Min(BatchSize, data.Count),
                data.SHBands, data.Antialiased);
            var scratch = ScriptableObject.CreateInstance<GsplatAssetSpark>();
            scratch.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                for (int offset = 0; offset < data.Count; offset += BatchSize)
                {
                    token.ThrowIfCancellationRequested();
                    int count = Math.Min(BatchSize, data.Count - offset);
                    if (batch.Count != count)
                        batch = new GsplatDecodedData(count, data.SHBands, data.Antialiased);
                    batch.Bounds = data.Bounds;
                    Array.Copy(data.Positions, offset, batch.Positions, 0, count);
                    Array.Copy(data.Scales, offset, batch.Scales, 0, count);
                    Array.Copy(data.Rotations, offset, batch.Rotations, 0, count);
                    Array.Copy(data.Colors, offset, batch.Colors, 0, count);
                    Array.Copy(data.SHs, offset * coefficients, batch.SHs, 0, count * coefficients);

                    // The pinned vendor packer only writes managed fields/arrays and uses math
                    // functions. This private scratch asset is never rendered or inspected while
                    // its worker runs; creation, publication and destruction stay on Unity's thread.
                    await Task.Run(() => scratch.LoadFromDecoded(batch), token);
                    token.ThrowIfCancellationRequested();
                    Array.Copy(scratch.PackedSplats, 0, asset.PackedSplats, offset, count);
                    if (data.SHBands >= 1)
                        Array.Copy(scratch.PackedSH1, 0, asset.PackedSH1, offset * 2, count * 2);
                    if (data.SHBands >= 2)
                        Array.Copy(scratch.PackedSH2, 0, asset.PackedSH2, offset * 4, count * 4);
                    if (data.SHBands >= 3)
                        Array.Copy(scratch.PackedSH3, 0, asset.PackedSH3, offset * 4, count * 4);
                    if (data.SHBands >= 4)
                        Array.Copy(scratch.PackedSH4, 0, asset.PackedSH4, offset * 4, count * 4);
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(scratch);
                else UnityEngine.Object.DestroyImmediate(scratch);
            }
        }
    }
}
