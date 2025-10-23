using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Stopwatch = System.Diagnostics.Stopwatch;

// Legacy XR aliases (avoid collisions with Input System types)
using UnityEngine.XR;
using XRInputDevice   = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class TwoAFCManager : MonoBehaviour
{
    [Header("Scene References")]
    public StimulusTile leftTile;
    public StimulusTile rightTile;

    [Header("Instruction (either or both)")]
    public TextMeshProUGUI instructionText; // UGUI (Canvas). Leave null if unused.
    public TMP_Text instructionText3D;      // 3D TextMeshPro (Mesh). Leave null if unused.
    public TextMeshProUGUI hudText;         // optional HUD/debug

    [Header("Instruction Text Control")]
    public bool overrideInstructionText = true;
    [TextArea] public string instructionMessage =
        "2AFC\nPick the FULL-RES (1024) tile using your controller triggers.";
    public float quietSecondsBeforeHide = 0.35f;

    [Header("Textures (Resources/<folder>)")]
    public string resourceFolder = "NormalMaps";
    public string namePrefix = "";

    [Header("1024 reference crops (Resources/<folder>)")]
    [Tooltip("Folder under Resources that contains ONLY 1024-px crops for the reference (e.g., Resources/1024Set/Bricks004_Crops).")]
    public string ref1024Folder = "1024Set/Bricks004_Crops";

    [Header("Intro Trials (fixed)")]
    public bool runIntro = true;
    public int[] introPx = new int[] { 4, 8, 16 };

    [Header("Asymmetric Staircase (raise slower, drop faster)")]
    public int staircaseStartPx = 16;
    public int initialStepPx = 128;
    public int minStepPx = 4;
    public int maxReversals = 6;
    public int maxStaircaseTrials = 200;
    public int nCorrectToStepUp = 2;
    public float upStepMultiplier = 0.5f;
    public float downStepMultiplier = 2f;

    [Header("Timing")]
    public float itiSeconds = 0.6f;
    public float timeoutSeconds = 30f;

    [Header("XR Actions (drag from XRI Default Input Actions)")]
    public InputActionReference leftSelectRef;   // XRI LeftHand/Select (Value/Button)
    public InputActionReference rightSelectRef;  // XRI RightHand/Select
    public InputActionReference leftActivateRef; // optional
    public InputActionReference rightActivateRef;// optional

    [Header("XR Inputs (actions + raw device fallback)")]
    public bool preferActionsWhenAvailable = true;

    [Header("Response Feedback UI")]
    public TMP_Text feedbackText;              // “You picked Left/Right”
    public float feedbackSeconds = 0.8f;
    [Tooltip("Optional banner text that shows the current scene name on start or when changing.")]
    public TMP_Text sceneBannerText;
    public float sceneBannerSeconds = 1.5f;

    [Header("End of Experiment")]
    public Button nextButton;                  // reveals at the end
    [Tooltip("If set, load this scene when Next is pressed; otherwise loads the next scene in Build Settings.")]
    public string nextSceneName = "";

    [Header("Diagnostics")]
    public bool verbose = true;
    public bool pollInputs = true;
    public float pollIntervalSeconds = 0.30f;
    public bool logDeviceLists = true;
    public float devicesLogInterval = 5f;

    [Header("CSV Logging")]
    public bool logToCsv = true;
    public string resultsFolderName = "2AFC results";
    public bool writeInAssetsWhenInEditor = true;

    // ======== ADDED: Audio feedback (keeps your label, just adds sound) ========
    [Header("Audio Feedback (Added)")]
    [SerializeField] private AudioSource audioSource;          // created at runtime if null
    [SerializeField] private bool useSynthPlaceholders = true; // true = use SoundSynth ding/noise
    [SerializeField] private AudioClip correctClipOverride;    // optional user clip
    [SerializeField] private AudioClip wrongClipOverride;      // optional user clip
    private AudioClip correctClip;
    private AudioClip wrongClip;

    // ======== ADDED: Inter-trial grey flash (Screen Space - Overlay) ===========
    [Header("Inter-Trial Grey Flash (Added)")]
    public Image flashPanel;                 // assign a full-screen grey Image
    public bool flashBetweenTrials = true;
    public float flashDuration = 0.9f;

    // ---------- internals ----------
    private Dictionary<int, Texture2D> texByPx;     // all normal maps by size
    private List<int> levels;                       // 1024..4 actually present
    private List<Texture2D> refCrops = new();       // 1024 reference crops
    private int lastRefIdx = -1;                    // prevent immediate repeat

    private int trial;
    private bool waiting;
    private bool leftStd;
    private Stopwatch sw;

    // staircase state
    private int curIdx;
    private int baseStepIdx; // 1 step = 4 px
    private int reversals;
    private int staircaseTrials;
    private int lastMoveDir; // +1 toward 1024, -1 away
    private int consecCorrect;

    private enum Pending { None, Left, Right }
    private Pending pending = Pending.None;

    // actions (resolved from refs)
    private InputAction leftSelect, rightSelect, leftActivate, rightActivate;

    // Raw InputSystem controls (generic XRController)
    private AxisControl leftTriggerCtrl, rightTriggerCtrl;
    private AxisControl leftGripCtrl, rightGripCtrl;
    private ButtonControl leftPrimaryBtn, rightPrimaryBtn;
    private ButtonControl leftSecondaryBtn, rightSecondaryBtn;
    private ButtonControl leftTriggerPressedBtn, rightTriggerPressedBtn;

    // Oculus layout aliases (if available)
    private AxisControl leftOculusTrigger, rightOculusTrigger;
    private ButtonControl leftOculusPrimary, rightOculusPrimary;
    private ButtonControl leftOculusSecondary, rightOculusSecondary;

    // Legacy XR
    private XRInputDevice xrLeft, xrRight;
    private float legacyRefreshTimer;

    private bool leftLatched, rightLatched;
    private bool firstInteractionSeen = false;
    private float trialStartTime = 0f;

    private DataLogger logger;

    // ================= Lifecycle =================
    private void Awake() { ResolveInputs(); }
    private void OnEnable()  { EnableActions(true); }
    private void OnDisable() { EnableActions(false); }

    private void Start()
    {
        // Instruction text visible at start
        if (overrideInstructionText && !string.IsNullOrEmpty(instructionMessage))
        {
            if (instructionText)   instructionText.text = instructionMessage;
            if (instructionText3D) instructionText3D.text = instructionMessage;
        }
        if (instructionText)   instructionText.gameObject.SetActive(true);
        if (instructionText3D) instructionText3D.gameObject.SetActive(true);

        // Hide feedback + Next button initially
        if (feedbackText) feedbackText.gameObject.SetActive(false);
        if (nextButton)
        {
            nextButton.gameObject.SetActive(false);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextPressed);
        }

        // Optional scene banner
        if (sceneBannerText)
        {
            sceneBannerText.text = $"Scene: {SceneManager.GetActiveScene().name}";
            StartCoroutine(ShowTemporarily(sceneBannerText.gameObject, sceneBannerSeconds));
        }

        LoadTextures();
        LoadRefCrops();
        BuildLevels_1024_to_4_step_4();

        if (!texByPx.ContainsKey(1024))
        {
            Debug.LogError("[TwoAFC] Missing 1024px normal map in Resources/" + resourceFolder);
            enabled = false; return;
        }
        if (refCrops.Count == 0)
        {
            Debug.LogWarning("[TwoAFC] No 1024-px reference crops found; falling back to main 1024 texture.");
        }

        if (logToCsv)
        {
            logger = new DataLogger(resultsFolderName, writeInAssetsWhenInEditor);
            Debug.Log("[TwoAFC] Logging to: " + logger.FilePath);
        }

        // ======== ADDED: audio init ========
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D
        if (correctClipOverride && wrongClipOverride && !useSynthPlaceholders)
        {
            correctClip = correctClipOverride;
            wrongClip   = wrongClipOverride;
        }
        else
        {
            // uses your SoundSynth.cs placeholder tones
            correctClip = SoundSynth.MakeSine(880f, 0.18f, 0.22f);
            wrongClip   = SoundSynth.MakeNoise(0.22f, 0.18f);
        }

        // ======== ADDED: flash init (keep disabled) ========
        if (flashPanel) flashPanel.gameObject.SetActive(false);

        sw = new Stopwatch();
        trial = 0;

        StartCoroutine(MainRoutine());
        if (logDeviceLists) StartCoroutine(LogDevicesPeriodically());
    }

    // ================= Main flow =================
    private IEnumerator MainRoutine()
    {
        if (runIntro)
        {
            foreach (var px in introPx)
            {
                if (!texByPx.ContainsKey(px)) continue;
                yield return RunOneTrialAt(px, "intro");
                // ORIGINAL: yield return new WaitForSeconds(itiSeconds);
                // ADDED: unified pause to allow optional grey flash
                yield return InterTrialPause();
            }
        }

        // Staircase init
        curIdx = IndexForPxClamped(staircaseStartPx);
        baseStepIdx = Mathf.Max(1, PixelsToIndexStep(initialStepPx));
        reversals = 0;
        lastMoveDir = 0;
        staircaseTrials = 0;
        consecCorrect = 0;

        if (verbose)
            Debug.Log($"[TwoAFC] Staircase start: cmp={levels[curIdx]}px, baseStepIdx={baseStepIdx} (~{IndexToPixels(baseStepIdx)}px)");

        while (!StaircaseShouldStop())
        {
            int cmpPx = levels[curIdx];
            yield return RunOneTrialAt(cmpPx, "stair");
            staircaseTrials++;
            // ORIGINAL: yield return new WaitForSeconds(itiSeconds);
            // ADDED: unified pause to allow optional grey flash
            yield return InterTrialPause();
        }

        int jndPx = levels[curIdx];
        Debug.Log($"[TwoAFC] STAIRCASE COMPLETE → JND ≈ {jndPx} px (reversals={reversals})");

        if (instructionText)   instructionText.text = $"Done.\nJND ≈ {jndPx} px";
        if (instructionText3D) instructionText3D.text = $"Done.\nJND ≈ {jndPx} px";
        if (feedbackText)
        {
            feedbackText.text = "Experiment complete.";
            feedbackText.gameObject.SetActive(true);
        }

        if (logToCsv && logger != null)
        {
            logger.LogSummary("JND_px", jndPx.ToString());
            logger.SaveNow();
        }

        // show Next button
        if (nextButton) nextButton.gameObject.SetActive(true);
    }

    private IEnumerator RunOneTrialAt(int cmpPx, string stage)
    {
        trial++;
        pending = Pending.None;
        leftLatched = rightLatched = false;

        // Randomize standard side every trial
        leftStd = (Random.value < 0.5f);

        // 1024 reference: pick a crop, but NEVER the same as previous trial
        var texStd = PickRefCropOrFallback();

        // Comparison texture (ladder)
        var texCmp = texByPx[cmpPx];

        if (leftStd) { leftTile.SetNormal(texStd); rightTile.SetNormal(texCmp); }
        else         { leftTile.SetNormal(texCmp); rightTile.SetNormal(texStd); }

        Debug.Log($"[TwoAFC] Trial {trial}: Std={(leftStd ? "Left" : "Right")}(1024 crop), Cmp={cmpPx}px [{stage}]");

        if (hudText)
            hudText.text = $"Trial {trial}\nStd: {(leftStd ? "Left(1024)" : "Right(1024)")}  Cmp: {cmpPx}px";

        waiting = true;
        sw.Restart();
        trialStartTime = Time.time;
        if (pollInputs) StartCoroutine(PollInputs());

        float t0 = Time.time;
        string resp = ""; bool correct = false;

        while (waiting && Time.time - t0 < timeoutSeconds)
        {
            if (pending == Pending.Left)  { HideInstructionAfterQuiet(); resp = "Left";  correct =  leftStd; waiting = false; break; }
            if (pending == Pending.Right) { HideInstructionAfterQuiet(); resp = "Right"; correct = !leftStd; waiting = false; break; }

            if (EdgePressed(true))  { HideInstructionAfterQuiet(); resp = "Left";  correct =  leftStd; waiting = false; break; }
            if (EdgePressed(false)) { HideInstructionAfterQuiet(); resp = "Right"; correct = !leftStd; waiting = false; break; }

            yield return null;
        }

        sw.Stop();

        if (waiting)
        {
            resp = "Timeout"; correct = false; waiting = false;
            Debug.LogWarning("[TwoAFC] Response: TIMEOUT");
        }
        else
        {
            Debug.Log($"[TwoAFC] Response: {resp} (correct={correct})");
            // short feedback “You picked …” (kept)
            if (feedbackText)
            {
                feedbackText.text = $"You picked {resp}";
                StartCoroutine(ShowTemporarily(feedbackText.gameObject, feedbackSeconds));
            }
            // ======== ADDED: play audio feedback ========
            if (audioSource != null)
            {
                if (correct) audioSource.PlayOneShot(correctClip);
                else         audioSource.PlayOneShot(wrongClip);
            }
        }

        if (logToCsv && logger != null)
        {
            long rtMs = (long)sw.Elapsed.TotalMilliseconds;
            string stdSide = leftStd ? "Left" : "Right";
            string comment = stage == "stair" ? $"stair baseStep~{IndexToPixels(baseStepIdx)}px" : "intro";
            logger.LogTrial(trial, stdSide, cmpPx, resp, correct, rtMs, -1, cmpPx, reversals, comment);
        }

        if (stage == "stair") UpdateStaircase(correct);
    }

    private void HideInstructionAfterQuiet()
    {
        if (firstInteractionSeen) return;
        if (Time.time - trialStartTime < quietSecondsBeforeHide) return;
        firstInteractionSeen = true;

        if (instructionText)   instructionText.gameObject.SetActive(false);
        if (instructionText3D) instructionText3D.gameObject.SetActive(false);
    }

    // ================= 1024 crops =================
    private void LoadRefCrops()
    {
        refCrops.Clear();
        if (!string.IsNullOrEmpty(ref1024Folder))
        {
            var all = Resources.LoadAll<Texture2D>(ref1024Folder);
            foreach (var t in all)
            {
                // accept any texture here; user guarantees these are 1024 crops
                if (t != null && !refCrops.Contains(t)) refCrops.Add(t);
            }
        }
        if (verbose) Debug.Log($"[TwoAFC] Loaded ref crops ({refCrops.Count}) from Resources/{ref1024Folder}");
    }

    private Texture2D PickRefCropOrFallback()
    {
        if (refCrops.Count == 0)
            return texByPx[1024];

        // choose a random index different from lastRefIdx
        int idx;
        if (refCrops.Count == 1) idx = 0;
        else
        {
            do { idx = Random.Range(0, refCrops.Count); } while (idx == lastRefIdx);
        }
        lastRefIdx = idx;
        return refCrops[idx];
    }

    // ================= Staircase =================
    private void UpdateStaircase(bool correct)
    {
        int moveDir = 0; // +1 toward 1024 (harder), -1 away (easier)

        if (correct)
        {
            consecCorrect++;
            if (consecCorrect >= nCorrectToStepUp)
            {
                int upIdx = Mathf.Max(1, Mathf.RoundToInt(baseStepIdx * Mathf.Max(0.1f, upStepMultiplier)));
                int newIdx = Mathf.Max(1, curIdx - upIdx);
                moveDir = newIdx < curIdx ? +1 : 0;
                curIdx = newIdx;
                consecCorrect = 0;
            }
        }
        else
        {
            consecCorrect = 0;
            int downIdx = Mathf.Max(1, Mathf.RoundToInt(baseStepIdx * Mathf.Max(0.1f, downStepMultiplier)));
            int newIdx = Mathf.Min(levels.Count - 1, curIdx + downIdx);
            moveDir = newIdx > curIdx ? -1 : 0;
            curIdx = newIdx;
        }

        if (moveDir != 0 && lastMoveDir != 0 && moveDir != lastMoveDir)
        {
            reversals++;
            if (baseStepIdx > 1) baseStepIdx = Mathf.Max(1, baseStepIdx / 2);
            if (verbose) Debug.Log($"[TwoAFC] Reversal #{reversals} → baseStepIdx={baseStepIdx} (~{IndexToPixels(baseStepIdx)}px)");
        }
        if (moveDir != 0) lastMoveDir = moveDir;

        if (verbose)
        {
            int upDbg = Mathf.Max(1, Mathf.RoundToInt(baseStepIdx * Mathf.Max(0.1f, upStepMultiplier)));
            int dnDbg = Mathf.Max(1, Mathf.RoundToInt(baseStepIdx * Mathf.Max(0.1f, downStepMultiplier)));
            Debug.Log($"[TwoAFC] Stair upd: correct={correct}, consec={consecCorrect}, curPx={levels[curIdx]}, baseIdx={baseStepIdx}, upIdx={upDbg}, downIdx={dnDbg}, revs={reversals}");
        }
    }

    private bool StaircaseShouldStop()
    {
        if (staircaseTrials >= maxStaircaseTrials) return true;
        if (IndexToPixels(baseStepIdx) <= minStepPx && reversals >= maxReversals) return true;
        if (curIdx < 1 || curIdx > levels.Count - 1) return true;
        return false;
    }

    // ================= Inputs =================
    private void ResolveInputs()
    {
        // explicit refs
        leftSelect   = leftSelectRef   ? leftSelectRef.action   : null;
        rightSelect  = rightSelectRef  ? rightSelectRef.action  : null;
        leftActivate = leftActivateRef ? leftActivateRef.action : null;
        rightActivate= rightActivateRef? rightActivateRef.action: null;

        // Generic XRController controls
        leftTriggerCtrl        = InputSystem.FindControl("<XRController>{LeftHand}/trigger")         as AxisControl;
        rightTriggerCtrl       = InputSystem.FindControl("<XRController>{RightHand}/trigger")        as AxisControl;
        leftGripCtrl           = InputSystem.FindControl("<XRController>{LeftHand}/grip")            as AxisControl;
        rightGripCtrl          = InputSystem.FindControl("<XRController>{RightHand}/grip")           as AxisControl;
        leftPrimaryBtn         = InputSystem.FindControl("<XRController>{LeftHand}/primaryButton")   as ButtonControl;
        rightPrimaryBtn        = InputSystem.FindControl("<XRController>{RightHand}/primaryButton")  as ButtonControl;
        leftSecondaryBtn       = InputSystem.FindControl("<XRController>{LeftHand}/secondaryButton") as ButtonControl;
        rightSecondaryBtn      = InputSystem.FindControl("<XRController>{RightHand}/secondaryButton")as ButtonControl;
        leftTriggerPressedBtn  = InputSystem.FindControl("<XRController>{LeftHand}/triggerPressed")  as ButtonControl;
        rightTriggerPressedBtn = InputSystem.FindControl("<XRController>{RightHand}/triggerPressed") as ButtonControl;

        // Oculus layouts (if available)
        leftOculusTrigger   = InputSystem.FindControl("<OculusTouchController>{LeftHand}/trigger")        as AxisControl;
        rightOculusTrigger  = InputSystem.FindControl("<OculusTouchController>{RightHand}/trigger")       as AxisControl;
        leftOculusPrimary   = InputSystem.FindControl("<OculusTouchController>{LeftHand}/primaryButton")  as ButtonControl;
        rightOculusPrimary  = InputSystem.FindControl("<OculusTouchController>{RightHand}/primaryButton") as ButtonControl;
        leftOculusSecondary = InputSystem.FindControl("<OculusTouchController>{LeftHand}/secondaryButton")as ButtonControl;
        rightOculusSecondary= InputSystem.FindControl("<OculusTouchController>{RightHand}/secondaryButton")as ButtonControl;

        RefreshLegacyXRDevices();
        Debug.Log($"[TwoAFC] Actions present: LSel={(leftSelect!=null)} RSel={(rightSelect!=null)}  LAct={(leftActivate!=null)} RAct={(rightActivate!=null)}");
    }

    private void EnableActions(bool on)
    {
        System.Action<InputAction,bool> set = (a,enable) => { if (a == null) return; if (enable) a.Enable(); else a.Disable(); };
        set(leftSelect,   on);
        set(rightSelect,  on);
        set(leftActivate, on);
        set(rightActivate,on);
    }

    private IEnumerator LogDevicesPeriodically()
    {
        while (true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[TwoAFC] Connected InputSystem devices:");
            foreach (var d in InputSystem.devices)
                sb.AppendLine($"  - {d.layout}  '{d.displayName}' path={d.path}");
            Debug.Log(sb.ToString());

            Debug.Log($"[TwoAFC] Legacy XR valid? left={xrLeft.isValid} right={xrRight.isValid}");
            yield return new WaitForSeconds(devicesLogInterval);
        }
    }

    private void RefreshLegacyXRDevices()
    {
        var L = new List<XRInputDevice>();
        var R = new List<XRInputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand,  L);
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, R);
        xrLeft  = (L.Count>0) ? L[0] : default;
        xrRight = (R.Count>0) ? R[0] : default;
    }

    private IEnumerator PollInputs()
    {
        while (waiting)
        {
            legacyRefreshTimer += pollIntervalSeconds;
            if (legacyRefreshTimer >= 2f)
            {
                legacyRefreshTimer = 0f;
                RefreshLegacyXRDevices();
            }

            float lv = ReadLeftRaw();
            float rv = ReadRightRaw();
            Debug.Log($"[TwoAFC] POLL  Left={lv:0.00}  Right={rv:0.00}");
            yield return new WaitForSeconds(pollIntervalSeconds);
        }
    }

    private bool EdgePressed(bool isLeft)
    {
        float v = isLeft ? ReadLeftRaw() : ReadRightRaw();
        ref bool latched = ref (isLeft ? ref leftLatched : ref rightLatched);
        bool now = v > 0.5f;
        bool rising = now && !latched;
        latched = now;
        return rising;
    }

    private float ReadLeftRaw()
    {
        float v = 0f;
        if (preferActionsWhenAvailable)
        {
            if (leftSelect   != null) { try { v = Mathf.Max(v, leftSelect.ReadValue<float>()); }   catch { if (leftSelect.WasPressedThisFrame()) v = 1f; } }
            if (leftActivate != null) { try { v = Mathf.Max(v, leftActivate.ReadValue<float>()); } catch { if (leftActivate.WasPressedThisFrame()) v = 1f; } }
        }
        if (leftTriggerCtrl        != null) v = Mathf.Max(v, leftTriggerCtrl.ReadValue());
        if (leftGripCtrl           != null) v = Mathf.Max(v, leftGripCtrl.ReadValue());
        if (leftTriggerPressedBtn  != null) v = Mathf.Max(v, leftTriggerPressedBtn.isPressed ? 1f : 0f);
        if (leftPrimaryBtn         != null) v = Mathf.Max(v, leftPrimaryBtn.isPressed ? 1f : 0f);
        if (leftSecondaryBtn       != null) v = Mathf.Max(v, leftSecondaryBtn.isPressed ? 1f : 0f);
        if (leftOculusTrigger      != null) v = Mathf.Max(v, leftOculusTrigger.ReadValue());
        if (leftOculusPrimary      != null) v = Mathf.Max(v, leftOculusPrimary.isPressed ? 1f : 0f);
        if (leftOculusSecondary    != null) v = Mathf.Max(v, leftOculusSecondary.isPressed ? 1f : 0f);
        if (xrLeft.isValid)
        {
            bool b; float f;
            if (xrLeft.TryGetFeatureValue(XRCommonUsages.trigger, out f))         v = Mathf.Max(v, f);
            if (xrLeft.TryGetFeatureValue(XRCommonUsages.grip, out f))            v = Mathf.Max(v, f);
            if (xrLeft.TryGetFeatureValue(XRCommonUsages.triggerButton, out b))   v = Mathf.Max(v, b ? 1f : 0f);
            if (xrLeft.TryGetFeatureValue(XRCommonUsages.primaryButton, out b))   v = Mathf.Max(v, b ? 1f : 0f);
            if (xrLeft.TryGetFeatureValue(XRCommonUsages.secondaryButton, out b)) v = Mathf.Max(v, b ? 1f : 0f);
        }
        return v;
    }

    private float ReadRightRaw()
    {
        float v = 0f;
        if (preferActionsWhenAvailable)
        {
            if (rightSelect   != null) { try { v = Mathf.Max(v, rightSelect.ReadValue<float>()); }   catch { if (rightSelect.WasPressedThisFrame()) v = 1f; } }
            if (rightActivate != null) { try { v = Mathf.Max(v, rightActivate.ReadValue<float>()); } catch { if (rightActivate.WasPressedThisFrame()) v = 1f; } }
        }
        if (rightTriggerCtrl        != null) v = Mathf.Max(v, rightTriggerCtrl.ReadValue());
        if (rightGripCtrl           != null) v = Mathf.Max(v, rightGripCtrl.ReadValue());
        if (rightTriggerPressedBtn  != null) v = Mathf.Max(v, rightTriggerPressedBtn.isPressed ? 1f : 0f);
        if (rightPrimaryBtn         != null) v = Mathf.Max(v, rightPrimaryBtn.isPressed ? 1f : 0f);
        if (rightSecondaryBtn       != null) v = Mathf.Max(v, rightSecondaryBtn.isPressed ? 1f : 0f);
        if (rightOculusTrigger      != null) v = Mathf.Max(v, rightOculusTrigger.ReadValue());
        if (rightOculusPrimary      != null) v = Mathf.Max(v, rightOculusPrimary.isPressed ? 1f : 0f);
        if (rightOculusSecondary    != null) v = Mathf.Max(v, rightOculusSecondary.isPressed ? 1f : 0f);
        if (xrRight.isValid)
        {
            bool b; float f;
            if (xrRight.TryGetFeatureValue(XRCommonUsages.trigger, out f))         v = Mathf.Max(v, f);
            if (xrRight.TryGetFeatureValue(XRCommonUsages.grip, out f))            v = Mathf.Max(v, f);
            if (xrRight.TryGetFeatureValue(XRCommonUsages.triggerButton, out b))   v = Mathf.Max(v, b ? 1f : 0f);
            if (xrRight.TryGetFeatureValue(XRCommonUsages.primaryButton, out b))   v = Mathf.Max(v, b ? 1f : 0f);
            if (xrRight.TryGetFeatureValue(XRCommonUsages.secondaryButton, out b)) v = Mathf.Max(v, b ? 1f : 0f);
        }
        return v;
    }

    // ================= Textures / ladder =================
    private void LoadTextures()
    {
        texByPx = new Dictionary<int, Texture2D>();
        var all = Resources.LoadAll<Texture2D>(resourceFolder);
        var rx = new Regex(@"_(\d+)px", RegexOptions.IgnoreCase);

        foreach (var t in all)
        {
            if (!string.IsNullOrEmpty(namePrefix) && !t.name.StartsWith(namePrefix)) continue;
            var m = rx.Match(t.name);
            if (!m.Success) continue;
            if (!int.TryParse(m.Groups[1].Value, out int px)) continue;
            if (!texByPx.ContainsKey(px)) texByPx.Add(px, t);
        }

        if (texByPx.Count == 0)
            Debug.LogError($"[TwoAFC] No normal maps found in Resources/{resourceFolder} (expected Name_###px).");
    }

    private void BuildLevels_1024_to_4_step_4()
    {
        var ideal = new List<int>();
        for (int px = 1024; px >= 4; px -= 4) ideal.Add(px);

        levels = new List<int>();
        foreach (var px in ideal)
            if (texByPx.ContainsKey(px)) levels.Add(px);

        if (verbose) Debug.Log($"[TwoAFC] Levels ({levels.Count}): {Preview(levels)}");
    }

    // ================= Helpers & UI =================
    private int IndexForPxClamped(int px)
    {
        int bestIdx = 1;
        int bestDelta = int.MaxValue;
        for (int i = 1; i < levels.Count; i++)
        {
            int d = Mathf.Abs(levels[i] - px);
            if (d < bestDelta) { bestDelta = d; bestIdx = i; }
        }
        return bestIdx;
    }

    private int PixelsToIndexStep(int px) => Mathf.Max(1, Mathf.RoundToInt(px / 4f));
    private int IndexToPixels(int idx)     => Mathf.Max(1, idx) * 4;

    private string Preview(List<int> arr)
    {
        if (arr == null || arr.Count == 0) return "()";
        if (arr.Count <= 40) return string.Join(",", arr);
        return $"{string.Join(",", arr.Take(20))}, … ,{string.Join(",", arr.TakeLast(20))}";
    }

    private IEnumerator ShowTemporarily(GameObject go, float seconds)
    {
        if (!go) yield break;
        go.SetActive(true);
        yield return new WaitForSeconds(seconds);
        go.SetActive(false);
    }

    // ======== ADDED: unified inter-trial pause with optional flash ========
    private IEnumerator InterTrialPause()
    {
        if (flashBetweenTrials && flashPanel != null)
        {
            flashPanel.gameObject.SetActive(true);       // instant ON
            yield return new WaitForSeconds(flashDuration);
            flashPanel.gameObject.SetActive(false);      // instant OFF
        }
        else
        {
            yield return new WaitForSeconds(itiSeconds); // original behaviour
        }
    }

    private void OnNextPressed()
    {
        // optional banner
        if (sceneBannerText)
        {
            sceneBannerText.text = "Loading next test…";
            sceneBannerText.gameObject.SetActive(true);
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int cur = SceneManager.GetActiveScene().buildIndex;
            int nxt = (cur + 1) % SceneManager.sceneCountInBuildSettings;
            SceneManager.LoadScene(nxt);
        }
    }

    // Optional UI buttons (if you keep them)
    public void SubmitLeft()  { if (waiting) pending = Pending.Left; }
    public void SubmitRight() { if (waiting) pending = Pending.Right; }
}
