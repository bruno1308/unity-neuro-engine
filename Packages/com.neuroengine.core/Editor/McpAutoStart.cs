using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// Configures Unity-MCP for stdio transport mode.
    /// When UseHttpTransport is false, Unity-MCP's StdioBridgeHost auto-starts,
    /// enabling Claude Code to communicate with Unity via the MCP server.
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoStart
    {
        private const string AutoStartEnabledKey = "NeuroEngine_McpAutoStart_Enabled";
        private const string HasConfiguredThisSessionKey = "NeuroEngine_McpAutoStart_SessionConfigured";
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";

        static McpAutoStart()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (SessionState.GetBool(HasConfiguredThisSessionKey, false))
                return;

            SessionState.SetBool(HasConfiguredThisSessionKey, true);

            if (!EditorPrefs.GetBool(AutoStartEnabledKey, true))
            {
                Debug.Log("[NeuroEngine] MCP stdio mode auto-config disabled. Enable via NeuroEngine > MCP Auto-Start");
                return;
            }

            ConfigureStdioMode();
        }

        private static void ConfigureStdioMode()
        {
            // Set UseHttpTransport to false so Unity-MCP uses stdio mode
            // The StdioBridgeHost auto-starts when this is false
            bool currentUseHttp = EditorPrefs.GetBool(UseHttpTransportKey, true);

            if (currentUseHttp)
            {
                EditorPrefs.SetBool(UseHttpTransportKey, false);
                Debug.Log("[NeuroEngine] Configured Unity-MCP for stdio transport mode. Bridge will auto-start.");

                // Try to start the bridge immediately via reflection
                TryStartStdioBridge();
            }
            else
            {
                // Already configured for stdio, check if bridge is running
                if (IsStdioBridgeRunning())
                {
                    Debug.Log("[NeuroEngine] Unity-MCP stdio bridge is running.");
                }
                else
                {
                    Debug.Log("[NeuroEngine] Unity-MCP stdio mode configured. Bridge should auto-start.");
                    TryStartStdioBridge();
                }
            }
        }

        private static bool IsStdioBridgeRunning()
        {
            try
            {
                var bridgeHostType = Type.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost, MCPForUnity.Editor");
                if (bridgeHostType == null) return false;

                var isRunningProp = bridgeHostType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
                if (isRunningProp == null) return false;

                return (bool)isRunningProp.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        private static void TryStartStdioBridge()
        {
            try
            {
                var bridgeHostType = Type.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost, MCPForUnity.Editor");
                if (bridgeHostType == null)
                {
                    Debug.LogWarning("[NeuroEngine] Unity-MCP StdioBridgeHost not found.");
                    return;
                }

                var startMethod = bridgeHostType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
                if (startMethod == null)
                {
                    Debug.LogWarning("[NeuroEngine] StdioBridgeHost.Start method not found.");
                    return;
                }

                startMethod.Invoke(null, null);

                // Check if it started
                if (IsStdioBridgeRunning())
                {
                    var getPortMethod = bridgeHostType.GetMethod("GetCurrentPort", BindingFlags.Public | BindingFlags.Static);
                    int port = getPortMethod != null ? (int)getPortMethod.Invoke(null, null) : -1;
                    Debug.Log($"[NeuroEngine] Unity-MCP stdio bridge started on port {port}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NeuroEngine] Failed to start stdio bridge: {ex.Message}");
            }
        }

        [MenuItem("NeuroEngine/MCP Auto-Start/Enable")]
        private static void EnableAutoStart()
        {
            EditorPrefs.SetBool(AutoStartEnabledKey, true);
            Debug.Log("[NeuroEngine] MCP auto-start enabled.");
        }

        [MenuItem("NeuroEngine/MCP Auto-Start/Disable")]
        private static void DisableAutoStart()
        {
            EditorPrefs.SetBool(AutoStartEnabledKey, false);
            Debug.Log("[NeuroEngine] MCP auto-start disabled.");
        }

        [MenuItem("NeuroEngine/MCP Auto-Start/Start Bridge Now")]
        private static void StartBridgeNow()
        {
            ConfigureStdioMode();
        }

        [MenuItem("NeuroEngine/MCP Auto-Start/Check Status")]
        private static void CheckStatus()
        {
            bool useHttp = EditorPrefs.GetBool(UseHttpTransportKey, true);
            bool isRunning = IsStdioBridgeRunning();

            Debug.Log($"[NeuroEngine] MCP Status: UseHttpTransport={useHttp}, StdioBridgeRunning={isRunning}");

            if (isRunning)
            {
                try
                {
                    var bridgeHostType = Type.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost, MCPForUnity.Editor");
                    var getPortMethod = bridgeHostType?.GetMethod("GetCurrentPort", BindingFlags.Public | BindingFlags.Static);
                    int port = getPortMethod != null ? (int)getPortMethod.Invoke(null, null) : -1;
                    Debug.Log($"[NeuroEngine] Stdio bridge listening on port {port}");
                }
                catch { }
            }
        }

        [MenuItem("NeuroEngine/MCP Auto-Start/Enable", true)]
        private static bool EnableValidate() => !EditorPrefs.GetBool(AutoStartEnabledKey, true);

        [MenuItem("NeuroEngine/MCP Auto-Start/Disable", true)]
        private static bool DisableValidate() => EditorPrefs.GetBool(AutoStartEnabledKey, true);
    }
}
