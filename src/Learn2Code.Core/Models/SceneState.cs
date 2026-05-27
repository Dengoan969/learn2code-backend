namespace Learn2Code.Core.Models;

public class SceneState
{
    public SceneState()
    {
    }

    public SceneState(params SpriteState[] sprites)
    {
        Sprites = new List<SpriteState>(sprites);
    }

    public List<SpriteState> Sprites { get; set; } = new();
}