using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissingArtifactsTracker
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MissingArtifactsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.yourname.missingartifacts";
        public const string PluginName = "MissingArtifactsTracker";
        public const string PluginVersion = "1.4.0";

        // Compound codes for Bulwark's Ambry
        private static readonly Dictionary<string, string> ArtifactCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Chaos", "● ▲ ● | ● ▲ ● | ● ▲ ●" },
            { "Command", "■ ■ ■ | ■ ■ ■ | ▲ ▲ ▲" },
            { "Death", "● ● ● | ■ ■ ■ | ● ● ●" },
            { "Dissonance", "● ◆ ■ | ◆ ■ ◆ | ■ ◆ ●" },
            { "Enigma", "◆ ■ ■ | ● ■ ▲ | ◆ ■ ■" },
            { "Evolution", "◆ ◆ ◆ | ◆ ◆ ◆ | ◆ ◆ ◆" },
            { "Frailty", "● ● ● | ● ▲ ● | ● ● ●" },
            { "Glass", "◆ ◆ ◆ | ◆ ◆ ◆ | ◆ ◆ ◆" },
            { "Honor", "■ ■ ■ | ■ ◆ ■ | ■ ■ ■" },
            { "Kin", "● ▲ ▲ | ◆ ▲ ▲ | ■ ▲ ▲" },
            { "Metamorphosis", "◆ ■ ● | ◆ ■ ● | ◆ ■ ●" },
            { "Sacrifice", "▲ ▲ ▲ | ▲ ▲ ▲ | ▲ ◆ ▲" },
            { "Soul", "● ■ ■ | ● ◆ ■ | ● ■ ■" },
            { "Spite", "▲ ● ▲ | ● ● ● | ▲ ● ▲" },
            { "Swarms", "● ● ▲ | ▲ ◆ ▲ | ▲ ● ●" },
            { "Vengeance", "◆ ◆ ◆ | ◆ ■ ◆ | ◆ ◆ ◆" },
            { "Rebirth", "◆ ▲ ■ | ● ◆ ▲ | ■ ● ◆" }
        };

        private List<string> cachedMissing = new List<string>();
        private string stageNotice = string.Empty;
        private float displayTimer = 0f;
        private const float DisplayDuration = 10f;
        private const float FadeDuration = 2f;

        private Texture2D panelTex;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle itemStyle;
        private GUIStyle noticeStyle;

        private void Awake()
        {
            Logger.LogInfo("[MissingArtifactsTracker] UI Overlay v1.4.0 Initialized.");
            Stage.onStageStartGlobal += OnStageStart;
            InitTextures();
        }

        private void OnDestroy()
        {
            Stage.onStageStartGlobal -= OnStageStart;
        }

        private void InitTextures()
        {
            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.11f, 0.90f));
            panelTex.Apply();
        }

        private void Update()
        {
            if (displayTimer > 0f)
            {
                displayTimer -= Time.deltaTime;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                QueryMissingArtifacts();
                stageNotice = string.Empty;
                displayTimer = DisplayDuration;
            }
        }

        private void OnStageStart(Stage stage)
        {
            StartCoroutine(DelayedStageCheck());
        }

        private IEnumerator DelayedStageCheck()
        {
            yield return new WaitForSeconds(2.5f);

            if (Run.instance == null) yield break;

            bool isStage1 = Run.instance.stageClearCount == 0;
            string currentScene = SceneManager.GetActiveScene().name.ToLowerInvariant();
            bool isSkyMeadow = currentScene.Contains("skymeadow");
            bool isHelminthHatchery = currentScene.Contains("lemuriantemple") || currentScene.Contains("itgrovetime");

            SceneDef currentSceneDef = SceneCatalog.GetSceneDefFromSceneName(currentScene);
            if (currentSceneDef != null)
            {
                string title = Language.GetString(currentSceneDef.nameToken).ToLowerInvariant();
                if (title.Contains("sky meadow")) isSkyMeadow = true;
                if (title.Contains("helminth hatchery")) isHelminthHatchery = true;
            }

            if (!isStage1 && !isSkyMeadow && !isHelminthHatchery)
            {
                yield break;
            }

            if (isSkyMeadow)
            {
                stageNotice = "SKY MEADOW: Compound Portal Available Below";
            }
            else if (isHelminthHatchery)
            {
                stageNotice = "HELMINTH HATCHERY: Check Hidden Switches";
            }
            else
            {
                stageNotice = string.Empty;
            }

            QueryMissingArtifacts();
            displayTimer = DisplayDuration;
        }

        private void QueryMissingArtifacts()
        {
            cachedMissing.Clear();

            LocalUser localUser = LocalUserManager.GetFirstLocalUser();
            if (localUser == null || localUser.userProfile == null) return;

            UserProfile profile = localUser.userProfile;
            int count = ArtifactCatalog.artifactCount;

            for (int i = 0; i < count; i++)
            {
                ArtifactDef artifact = ArtifactCatalog.GetArtifactDef((ArtifactIndex)i);
                if (artifact == null) continue;

                if (artifact.unlockableDef != null && !profile.HasUnlockable(artifact.unlockableDef))
                {
                    string name = Language.GetString(artifact.nameToken);
                    if (string.IsNullOrEmpty(name) || name == artifact.nameToken)
                    {
                        name = artifact.cachedName;
                    }

                    string cleanName = name.Replace("Artifact of ", "").Replace("Artifact of the ", "").Trim();
                    string codeSuffix = ArtifactCodes.TryGetValue(cleanName, out string code) ? $"  <color=#8FAEC2>[{code}]</color>" : "";

                    // Render cleanly without the diamond glyph
                    cachedMissing.Add($"{name}{codeSuffix}");
                }
            }
        }

        private void EnsureStyles()
        {
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.box);
                panelStyle.normal.background = panelTex;
                panelStyle.padding = new RectOffset(18, 18, 16, 16);

                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    richText = true,
                    normal = { textColor = new Color(0.95f, 0.78f, 0.35f, 1f) }
                };

                noticeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    richText = true,
                    normal = { textColor = new Color(0.42f, 0.88f, 1f, 1f) }
                };

                itemStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    richText = true,
                    normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
                };
            }
        }

        private void OnGUI()
        {
            if (displayTimer <= 0f) return;

            EnsureStyles();

            float alpha = displayTimer < FadeDuration ? (displayTimer / FadeDuration) : 1f;
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            bool twoColumns = cachedMissing.Count > 8;
            float panelWidth = twoColumns ? 640f : 350f;
            int rows = twoColumns ? Mathf.CeilToInt(cachedMissing.Count / 2f) : cachedMissing.Count;

            // Accurate dynamic height calculation with generous line padding
            float lineHeight = 26f;
            float headerHeight = 32f;
            float bannerHeight = string.IsNullOrEmpty(stageNotice) ? 0f : 28f;
            float innerPadding = 32f; // Top and bottom padding
            float panelHeight = headerHeight + bannerHeight + (Mathf.Max(rows, 1) * lineHeight) + innerPadding;

            // Anchor to the middle-height on the left of the screen
            float panelX = 28f;
            float panelY = Mathf.Max(20f, (Screen.height - panelHeight) * 0.5f);

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 16f, panelRect.y + 14f, panelWidth - 32f, panelHeight - 24f));

            if (!string.IsNullOrEmpty(stageNotice))
            {
                GUILayout.Label(stageNotice, noticeStyle);
                GUILayout.Space(2f);
            }

            if (cachedMissing.Count == 0)
            {
                GUILayout.Label("ARTIFACTS COMPLETE (All Unlocked)", titleStyle);
            }
            else
            {
                GUILayout.Label($"MISSING ARTIFACTS ({cachedMissing.Count})", titleStyle);
                GUILayout.Space(6f);

                if (twoColumns)
                {
                    GUILayout.BeginHorizontal();

                    // Left Column
                    GUILayout.BeginVertical(GUILayout.Width((panelWidth - 48f) / 2f));
                    for (int i = 0; i < rows; i++)
                    {
                        GUILayout.Label(cachedMissing[i], itemStyle, GUILayout.Height(lineHeight - 2f));
                    }
                    GUILayout.EndVertical();

                    // Right Column
                    GUILayout.BeginVertical(GUILayout.Width((panelWidth - 48f) / 2f));
                    for (int i = rows; i < cachedMissing.Count; i++)
                    {
                        GUILayout.Label(cachedMissing[i], itemStyle, GUILayout.Height(lineHeight - 2f));
                    }
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
                else
                {
                    foreach (string item in cachedMissing)
                    {
                        GUILayout.Label(item, itemStyle, GUILayout.Height(lineHeight - 2f));
                    }
                }
            }

            GUILayout.EndArea();
            GUI.color = prevColor;
        }
    }
}