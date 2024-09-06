using System.IO;
using BepInEx;

namespace Settlers.Settlers;

public class MyPaths
{
    private static readonly string m_folderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "VikingNPC";

    public static string GetFolderPath() => m_folderPath;

}