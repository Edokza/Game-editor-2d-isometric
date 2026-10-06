namespace GameEditor.Domain;

public static class Tiles
{
    // ponytail: hardcoded to the assets/tiles ids (1 water, 6 dirt_block, 7 stone_block); per-tile metadata file when tiles get added often
    public static bool IsSolid(int tileId) => tileId is 1 or 6 or 7;
}
