using UnityEngine;

/// <summary>
/// 场景里唯一挂在GameObject上的脚本。
/// 运行时动态创建NetworkManager和UIManager，
/// 避免场景文件需要正确的脚本GUID。
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    void Awake()
    {
        // NetworkManager
        GameObject nmGO = new GameObject("NetworkManager");
        DontDestroyOnLoad(nmGO);
        nmGO.AddComponent<NetworkManager>();

        // UIManager
        GameObject uiGO = new GameObject("UIManager");
        DontDestroyOnLoad(uiGO);
        uiGO.AddComponent<UIManager>();
    }
}
