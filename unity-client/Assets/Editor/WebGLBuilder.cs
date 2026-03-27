using UnityEditor;
using UnityEngine;

public class WebGLBuilder
{
    public static void Build()
    {
        // Allow HTTP connections in WebGL builds
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity" },
            locationPathName = "build/WebGL/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
    }
}
