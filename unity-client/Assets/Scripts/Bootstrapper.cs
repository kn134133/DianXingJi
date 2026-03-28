using UnityEngine;

/// <summary>
/// 用 RuntimeInitializeOnLoadMethod 在游戏启动时自动执行，
/// 完全不依赖场景里的GameObject引用或GUID。
/// </summary>
public static class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // NetworkManager
        GameObject nmGO = new GameObject("NetworkManager");
        Object.DontDestroyOnLoad(nmGO);
        nmGO.AddComponent<NetworkManager>();

        // UIManager
        GameObject uiGO = new GameObject("UIManager");
        Object.DontDestroyOnLoad(uiGO);
        uiGO.AddComponent<UIManager>();
    }
}
