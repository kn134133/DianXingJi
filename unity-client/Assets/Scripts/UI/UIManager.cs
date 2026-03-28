using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 动态创建全部UI，不依赖场景中的任何Inspector引用
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private GameObject _loginPanel;
    private GameObject _mainMenuPanel;
    private GameObject _toastPanel;
    private Text _toastText;
    private Coroutine _toastCoroutine;
    private InputField _usernameInput;
    private InputField _passwordInput;
    private Text _loginStatusText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BuildCanvas();
        ShowLogin();
    }

    void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        _loginPanel    = BuildLoginPanel(canvasGO.transform);
        _mainMenuPanel = BuildMainMenuPanel(canvasGO.transform);
        _toastPanel    = BuildToast(canvasGO.transform);
    }

    GameObject BuildLoginPanel(Transform parent)
    {
        GameObject panel = MakePanel(parent, "LoginPanel",
            new Color(0.08f, 0.12f, 0.18f, 1f), Vector2.zero, new Vector2(960, 600));

        MakeText(panel.transform, "Title", "滇 行 记",
            new Vector2(0, 170), new Vector2(400, 70), 38, Color.white, FontStyle.Bold);
        MakeText(panel.transform, "Sub", "云南文化探索解谜",
            new Vector2(0, 120), new Vector2(300, 34), 17, new Color(0.7f, 0.85f, 1f));

        MakeText(panel.transform, "LblUser", "用 户 名",
            new Vector2(0, 45), new Vector2(260, 26), 14, new Color(0.75f, 0.75f, 0.75f));
        _usernameInput = MakeInput(panel.transform, "InputUser",
            new Vector2(0, 15), new Vector2(280, 40), "请输入用户名");

        MakeText(panel.transform, "LblPass", "密    码",
            new Vector2(0, -40), new Vector2(260, 26), 14, new Color(0.75f, 0.75f, 0.75f));
        _passwordInput = MakeInput(panel.transform, "InputPass",
            new Vector2(0, -68), new Vector2(280, 40), "请输入密码", true);

        _loginStatusText = MakeText(panel.transform, "Status", "",
            new Vector2(0, -115), new Vector2(300, 28), 13, new Color(1f, 0.45f, 0.45f));

        MakeButton(panel.transform, "BtnLogin", "登  录",
            new Vector2(-75, -158), new Vector2(120, 42),
            new Color(0.18f, 0.48f, 0.88f), OnLoginClick);
        MakeButton(panel.transform, "BtnReg", "注  册",
            new Vector2(75, -158), new Vector2(120, 42),
            new Color(0.12f, 0.38f, 0.22f), OnRegisterClick);

        return panel;
    }

    GameObject BuildMainMenuPanel(Transform parent)
    {
        GameObject panel = MakePanel(parent, "MainMenuPanel",
            new Color(0.06f, 0.1f, 0.16f, 1f), Vector2.zero, new Vector2(960, 600));

        MakeText(panel.transform, "Title", "滇 行 记",
            new Vector2(0, 200), new Vector2(400, 70), 42, Color.white, FontStyle.Bold);
        MakeText(panel.transform, "Sub", "云南文化探索解谜",
            new Vector2(0, 152), new Vector2(320, 36), 18, new Color(0.7f, 0.85f, 1f));

        MakeButton(panel.transform, "BtnStart", "开始游戏",
            new Vector2(0, 40), new Vector2(200, 52),
            new Color(0.18f, 0.52f, 0.88f), OnStartGame);
        MakeButton(panel.transform, "BtnLogout", "退出登录",
            new Vector2(0, -28), new Vector2(200, 52),
            new Color(0.32f, 0.15f, 0.15f), OnLogout);

        MakeText(panel.transform, "Ver", "v0.1  |  滇行记",
            new Vector2(0, -255), new Vector2(300, 26), 12, new Color(0.38f, 0.38f, 0.38f));

        panel.SetActive(false);
        return panel;
    }

    GameObject BuildToast(Transform parent)
    {
        GameObject panel = MakePanel(parent, "Toast",
            new Color(0f, 0f, 0f, 0.8f), new Vector2(0, -230), new Vector2(380, 46));
        _toastText = MakeText(panel.transform, "Txt", "",
            Vector2.zero, new Vector2(360, 40), 15, Color.white);
        panel.SetActive(false);
        return panel;
    }

    // ===== 显示控制 =====

    public void ShowLogin()
    {
        _loginPanel.SetActive(true);
        _mainMenuPanel.SetActive(false);
        if (_loginStatusText) _loginStatusText.text = "";
    }

    public void ShowMainMenu()
    {
        _loginPanel.SetActive(false);
        _mainMenuPanel.SetActive(true);
    }

    public void ShowToast(string msg, float dur = 2.5f)
    {
        if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
        _toastCoroutine = StartCoroutine(ToastCo(msg, dur));
    }

    IEnumerator ToastCo(string msg, float dur)
    {
        _toastText.text = msg;
        _toastPanel.SetActive(true);
        yield return new WaitForSeconds(dur);
        _toastPanel.SetActive(false);
    }

    // ===== 按钮回调 =====

    void OnLoginClick()
    {
        string user = _usernameInput.text.Trim();
        string pass = _passwordInput.text.Trim();
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        { _loginStatusText.text = "请填写用户名和密码"; return; }

        _loginStatusText.text = "登录中...";
        string json = "{\"username\":\"" + user + "\",\"password\":\"" + pass + "\"}";
        StartCoroutine(NetworkManager.Instance.Post("/auth/login", json, (ok, body) =>
        {
            if (ok && body.Contains("\"success\":true"))
            {
                _loginStatusText.text = "";
                ShowMainMenu();
                ShowToast("欢迎，" + user + "！");
            }
            else
            {
                _loginStatusText.text = "用户名或密码错误";
            }
        }));
    }

    void OnRegisterClick()
    {
        string user = _usernameInput.text.Trim();
        string pass = _passwordInput.text.Trim();
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        { _loginStatusText.text = "请填写用户名和密码"; return; }
        if (pass.Length < 6)
        { _loginStatusText.text = "密码至少6位"; return; }

        _loginStatusText.text = "注册中...";
        string json = "{\"username\":\"" + user + "\",\"password\":\"" + pass +
                      "\",\"email\":\"" + user + "@game.com\"}";
        StartCoroutine(NetworkManager.Instance.Post("/auth/register", json, (ok, body) =>
        {
            if (ok && body.Contains("\"success\":true"))
            { _loginStatusText.text = "注册成功，请登录！"; ShowToast("注册成功！"); }
            else if (body.Contains("exist") || body.Contains("已存在"))
            { _loginStatusText.text = "用户名已存在"; }
            else
            { _loginStatusText.text = "注册失败，请重试"; }
        }));
    }

    void OnStartGame()  { ShowToast("游戏内容加载中..."); }
    void OnLogout()     { ShowLogin(); ShowToast("已退出登录"); }

    // ===== UI工厂 =====

    GameObject MakePanel(Transform parent, string name, Color color,
                         Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = color;
        return go;
    }

    Text MakeText(Transform parent, string name, string content,
                  Vector2 pos, Vector2 size, int fs, Color color,
                  FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.font = null;
        t.fontSize = fs;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = style;
        return t;
    }

    InputField MakeInput(Transform parent, string name,
                         Vector2 pos, Vector2 size,
                         string placeholder, bool password = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.14f, 0.19f, 0.27f, 1f);

        // placeholder
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        RectTransform phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(8, 2); phRT.offsetMax = new Vector2(-8, -2);
        Text ph = phGO.AddComponent<Text>();
        ph.text = placeholder;
        ph.font = null;
        ph.fontSize = 14; ph.color = new Color(0.45f, 0.45f, 0.45f);
        ph.alignment = TextAnchor.MiddleLeft;

        // text
        GameObject tGO = new GameObject("Text");
        tGO.transform.SetParent(go.transform, false);
        RectTransform tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8, 2); tRT.offsetMax = new Vector2(-8, -2);
        Text txt = tGO.AddComponent<Text>();
        txt.font = null;
        txt.fontSize = 14; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        InputField field = go.AddComponent<InputField>();
        field.textComponent = txt;
        field.placeholder = ph;
        if (password) field.contentType = InputField.ContentType.Password;
        return field;
    }

    void MakeButton(Transform parent, string name, string label,
                    Vector2 pos, Vector2 size, Color color,
                    UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = color;
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(
            Mathf.Min(color.r+0.15f,1f), Mathf.Min(color.g+0.15f,1f), Mathf.Min(color.b+0.15f,1f));
        cb.pressedColor = new Color(color.r*0.7f, color.g*0.7f, color.b*0.7f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        MakeText(go.transform, "Lbl", label, Vector2.zero, size, 15, Color.white, FontStyle.Bold);
    }
}
