using System.Reflection;
using SkillManager;
using UnityEngine;

namespace Settlers.Settlers;

public static class CompanionSkill
{
    public static void Setup()
    {
        if (RegisterSprite("companionicon.png") is not { } icon) return;
        Skill companion = new("Companion", icon);
        companion.Description.English("Improves settlers base health and reduces taming time");
        companion.Configurable = true;
    }

    private static Sprite? RegisterSprite(string fileName, string folderName = "icons")
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string path = $"Settlers.{folderName}.{fileName}";
        using var stream = assembly.GetManifestResourceStream(path);
        if (stream == null) return null;
        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(buffer);
        
        return texture.LoadImage(buffer) ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null;
    }
}