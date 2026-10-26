using GameEditor.Domain;

namespace GameEditor.Application;

public sealed class SolidCommand(TileMap map, int tileId, bool solid) : ICommand
{
    public void Execute() => map.SetSolid(tileId, solid);
    public void Undo() => map.SetSolid(tileId, !solid);
}

public sealed class SolidSpriteCommand(TileMap map, string sprite, bool solid) : ICommand
{
    public void Execute() => map.SetSolidSprite(sprite, solid);
    public void Undo() => map.SetSolidSprite(sprite, !solid);
}

public sealed class ShadowCommand(TileMap map, string sprite, bool shadow) : ICommand
{
    public void Execute() => map.SetShadow(sprite, shadow);
    public void Undo() => map.SetShadow(sprite, !shadow);
}
