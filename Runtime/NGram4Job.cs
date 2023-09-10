using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
namespace Kurisu.NGram
{
    /// <summary>
    /// Multi-thread 2~4 Gram implement using Job System
    /// </summary>
    [BurstCompile]
    public struct NGram4Job : IJob
    {
        #region Job ReadOnly Properties
        [ReadOnly]
        public NativeArray<byte> History;
        [ReadOnly]
        public int NGram;
        [ReadOnly]
        public NativeArray<byte> Inference;
        #endregion
        public NativeArray<int> Result;
        [BurstCompile]
        public void Execute()
        {
            int count = History.Length - NGram + 1;
            var predictions = new NativeHashMap<int, int>(count, Allocator.Temp);
            //Dictionary<Pattern,OccurenceKey>
            var worldOccurence = new NativeMultiHashMap<int, int>(count, Allocator.Temp);
            //Dictionary<OccurenceKey,OccurenceCount>
            var occurenceMap = new NativeHashMap<int, int>(count, Allocator.Temp);
            Compression(worldOccurence, occurenceMap);
            BuildPrediction(worldOccurence, occurenceMap, predictions);
            TryInference(predictions);
            predictions.Dispose();
            worldOccurence.Dispose();
            occurenceMap.Dispose();
        }
        //Compress byte[] to int32 (4 bytes)
        [BurstCompile]
        private void Compression(
            NativeMultiHashMap<int, int> worldOccurence,
            NativeHashMap<int, int> occurenceMap
         )
        {
            var buffer = new NativeArray<byte>(4, Allocator.Temp);
            int count = History.Length - NGram + 1;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    buffer[j] = (j < NGram - 1) ? History[i + j] : (byte)0;
                }
                int key = buffer.Reinterpret<int>(UnsafeUtility.SizeOf<byte>())[0];
                buffer[3] = History[i + 3];
                int occurenceKey = buffer.Reinterpret<int>(UnsafeUtility.SizeOf<byte>())[0];
                if (occurenceMap.TryGetValue(occurenceKey, out int occurences))
                {
                    occurenceMap[occurenceKey] = occurences + 1;
                }
                else
                {
                    occurenceMap[occurenceKey] = 1;
                    worldOccurence.Add(key, occurenceKey);
                }
            }
            buffer.Dispose();
        }
        [BurstCompile]
        private void TryInference(NativeHashMap<int, int> predictions)
        {
            var buffer = new NativeArray<byte>(4, Allocator.Temp);
            for (int i = 0; i < 4; i++)
            {
                buffer[i] = (i < NGram - 1) ? Inference[^(NGram - 1 - i)] : (byte)0;
            }
            int key = buffer.Reinterpret<int>(UnsafeUtility.SizeOf<byte>())[0];
            buffer.Dispose();
            if (predictions.TryGetValue(key, out int result))
            {
                Result[0] = result;
            }
            else
            {
                Result[0] = -1;
            }
        }
        [BurstCompile]
        private readonly void BuildPrediction(
             NativeMultiHashMap<int, int> worldOccurence,
            NativeHashMap<int, int> occurenceMap,
            NativeHashMap<int, int> predictions
        )
        {
            var keys = worldOccurence.GetKeyArray(Allocator.Temp);
            foreach (var start in keys)
            {
                int prediction = -1;
                int maximum = 0;
                foreach (var end in worldOccurence.GetValuesForKey(start))
                {
                    if (occurenceMap[end] > maximum)
                    {
                        prediction = end;
                        maximum = occurenceMap[end];
                    }
                }
                predictions[start] = prediction;
            }
            keys.Dispose();
        }
    }
}