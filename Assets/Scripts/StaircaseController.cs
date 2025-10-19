using System;
using System.Collections.Generic;
using System.Linq;

public class StaircaseController
{
    public readonly List<int> ladder;  // e.g., [1024, 512, 256, 128, 64, 32, 16, 8, 4]
    public int index;
    public int minIndex, maxIndex;

    public int reversalsToStop = 6;
    public int consecutiveCorrectNeeded = 2; // 2-down/1-up

    private int consecutiveCorrect = 0;
    private int lastDir = 0;  // +1 = moved harder (toward index 0), -1 = easier (toward max)
    private readonly List<int> reversalIdx = new();

    public StaircaseController(List<int> ladder, int startIndex)
    {
        this.ladder = ladder;
        minIndex = 0;
        maxIndex = ladder.Count - 1;
        index = Math.Clamp(startIndex, minIndex, maxIndex);
    }

    public bool IsComplete => reversalIdx.Count >= reversalsToStop;
    public IReadOnlyList<int> ReversalIndices => reversalIdx.AsReadOnly();

    public void UpdateWithResponse(bool correct)
    {
        int before = index;
        if (correct)
        {
            // make it harder (more similar to 1024)
            if (++consecutiveCorrect >= consecutiveCorrectNeeded)
            {
                index = Math.Max(minIndex, index - 1);
                consecutiveCorrect = 0;
                CheckReversal(before, +1);
            }
        }
        else
        {
            // make it easier (more different from 1024)
            consecutiveCorrect = 0;
            index = Math.Min(maxIndex, index + 1);
            CheckReversal(before, -1);
        }
    }

    private void CheckReversal(int before, int dir)
    {
        if (before == index) return; // boundary, no move
        if (lastDir != 0 && lastDir != dir) reversalIdx.Add(before);
        lastDir = dir;
    }

    public float ThresholdEstimatePx(int lastN = 6)
    {
        int take = Math.Min(lastN, reversalIdx.Count);
        if (take == 0) return ladder[index];

        var recent = reversalIdx.Skip(Math.Max(0, reversalIdx.Count - take));
        return recent.Select(i => (float)ladder[i]).Average();
    }
}
