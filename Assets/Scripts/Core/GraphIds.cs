namespace Why
{
    /// <summary>
    /// Highlight id allocation (exact integers stored in float vertex attributes, all below 2^24).
    /// See Docs/ARCHITECTURE.md.
    /// </summary>
    public static class GraphIds
    {
        public const int None = 0;

        public const int MatterBase = 1;
        public const int LifeBase = 10_000;
        public const int CivBase = 1_000_000;
        public const int CivBlock = 100_000;
        public const int CivFigureBase = 1;
        public const int CivLifelineBase = 1_000;

        public static int Matter(int index) => MatterBase + index;

        /// <summary>Tree-of-life node by preorder index (children ordered with our lineage first).</summary>
        public static int LifeNode(int preorder) => LifeBase + preorder;

        public static int Civ(int civIndex) => CivBase + civIndex * CivBlock;

        public static int CivEnd(int civIndex) => CivBase + civIndex * CivBlock + CivBlock - 1;

        public static int Figure(int civIndex, int figureIndexInCiv) =>
            Civ(civIndex) + CivFigureBase + figureIndexInCiv;

        /// <summary>Lifeline n of a civilization; lifelines are numbered in order of birth.</summary>
        public static int Lifeline(int civIndex, int n) => Civ(civIndex) + CivLifelineBase + n;
    }

    /// <summary>An inclusive id range.</summary>
    public struct IdRange
    {
        public int Min;
        public int Max;

        public IdRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public static IdRange Single(int id) => new IdRange(id, id);
        public static readonly IdRange Empty = new IdRange(-1, -2);
        public bool IsEmpty => Max < Min;
    }
}
