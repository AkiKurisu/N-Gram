using System;
using Unity.Collections;
using Unity.Jobs;
namespace Kurisu.NGram
{
    /// <summary>
    /// An example N-Gram resolver for 4-Gram
    /// </summary>
    public class NGram4Resolver : INGramResolver, IDisposable
    {
        private NativeArray<byte>? history;
        private NativeArray<byte>? inference;
        public bool Success { get; private set; }
        public byte Result { get; private set; }
        private JobHandle jobHandle;
        private NativeArray<int>? result;
        public void Dispose()
        {
            history?.Dispose();
            inference?.Dispose();
        }
        public void Resolve(byte[] history, byte[] inference)
        {
            Resolve(history, inference, 0, history.Length);
        }
        public void Resolve(byte[] history, byte[] inference, int historyStartIndex, int historyLength)
        {
            result?.Dispose();
            result = new NativeArray<int>(1, Allocator.TempJob);
            this.history?.Dispose();
            var historyArray = new NativeArray<byte>(historyLength, Allocator.TempJob);
            for (int i = 0; i < historyLength; i++)
            {
                historyArray[i] = history[i + historyStartIndex];
            }
            this.history = historyArray;
            this.inference?.Dispose();
            this.inference = new NativeArray<byte>(inference, Allocator.TempJob);
            jobHandle = new NGram4Job()
            {
                History = this.history.Value,
                Inference = this.inference.Value,
                Result = result.Value,
                NGram = 4
            }.Schedule();
        }
        public void Complete()
        {
            jobHandle.Complete();
            //[first][second][third][predict]
            Success = result.Value[0] >= 0;
            if (Success)
                Result = BitConverter.GetBytes(result.Value[0])[3];
            result.Value.Dispose();
            history.Value.Dispose();
            inference.Value.Dispose();
            history = null;
            inference = null;
            result = null;
        }
    }
}