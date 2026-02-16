namespace XCOM_3
{
    public static class WorldMetrics
    {
        // Règle métier: 1 étage = 2 cases de hauteur.
        public const float FloorHeightInCells = 2.0f;

        public static float FloorToWorldY(int floor, int cellSize)
            => floor * cellSize * FloorHeightInCells;
    }
}
