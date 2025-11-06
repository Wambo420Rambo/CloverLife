using BepInEx;
using HarmonyLib;
using Panik;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TMPro;
using UnityEngine;


namespace CloverLife
{
    [BepInPlugin("CloverLife", "Clover Quality of Life", "1.0.1")]
    public class StarterPlugin : BaseUnityPlugin
    {
        public static bool throwAway = false;
        public static bool corpse = false;
        public static bool startOfCutscene = false;
        private Harmony harmony;
        private JsonConfig config = new JsonConfig();

        private void Awake()
        {
            if (!config.LoadConfig())
            {
                config.DiscardCharm = 4;
                config.JackpotSpeed = 10;
                config.WhenGambling = 4;
                config.Normal = 1;
                config.WhenInCutscene = 3;

                Logger.LogInfo("Setting default config values");
            }
            Logger.LogInfo("Config values: " +
                $"DiscardCharm={config.DiscardCharm}, " +
                $"JackpotSpeed={config.JackpotSpeed}, " +
                $"WhenGambling={config.WhenGambling}, " +
                $"Normal={config.Normal}, " +
                $"WhenInCutscene={config.WhenInCutscene}");

            harmony = new Harmony("CloverLife");
            harmony.PatchAll(typeof(Patches));
        }


        private GameplayMaster.GamePhase lastPhase = GameplayMaster.GamePhase.Undefined;

        private void Update()
        {
            if (Level.CurrentScene == 1)
            {
                skipIntro();
            }

            var phase = GameplayMaster.GetGamePhase();

            if (phase == GameplayMaster.GamePhase.preparation && !corpse)
            {
                SetCorpse();
            }

            bool shouldSpeedUp = phase == GameplayMaster.GamePhase.cutscene ||
                                 phase == GameplayMaster.GamePhase.gambling;


            if (shouldSpeedUp && !startOfCutscene)
            {
                startOfCutscene = true;
            }
            else if (!shouldSpeedUp && startOfCutscene)
            {
                startOfCutscene = false;
            }

            if (throwAway && phase == GameplayMaster.GamePhase.preparation)
            {
                Time.timeScale = config.DiscardCharm;
                Data.settings.transitionSpeed = config.DiscardCharm;
                Logger.LogInfo("Setting transition speed to " + Time.timeScale);
                throwAway = false;
                new WaitForSeconds(0.5f);

                Time.timeScale = config.Normal;
                Data.settings.transitionSpeed = config.Normal;
                Logger.LogInfo("Resetting speed to normal from discard");
            }

            // Only update speed if phase actually changed
            if (phase != lastPhase)
            {
                lastPhase = phase;

                if (phase == GameplayMaster.GamePhase.gambling)
                {
                    long jackpots = GameplayData.SpinsWithAtLeast1Jackpot_Get();
                    Data.settings.transitionSpeed = jackpots >= config.Normal ? config.JackpotSpeed : config.WhenGambling;
                    Logger.LogInfo("Setting transition speed to " + Data.settings.transitionSpeed);
                }
                else if (shouldSpeedUp)
                {
                    Time.timeScale = config.WhenInCutscene;
                    Logger.LogInfo("Setting time scale to " + Time.timeScale);
                }
                else
                {
                    Time.timeScale = config.Normal;
                    Data.settings.transitionSpeed = config.Normal;
                    Logger.LogInfo("Resetting speed to normal");
                }
            }
        }


        private void OnDestroy()
        {
            // Clean up patches when plugin unloads
            harmony?.UnpatchSelf();
        }
        public void skipIntro()
        {
            Level.GoTo(2, true);
        }

        public void SetCorpse()
        {
            var skeletonParts = new List<PowerupScript.Identifier> {
                PowerupScript.Identifier.Skeleton_Arm1,
                PowerupScript.Identifier.Skeleton_Arm2,
                PowerupScript.Identifier.Skeleton_Leg1,
                PowerupScript.Identifier.Skeleton_Leg2
            };

            int availableSlots = Enumerable.Range(0, 4)
                .Count(slot => PowerupScript.IsDrawerAvailable(slot));

            // Filter and limit to available slots
            var missingParts = skeletonParts
                .Where(part => !PowerupScript.IsInDrawer_Quick(part) &&
                               !PowerupScript.IsEquipped_Quick(part))
                .Take(availableSlots)
                .ToList();


            Logger.LogInfo("Missing skeleton parts: " + string.Join(", ", missingParts));

            int partsAdded = 0;
            foreach (var part in missingParts)
            {
                if (PowerupScript.PutInDrawer_Quick(part))
                {
                    Logger.LogInfo("Added missing skeleton part: " + part);
                    partsAdded++;
                }
                else
                {
                    Logger.LogWarning("Failed to add skeleton part: " + part + " (no free drawer?)");
                }
            }
            if (partsAdded > 0 || missingParts.Count == 0)
            {
                corpse = true;
            }
        }

        private static class Patches
        {

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CardsPackScript), "Animator_PackPunch")]
            private static bool Animator_PackPunchPrefix(CardsPackScript __instance)
            {
                // Skip the pack punch animation entirely
                return false; // Returning false skips the original method
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MemoryPackDealUI), "DealCoroutine")]
            private static bool DealCoroutinePrefix(MemoryPackDealUI __instance, ref IEnumerator __result)
            {
                __result = FastDealCoroutine(__instance);
                return false; // Skip original method
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(PowerupScript), "ThrowAway")]
            private static bool setThrowAway()
            {
                throwAway = true;
                Debug.Log("ThrowAway called, setting throwAway to true");
                return true; // Don't skip original method
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(GameplayMaster), "Start")]
            private static bool Start()
            {
                corpse = false;
                Debug.Log("GameplayMaster Start called, setting corpse to false");
                return true; // Don't skip original method
            }

            private static IEnumerator FastDealCoroutine(MemoryPackDealUI instance)
            {
                float transitionSpeed = (float)Data.settings.transitionSpeed;
                BigInteger debtGet = GameplayData.DebtGet();
                int packsOfferN = GameplayData.RunModifier_BonusPacksThisTime_Get();
                CameraController.PositionKind backupCameraPosition = CameraController.GetPositionKind();
                CameraController.SetPosition(CameraController.PositionKind.ATMStraight, false, 1f);

                // Skip initial wait
                RunModifierScript.TriggerAnimation_IfEquipped(RunModifierScript.Identifier.extraPacks);
                while (PowerupTriggerAnimController.HasAnimations())
                {
                    yield return null;
                }

                // Get the callback methods
                var onYesCallback = new DialogueScript.AnswerCallback(() =>
                {
                    Traverse.Create(instance).Field("_skipDeadlineDealAnswer").SetValue(true);
                });

                var onNoCallback = new DialogueScript.AnswerCallback(() =>
                {
                    Traverse.Create(instance).Field("_skipDeadlineDealAnswer").SetValue(false);
                });

                // Show dialogue
                if (packsOfferN <= 1)
                {
                    DialogueScript.SetQuestionDialogue(false,
                        onYesCallback,
                        onNoCallback,
                        new string[] { "DIALOGUE_SKIP_DEADLINE_PROPOSAL_0", "DIALOGUE_SKIP_DEADLINE_PROPOSAL_1" });
                }
                else
                {
                    DialogueScript.SetQuestionDialogue(false,
                        onYesCallback,
                        onNoCallback,
                        new string[] { "DIALOGUE_SKIP_DEADLINE_PROPOSAL_0", "DIALOGUE_SKIP_DEADLINE_PROPOSAL_1_ALT_PLURAL" });
                }

                // Wait for player to choose Yes or No
                while (DialogueScript.IsEnabled())
                {
                    yield return null;
                }

                // Get the player's answer
                bool skipDeadlineDealAnswer = Traverse.Create(instance).Field("_skipDeadlineDealAnswer").GetValue<bool>();

                // Only proceed if player chose YES
                if (skipDeadlineDealAnswer)
                {
                    bool firstTimeEverOpeningAPack = Data.game.RunModifier_UnlockedTotalNumber() <= 0;

                    // Instant deposit - no animation, no dialogue
                    while (GameplayData.DepositGet() < debtGet)
                    {
                        GameplayMaster.instance.FCall_DepositTry();
                        yield return null;
                    }

                    // Process all packs instantly
                    for (int packsIterator = 0; packsIterator < packsOfferN; packsIterator++)
                    {
                        // Get references
                        var cardsHolder = Traverse.Create(instance).Field("cardsHolder").GetValue<Transform>();

                        // Set showCards to true to position cards correctly
                        Traverse.Create(instance).Field("showCards").SetValue(true);

                        // Unlock achievement
                        PlatformAPI.AchievementUnlock_FullGame(PlatformAPI.AchievementFullGame.ANewHobby);

                        // Spawn cards instantly
                        CardScript card0 = CardScript.PoolSpawn(RunModifierScript.CardGetFromPack(), 500f, cardsHolder);
                        CardScript card = CardScript.PoolSpawn(RunModifierScript.CardGetFromPack(), 500f, cardsHolder);
                        CardScript card2 = CardScript.PoolSpawn(RunModifierScript.CardGetFromPack(), 500f, cardsHolder);

                        // Position cards (will be positioned by CardsMoveAroundCoroutine)
                        card0.rectTransform.anchoredPosition = new UnityEngine.Vector2(-212f, 32f);
                        card.rectTransform.anchoredPosition = new UnityEngine.Vector2(0f, 32f);
                        card2.rectTransform.anchoredPosition = new UnityEngine.Vector2(212f, 32f);

                        card0.rectTransform.SetLocalZ(0f);
                        card.rectTransform.SetLocalZ(0f);
                        card2.rectTransform.SetLocalZ(0f);

                        // Add cards to collection
                        Data.game.RunModifier_OwnedCount_Set(card0.identifier, Data.game.RunModifier_OwnedCount_Get(card0.identifier) + 1);
                        Data.game.RunModifier_OwnedCount_Set(card.identifier, Data.game.RunModifier_OwnedCount_Get(card.identifier) + 1);
                        Data.game.RunModifier_OwnedCount_Set(card2.identifier, Data.game.RunModifier_OwnedCount_Get(card2.identifier) + 1);

                        Data.game.RunModifier_UnlockedTimes_Set(card0.identifier, Data.game.RunModifier_UnlockedTimes_Get(card0.identifier) + 1);
                        Data.game.RunModifier_UnlockedTimes_Set(card.identifier, Data.game.RunModifier_UnlockedTimes_Get(card.identifier) + 1);
                        Data.game.RunModifier_UnlockedTimes_Set(card2.identifier, Data.game.RunModifier_UnlockedTimes_Get(card2.identifier) + 1);

                        card0.TextUpdate();
                        card.TextUpdate();
                        card2.TextUpdate();

                        // Start card animation coroutine
                        var coroutineCardsMoveAround = Traverse.Create(instance).Field("coroutineCardsMoveAround").GetValue<Coroutine>();
                        if (coroutineCardsMoveAround != null)
                        {
                            (instance as MonoBehaviour).StopCoroutine(coroutineCardsMoveAround);
                        }

                        var method = typeof(MemoryPackDealUI).GetMethod("CardsMoveAroundCoroutine",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var newCoroutine = (instance as MonoBehaviour).StartCoroutine(
                            (IEnumerator)method.Invoke(instance, new object[] { card0, card, card2 })
                        );
                        Traverse.Create(instance).Field("coroutineCardsMoveAround").SetValue(newCoroutine);

                        // Short delay for cards to appear
                        yield return null;

                        // Auto-flip all cards instantly (no user input needed)
                        card0.FlipRequest();
                        card.FlipRequest();
                        card2.FlipRequest();

                        // Open inspector
                        CardsInspectorScript.Open("CARDS_INSPECTOR_TITLE__PACK_OPENED", "CARDS_INSPECTOR_DESCRIPTION__PACK_OPENED", CardsInspectorScript.PromptKind.none);

                        // Wait a short moment for visual feedback
                        float timer = 0.5f;
                        while (timer > 0f)
                        {
                            timer -= Tick.Time;
                            yield return null;
                        }

                        // Wait until all cards are flipped
                        while (card0.IsFaceDown() || card.IsFaceDown() || card2.IsFaceDown() || CardScript.IsAnyCardFoiling())
                        {
                            yield return null;
                        }

                        // Update inspector to "all inspected" state
                        CardsInspectorScript.Close();
                        CardsInspectorScript.Open("CARDS_INSPECTOR_TITLE__PACK_OPENED_ALL_INSPECTED",
                            "CARDS_INSPECTOR_DESCRIPTION__PACK_OPENED_ALL_INSPECTED",
                            CardsInspectorScript.PromptKind.none);

                        // Auto-continue (no user input needed)
                        card0.OutlineForceHidden(true);
                        card.OutlineForceHidden(true);
                        card2.OutlineForceHidden(true);

                        Traverse.Create(instance).Field("showCards").SetValue(false);
                        Sound.Play("SoundCardsMoveOut", 1f, 1f);


                        // Stop animation coroutine
                        coroutineCardsMoveAround = Traverse.Create(instance).Field("coroutineCardsMoveAround").GetValue<Coroutine>();
                        if (coroutineCardsMoveAround != null)
                        {
                            (instance as MonoBehaviour).StopCoroutine(coroutineCardsMoveAround);
                            Traverse.Create(instance).Field("coroutineCardsMoveAround").SetValue(null);
                        }

                        // Clean up cards
                        card0?.PoolDestroy();
                        card?.PoolDestroy();
                        card2?.PoolDestroy();

                        CardsInspectorScript.Close();

                        yield return null;
                    }

                    // Handle first time tutorial
                    if (firstTimeEverOpeningAPack)
                    {
                        CameraController.SetPosition(CameraController.PositionKind.DeckBox, false, 1f * transitionSpeed);

                        float timer = 0.5f;
                        while (timer > 0f)
                        {
                            timer -= Tick.Time;
                            yield return null;
                        }

                        DialogueScript.SetDialogue(true, new string[] { "DIALOGUE_SKIP_CARDS_EXPLANATION_0" });
                        DialogueScript.SetDialogueInputDelay(0.5f);

                        while (DialogueScript.IsEnabled())
                        {
                            yield return null;
                        }

                        CameraController.SetPosition(CameraController.PositionKind.ATMStraight, false, 0f);
                        while (!CameraController.IsCameraNearPositionAndAngle(0.1f))
                        {
                            yield return null;
                        }
                    }

                    GameplayData.RunModifier_AcceptedDealsCounter_Set(GameplayData.RunModifier_AcceptedDealsCounter_Get() + 1);

                    // Check for collector achievement
                    bool flag6 = true;
                    int num2 = 20;
                    for (int i = 0; i < num2; i++)
                    {
                        RunModifierScript.Identifier identifier = (RunModifierScript.Identifier)i;
                        if (identifier != RunModifierScript.Identifier.defaultModifier &&
                            Data.game.RunModifier_UnlockedTimes_Get(identifier) <= 0)
                        {
                            flag6 = false;
                            break;
                        }
                    }
                    if (flag6)
                    {
                        PowerupScript.Unlock(PowerupScript.Identifier.TheCollector);
                    }

                    while (PowerupTriggerAnimController.HasAnimations())
                    {
                        yield return null;
                    }
                }
                else
                {
                    DialogueScript.SetDialogue(false, new string[] { "DIALOGUE_SKIP_DEADLINE_ANSWER_NO" });
                    while (DialogueScript.IsEnabled())
                    {
                        yield return null;
                    }
                }

                VirtualCursors.CursorDesiredVisibilitySet(0, false);
                CameraController.SetPosition(backupCameraPosition, false, 0f);
                CameraController.SetFreeCameraRotation(CameraController.instance.ATMStraightTransform.eulerAngles);

                var holder = Traverse.Create(instance).Field("holder").GetValue<GameObject>();
                holder.SetActive(false);

                Traverse.Create(instance).Field("dealCoroutine").SetValue(null);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CardScript), "Update")]
            private static bool UpdatePrefix(CardScript __instance,
                ref bool ___faceDown,
                ref bool ___flipRequest,
                GameObject ___canFlipGlow,
                Transform ___meshHolder,
                TextMeshPro ___textVictories,
                TextMeshPro ___textCopies,
                TextMeshPro ___textActive,
                ref bool ___forceHideText,
                ref Outline ___myOutline,
                ref Coroutine ___coroutineFoiling,
                ref bool ___forceHideOutline)
            {
                bool flag = __instance.IsHovered();
                bool flag2 = MemoryPackDealUI.IsDealRunnning();
                bool flag3 = Data.game.RunModifier_OwnedCount_Get(__instance.identifier) != 0;
                bool flag4 = ___faceDown && (Data.game.RunModifier_UnlockedTimes_Get(__instance.identifier) > 0 ||
                    __instance.identifier == RunModifierScript.Identifier.defaultModifier || flag2);

                // Auto-flip ALL unlocked cards instantly (no hover required)
                if (flag4)
                {
                    ___faceDown = false;
                    Traverse.Create(__instance).Method("FlipVfxAndSfx").GetValue();
                    __instance.TextUpdate();
                }

                ___flipRequest = false;

                if (___canFlipGlow.activeSelf != flag4)
                {
                    ___canFlipGlow.SetActive(flag4);
                }

                // Set angle instantly - no animation
                float targetAngle = ___faceDown ? -180f : 0f;
                ___meshHolder.SetLocalYAngle(targetAngle);

                // Color animations
                Color color = (Util.AngleSin(Tick.PassedTime * 1440f) > 0f) ? Color.yellow : new Color(1f, 0.5f, 0f, 1f);
                color.a = ___textActive.alpha;
                if (___textActive.color != color)
                {
                    ___textActive.color = color;
                }

                Color color2 = Color.white;
                if (!flag3)
                {
                    color2 = ((Util.AngleSin(Tick.PassedTime * 1440f) > 0f) ? Color.red : Color.yellow);
                }
                if (___textCopies.color != color2)
                {
                    ___textCopies.color = color2;
                }

                float num3 = (float)((Mathf.Abs(targetAngle) < 90f && !___forceHideText) ? 1 : 0);
                if (___textVictories.alpha != num3)
                {
                    ___textVictories.alpha = num3;
                }
                if (___textCopies.alpha != num3)
                {
                    ___textCopies.alpha = num3;
                }
                if (___textActive.alpha != num3)
                {
                    ___textActive.alpha = num3;
                }

                bool flag6 = __instance.IsFoiling();
                if (!flag6 && flag && !__instance.IsFaceDown())
                {
                    int num4 = (int)Traverse.Create(__instance).Method("DesiredFoilLevelGet").GetValue();
                    int num5 = Data.game.RunModifier_FoilLevel_Get(__instance.identifier);
                    if (num4 > num5)
                    {
                        var foilingMethod = Traverse.Create(__instance).Method("FoilingCoroutine");
                        if (foilingMethod.MethodExists())
                        {
                            ___coroutineFoiling = __instance.StartCoroutine((System.Collections.IEnumerator)foilingMethod.GetValue());
                        }
                    }
                }

                bool flag7 = flag && !flag6 && Mathf.Abs(Util.AngleSin(targetAngle)) < 0.05f && !___forceHideOutline;
                if (___myOutline.enabled != flag7)
                {
                    ___myOutline.enabled = flag7;
                }

                return false; // Skip original method
            }
        }
    }
}
