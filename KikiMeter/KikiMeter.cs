using BepInEx;
using RoR2;
using RoR2.Stats;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace KikiMeter
{
    //This is an example plugin that can be put in BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    //It's a very simple plugin that adds Bandit to the game, and gives you a tier 3 item whenever you press F2.
    //Lets examine what each line of code is for:

    //This attribute specifies that we have a dependency on R2API, as we're using it to add Bandit to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency("com.bepis.r2api")]

    //This attribute is required, and lists metadata for your plugin.
    //The GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config). I like to use the java package notation, which is "com.[your name here].[your plugin name here]"
    //The name is the name of the plugin that's displayed on load, and the version number just specifies what version the plugin is.
    [BepInPlugin("la.roblab.ror2.KikiMeter", "KikiMeter", "1.0")]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class KikiMeter : BaseUnityPlugin
    {
        const int SECONDS_IN_TICK_COUNT = 3;
        const int TICK_COUNT = SECONDS_IN_TICK_COUNT * 60;
        const ulong MAX_WIDTH = 200;
        const int BAR_HEIGHT = 20;
        const int BAR_PADDING = 20;
        const int WINDOW_HEADER = 30;
        public ulong CachedMaxDps { get; set; } = 0;
        public Dictionary<GameObject, CircularBuffer<ulong>> CachedDamageDealt { get; set; } = new Dictionary<GameObject, CircularBuffer<ulong>>();

        void DamageNotified(DamageDealtMessage damageDealtMessage)
        {
            // TODO: Better check. This might not work in single-player.
            if (NetworkServer.active)
                return;

            if ((bool)((UnityEngine.Object)damageDealtMessage.attacker))
            {
                // Manually update the stats because the game is apparently too stupid to do it itself.
                var master = damageDealtMessage.attacker.GetComponent<CharacterBody>()?.master.GetComponent<PlayerCharacterMasterController>();

                if (master)
                {
                    var stats = master.GetComponent<PlayerStatsComponent>();
                    stats.currentStats.PushStatValue(StatDef.totalDamageDealt, (ulong)damageDealtMessage.damage);
                }
            }
        }

        void ResetRunStats(Run run)
        {
            CachedMaxDps = 0;
            CachedDamageDealt = new Dictionary<GameObject, CircularBuffer<ulong>>();
        }

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            GlobalEventManager.onClientDamageNotified += DamageNotified;

            // Clean up on game over.
            Run.onRunStartGlobal += ResetRunStats;
            Run.onRunDestroyGlobal += ResetRunStats;
        }

        //The Update() method is run on every frame of the game.
        public void Update()
        {
            UpdateDamageDealt();
        }


        private static Texture2D _staticRectTexture;
        private static GUIStyle _staticRectStyle;

        Color Darken(Color color, float shade_factor)
        {
            return new Color(
                color.r * (1 - shade_factor),
                color.g * (1 - shade_factor),
                color.b * (1 - shade_factor));
        }

        // Draw a simple rectangle at the given coordonate.
        public static void GUIDrawRect(Rect position, Color color, string left, string right)
        {
            if (_staticRectTexture == null)
            {
                _staticRectTexture = new Texture2D(1, 1);
            }

            if (_staticRectStyle == null)
            {
                _staticRectStyle = new GUIStyle();
            }

            _staticRectTexture.SetPixel(0, 0, color);
            _staticRectTexture.Apply();

            _staticRectStyle.normal.background = _staticRectTexture;

            GUI.Box(position, GUIContent.none, _staticRectStyle);

            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(position.x + 5, position.y, MAX_WIDTH - 5, position.height), left, style);

            var style2 = new GUIStyle();
            style2.normal.textColor = Color.white;
            style2.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(position.x, position.y, MAX_WIDTH - 5, position.height), right, style2);
        }

        private Rect windowRect = new Rect(20, 20, MAX_WIDTH + 20, 50);

        public void OnGUI()
        {
            // DEBUG: are we enabled?
            GUIDrawRect(new Rect(0, 0, 10, 10), Color.red, "", "");

            if (CachedDamageDealt.Count != 0)
            {
                HorizResizer(windowRect);
                windowRect.height = CachedDamageDealt.Count * (BAR_HEIGHT + BAR_PADDING) + WINDOW_HEADER;
                windowRect = GUI.Window(0, windowRect, WindowFunction, "KikiMeter");
            }
        }

        bool draggingLeft = false;
        bool draggingRight = false;

        private Rect HorizResizer(Rect window, bool right = true, float detectionRange = 8f)
        {
            detectionRange *= 0.5f;
            Rect resizer = window;

            if (right)
            {
                resizer.xMin = resizer.xMax - detectionRange;
                resizer.xMax += detectionRange;
            }
            else
            {
                resizer.xMax = resizer.xMin + detectionRange;
                resizer.xMin -= detectionRange;
            }

            Event current = Event.current;
            //EditorGUIUtility.AddCursorRect(resizer, MouseCursor.ResizeHorizontal);

            // if mouse is no longer dragging, stop tracking direction of drag
            if (current.type == EventType.MouseUp)
            {
                draggingLeft = false;
                draggingRight = false;
            }

            // resize window if mouse is being dragged within resizor bounds
            if (current.mousePosition.x > resizer.xMin &&
                current.mousePosition.x < resizer.xMax &&
                current.type == EventType.MouseDrag &&
                current.button == 0 ||
                draggingLeft ||
                draggingRight)
            {
                if (right == !draggingLeft)
                {
                    window.width = current.mousePosition.x + current.delta.x;
                    //Repaint();
                    draggingRight = true;
                }
                else if (!right == !draggingRight)
                {
                    window.width = window.width - (current.mousePosition.x + current.delta.x);
                    //Repaint();
                    draggingLeft = true;
                }

            }

            return window;
        }


        void WindowFunction(int windowID)
        {
            // Make a very long rect that is 20 pixels tall.
            // This will make the window be resizable by the top
            // title bar - no matter how wide it gets.
            GUI.DragWindow(new Rect(0, 0, 10000, 200));

            // Use CachedDamageDealt to draw graphs
            Dictionary<GameObject, ulong> dpsPerPlayer = new Dictionary<GameObject, ulong>();

            foreach (var player in CachedDamageDealt)
            {
                ulong damageInWindow = 0;
                foreach (var damage in player.Value.Latest().Zip(player.Value.Latest().Skip(1), (a, b) => b - a))
                {
                    damageInWindow += damage;
                }

                ulong dps = damageInWindow / SECONDS_IN_TICK_COUNT;
                dpsPerPlayer.Add(player.Key, dps);
            }

            if (dpsPerPlayer.Count != 0)
            {
                CachedMaxDps = Math.Max(dpsPerPlayer.Values.Max(), CachedMaxDps);

                foreach (var item in dpsPerPlayer.Select((value, i) => new { i, value }))
                {
                    ulong cross_product;
                    if (CachedMaxDps == 0)
                        cross_product = 0;
                    else
                        cross_product = item.value.Value * MAX_WIDTH / CachedMaxDps;
                    var master = item.value.Key.GetComponent<CharacterMaster>();
                    if (master != null) {
                        GUIDrawRect(new Rect(10, (item.i * (BAR_HEIGHT + BAR_PADDING)) + WINDOW_HEADER, cross_product, BAR_HEIGHT), GetColorForHero(master), Util.GetBestMasterName(master), item.value.Value.ToString());
                    }
                }
            }
        }

        Color GetColorForHero(CharacterMaster master)
        {
            var survivor = SurvivorCatalog.FindSurvivorDefFromBody(master.bodyPrefab);
            Color survivorColor;
            if (survivor != null)
                survivorColor = survivor.primaryColor;
            else
                survivorColor = Color.red;
            return Darken(survivorColor, .2f);
        }


        public void UpdateDamageDealt()
        {

            if (Run.instance == null)
                return;

            RunReport runReport = RunReport.Generate(Run.instance, GameResultType.Unknown);

            for (int i = 0; i < runReport.playerInfoCount; i++)
                if (!CachedDamageDealt.ContainsKey(runReport.GetPlayerInfo(i).master.gameObject))
                    CachedDamageDealt.Add(runReport.GetPlayerInfo(i).master.gameObject, new CircularBuffer<ulong>(TICK_COUNT));

            for (int i = 0; i < runReport.playerInfoCount; i++)
            {
                var name = runReport.GetPlayerInfo(i).master.gameObject;
                var curDamageDealt = runReport.GetPlayerInfo(i).statSheet.GetStatValueULong(StatDef.totalDamageDealt);
                CachedDamageDealt[name].Add(curDamageDealt);
            }
        }
    }
}