using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using DianXingJi.Data;

namespace DianXingJi.Network
{
    /// <summary>
    /// 网络管理器 - 处理与后端的HTTP/Socket通信
    /// HTTP/HTTPS：用于低频非实时数据（登录/存档/文化资源）
    /// WebSocket：用于高频实时数据（关卡生成规则/NPC同步）
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("服务器配置")]
        [SerializeField] private string baseUrl = "http://139.129.27.169:8080";
        [SerializeField] private string wsUrl = "ws://139.129.27.169:8080/ws";
        [SerializeField] private float requestTimeout = 10f;

        private string _authToken = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ==================== 认证接口 ====================

        public IEnumerator Login(string username, string password, Action<LoginResult> callback)
        {
            string json = JsonUtility.ToJson(new LoginRequest { username = username, password = password });
            yield return StartCoroutine(PostRequest("/api/auth/login", json, null, (response) =>
            {
                if (response.Success)
                {
                    LoginResult result = JsonUtility.FromJson<LoginResult>(response.Data);
                    _authToken = result.token;
                    callback?.Invoke(result);
                }
                else
                {
                    callback?.Invoke(new LoginResult { success = false, message = response.Error });
                }
            }));
        }

        public IEnumerator Register(string username, string password, string email,
            Action<RegisterResult> callback)
        {
            string json = JsonUtility.ToJson(new RegisterRequest
            { username = username, password = password, email = email });
            yield return StartCoroutine(PostRequest("/api/auth/register", json, null, (response) =>
            {
                RegisterResult result = response.Success
                    ? JsonUtility.FromJson<RegisterResult>(response.Data)
                    : new RegisterResult { success = false, message = response.Error };
                callback?.Invoke(result);
            }));
        }

        // ==================== 关卡规则接口（Socket实时）====================

        public IEnumerator GetLevelRule(string theme, int levelIndex, Action<LevelRule> callback)
        {
            string url = $"/api/level/rule?theme={theme}&level={levelIndex}";
            yield return StartCoroutine(GetRequest(url, (response) =>
            {
                if (response.Success)
                {
                    LevelRule rule = JsonUtility.FromJson<LevelRule>(response.Data);
                    callback?.Invoke(rule);
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] 获取关卡规则失败: {response.Error}");
                    callback?.Invoke(null);
                }
            }));
        }

        public IEnumerator ValidateLevelCulture(LevelData levelData, Action<bool> callback)
        {
            string json = JsonUtility.ToJson(new LevelValidationRequest
            {
                theme = levelData.CulturalTheme,
                levelIndex = levelData.LevelIndex,
                puzzleIds = new List<int>(),
                buildingTypes = new List<string>()
            });

            foreach (var p in levelData.Puzzles ?? new List<PuzzleData>())
                ((LevelValidationRequest)JsonUtility.FromJson<LevelValidationRequest>(json)).puzzleIds.Add(p.Id);

            yield return StartCoroutine(PostRequest("/api/level/validate", json, null, (response) =>
            {
                if (response.Success)
                {
                    ValidationResult result = JsonUtility.FromJson<ValidationResult>(response.Data);
                    callback?.Invoke(result.isValid);
                }
                else
                {
                    // 后端验证失败时本地通过（防止网络问题导致游戏中断）
                    callback?.Invoke(true);
                }
            }));
        }

        // ==================== 进度存档接口 ====================

        public IEnumerator SaveProgress(GameProgress progress, Action<bool> callback)
        {
            string json = JsonUtility.ToJson(new SaveProgressRequest
            {
                playerId = progress.PlayerId,
                levelId = progress.LevelId,
                stateJson = JsonUtility.ToJson(progress.CurrentLevelState),
                unlockedCultureIds = progress.UnlockedCultureIds,
                totalScore = progress.TotalScore
            });

            yield return StartCoroutine(PostRequest("/api/progress/save", json, null, (response) =>
            {
                callback?.Invoke(response.Success);
            }));
        }

        public IEnumerator LoadProgress(int playerId, Action<GameProgress> callback)
        {
            yield return StartCoroutine(GetRequest($"/api/progress/{playerId}", (response) =>
            {
                if (response.Success)
                {
                    GameProgress progress = JsonUtility.FromJson<GameProgress>(response.Data);
                    callback?.Invoke(progress);
                }
                else
                {
                    callback?.Invoke(null);
                }
            }));
        }

        // ==================== 文化资源接口 ====================

        public IEnumerator GetCultureResources(Action<List<CultureResource>> callback)
        {
            yield return StartCoroutine(GetRequest("/api/culture/resources", (response) =>
            {
                if (response.Success)
                {
                    CultureResourceListWrapper wrapper =
                        JsonUtility.FromJson<CultureResourceListWrapper>(response.Data);
                    callback?.Invoke(wrapper.resources);
                }
                else
                {
                    callback?.Invoke(null);
                }
            }));
        }

        public IEnumerator RecordCultureUnlock(int playerId, int cultureResourceId, Action<bool> callback)
        {
            string json = JsonUtility.ToJson(new UnlockRequest
            { playerId = playerId, cultureResourceId = cultureResourceId });
            yield return StartCoroutine(PostRequest("/api/culture/unlock", json, null, (response) =>
            {
                callback?.Invoke(response.Success);
            }));
        }

        // ==================== 反馈接口 ====================

        public IEnumerator SubmitFeedback(int playerId, string type, string content, Action<bool> callback)
        {
            string json = JsonUtility.ToJson(new FeedbackRequest
            { playerId = playerId, feedbackType = type, content = content });
            yield return StartCoroutine(PostRequest("/api/feedback/submit", json, null, (response) =>
            {
                callback?.Invoke(response.Success);
            }));
        }

        // ==================== 通用HTTP请求 ====================

        private IEnumerator GetRequest(string path, Action<HttpResponse> callback)
        {
            string url = baseUrl + path;
            using UnityWebRequest request = UnityWebRequest.Get(url);
            SetAuthHeader(request);
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            callback?.Invoke(ParseResponse(request));
        }

        private IEnumerator PostRequest(string path, string jsonBody, Dictionary<string, string> headers,
            Action<HttpResponse> callback)
        {
            string url = baseUrl + path;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            using UnityWebRequest request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            request.timeout = (int)requestTimeout;

            if (headers != null)
                foreach (var h in headers)
                    request.SetRequestHeader(h.Key, h.Value);

            yield return request.SendWebRequest();

            callback?.Invoke(ParseResponse(request));
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(_authToken))
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
        }

        private HttpResponse ParseResponse(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                return new HttpResponse
                {
                    Success = true,
                    Data = request.downloadHandler.text,
                    StatusCode = (int)request.responseCode
                };
            }
            return new HttpResponse
            {
                Success = false,
                Error = request.error,
                StatusCode = (int)request.responseCode
            };
        }
    }

    // ==================== 请求/响应数据结构 ====================

    [Serializable] public class HttpResponse
    {
        public bool Success;
        public string Data;
        public string Error;
        public int StatusCode;
    }

    [Serializable] public class LoginRequest { public string username; public string password; }
    [Serializable] public class LoginResult
    {
        public bool success; public string message; public string token;
        public PlayerData playerData;
    }
    [Serializable] public class RegisterRequest { public string username; public string password; public string email; }
    [Serializable] public class RegisterResult { public bool success; public string message; }
    [Serializable] public class LevelValidationRequest
    {
        public string theme; public int levelIndex;
        public List<int> puzzleIds; public List<string> buildingTypes;
    }
    [Serializable] public class ValidationResult { public bool isValid; public string reason; }
    [Serializable] public class SaveProgressRequest
    {
        public int playerId; public int levelId; public string stateJson;
        public List<int> unlockedCultureIds; public int totalScore;
    }
    [Serializable] public class UnlockRequest { public int playerId; public int cultureResourceId; }
    [Serializable] public class FeedbackRequest { public int playerId; public string feedbackType; public string content; }
    [Serializable] public class CultureResourceListWrapper { public List<CultureResource> resources; }
}
