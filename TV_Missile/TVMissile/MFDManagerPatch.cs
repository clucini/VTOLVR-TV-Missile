using UnityEngine;
using Harmony;
using TV_Missile;
using UnityEngine.Events;

[HarmonyPatch(typeof(MFDManager), "SetupDict")]
class PatchSetupDict
{
	public static void Prefix(MFDManager __instance)
	{
        MFDPage[] componentsInChildren = __instance.GetComponentsInChildren<MFDPage>(includeInactive: true);
        // TODO: Kinda dumb 
        foreach (MFDPage Page in componentsInChildren)
        {
            if(Page.pageName == "TV")
            {
                Page.GetComponent<TVMFDPage>().OpenPage();
                Debug.LogWarning("MFD already has tv");
                return;
            }
        }

        bool foundHome = false;
        foreach (MFDPage Page in componentsInChildren)
        {
            if(Page.pageName == "home")
            {
                foundHome = true;
                Object.Instantiate(Main.MFDGameObject, __instance.transform);

                Debug.LogWarning("Found home, adding button");
                break;
            }
        }

        int children = __instance.transform.childCount;

        Debug.LogError($"Instance : {__instance.transform.name}");
        if(!foundHome)
            Debug.LogWarning("Did not find home");
        for (int i = 0; i < children; ++i)
            Debug.LogError($"Child {i}: {__instance.transform.GetChild(i).name}");
    }
}

[HarmonyPatch(typeof(MFDPage), "Initialize")]
class HomePagePatch
{
    [HarmonyPostfix]
    public static void PostFix(MFDPage __instance)
    {
        if (__instance.pageName != "home")
            return;

        MFDPage.MFDButtonInfo info = new MFDPage.MFDButtonInfo();
        info.OnPress.AddListener( () => { __instance.OpenPage("TV"); });
        info.label = "TV";
        info.label = "TV Missile";
        info.spOnly = false;
        info.mpOnly = false;
        info.button = MFD.MFDButtons.T2;
        __instance.SetPageButton(info);
        Debug.Log("Inting home");
    }
}

// Function called at the end of setpage
[HarmonyPatch(typeof(MFD), "UpdateInputDisplayObject")]
class SetPagePatch
{
    [HarmonyPostfix]
    public static void PostFix(MFD __instance)
    {
        if(__instance.activePage.name == "TV")
        {
        }
    }
}
