namespace Learn2Code.Core.Models;

public class SceneState
{
    public List<SpriteState> Sprites { get; set; } = new();

    public SceneState() { }

    public SceneState(params SpriteState[] sprites)
    {
        Sprites = new List<SpriteState>(sprites);
    }
}