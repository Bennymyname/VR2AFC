using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

public class InputSniffer : MonoBehaviour
{
    public float printEvery = 0.25f;
    float t;

    InputActionAsset asset;
    InputAction leftAct;
    InputAction rightAct;

    AxisControl leftTrigger, rightTrigger;
    AxisControl leftGrip, rightGrip;
    ButtonControl leftPrimary, rightPrimary;
    ButtonControl leftSecondary, rightSecondary;

    void Awake()
    {
        var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
        asset = allAssets.FirstOrDefault(a => a.name.ToLower().Contains("xri"))
             ?? allAssets.FirstOrDefault();

        if (asset != null)
        {
            string[] L = { "XRI LeftHand/Activate", "XRI LeftHand/Activate Value", "XRI LeftHand/Select", "XRI LeftHand/Select Value", "XRI LeftHand/UISubmit" };
            string[] R = { "XRI RightHand/Activate","XRI RightHand/Activate Value","XRI RightHand/Select","XRI RightHand/Select Value","XRI RightHand/UISubmit" };
            foreach (var p in L) { var a = asset.FindAction(p, false); if (a != null) { leftAct = a; break; } }
            foreach (var p in R) { var a = asset.FindAction(p, false); if (a != null) { rightAct = a; break; } }
            if (leftAct  != null) leftAct.Enable();
            if (rightAct != null) rightAct.Enable();
        }

        leftTrigger   = InputSystem.FindControl("<XRController>{LeftHand}/trigger")  as AxisControl;
        rightTrigger  = InputSystem.FindControl("<XRController>{RightHand}/trigger") as AxisControl;
        leftGrip      = InputSystem.FindControl("<XRController>{LeftHand}/grip")     as AxisControl;
        rightGrip     = InputSystem.FindControl("<XRController>{RightHand}/grip")    as AxisControl;
        leftPrimary   = InputSystem.FindControl("<XRController>{LeftHand}/primaryButton")  as ButtonControl;
        rightPrimary  = InputSystem.FindControl("<XRController>{RightHand}/primaryButton") as ButtonControl;
        leftSecondary = InputSystem.FindControl("<XRController>{LeftHand}/secondaryButton")  as ButtonControl;
        rightSecondary= InputSystem.FindControl("<XRController>{RightHand}/secondaryButton") as ButtonControl;

        string assetName = asset != null ? asset.name : "(none)";
        string leftActName  = leftAct  != null ? leftAct.name  : "(none)";
        string rightActName = rightAct != null ? rightAct.name : "(none)";
        Debug.Log($"[Sniffer] Asset={assetName}, leftAct={leftActName}, rightAct={rightActName}");
        Debug.Log($"[Sniffer] Controls: LT={(leftTrigger!=null)} RT={(rightTrigger!=null)} LG={(leftGrip!=null)} RG={(rightGrip!=null)} " +
                  $"LP={(leftPrimary!=null)} RP={(rightPrimary!=null)} LS={(leftSecondary!=null)} RS={(rightSecondary!=null)}");
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t < printEvery) return;
        t = 0f;

        float la = ReadAction(leftAct);
        float ra = ReadAction(rightAct);

        float ltr = leftTrigger   != null ? leftTrigger.ReadValue()   : 0f;
        float rtr = rightTrigger  != null ? rightTrigger.ReadValue()  : 0f;
        float lgr = leftGrip      != null ? leftGrip.ReadValue()      : 0f;
        float rgr = rightGrip     != null ? rightGrip.ReadValue()     : 0f;
        int   lpb = leftPrimary   != null ? (leftPrimary.isPressed   ? 1 : 0) : 0;
        int   rpb = rightPrimary  != null ? (rightPrimary.isPressed  ? 1 : 0) : 0;
        int   lsb = leftSecondary != null ? (leftSecondary.isPressed ? 1 : 0) : 0;
        int   rsb = rightSecondary!= null ? (rightSecondary.isPressed? 1 : 0) : 0;

        Debug.Log($"[Sniffer] ActL={la:0.00} ActR={ra:0.00} | TrigL={ltr:0.00} TrigR={rtr:0.00} GripL={lgr:0.00} GripR={rgr:0.00} " +
                  $"PrimL={lpb} PrimR={rpb} SecL={lsb} SecR={rsb}");
    }

    float ReadAction(InputAction a)
    {
        if (a == null) return 0f;
        try { return a.ReadValue<float>(); }
        catch { return a.WasPressedThisFrame() ? 1f : 0f; }
    }
}
