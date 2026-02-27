using UnityEngine;

public class ColdClearSmokeTest : MonoBehaviour
{
    void Start()
    {
        ColdClearNative.cc_default_options(out var opt);
        Debug.Log($"ColdClear OK. threads={opt.threads}, hold={opt.use_hold}, speculate={opt.speculate}");
    }
}