using Godot;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    // Terrain commands are baked once per MapVersion; entity/selection commands
    // are rebuilt on every click. Splitting them lets QueueRedraw on the dynamic
    // layer skip thousands of terrain primitives.
    private DrawDelegate _terrainLayer = null!;
    private DrawDelegate _dynamicLayer = null!;

    private void InitDrawLayers()
    {
        _terrainLayer = new DrawDelegate { OnDraw = DrawTerrain };
        _dynamicLayer = new DrawDelegate { OnDraw = DrawDynamic };
        AddChild(_terrainLayer);
        AddChild(_dynamicLayer);
    }

    private void RequestDynamicRedraw()
    {
        _dynamicLayer?.QueueRedraw();
    }

    private void RequestFullRedraw()
    {
        _terrainLayer?.QueueRedraw();
        _dynamicLayer?.QueueRedraw();
    }
}

internal sealed partial class DrawDelegate : Node2D
{
    public Action<CanvasItem>? OnDraw;

    public override void _Draw()
    {
        OnDraw?.Invoke(this);
    }
}
