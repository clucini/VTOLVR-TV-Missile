using System;
using UnityEngine;
using Harmony;

[HarmonyPatch(typeof(MFDManager), "SetupDict")]
class PatchSetupDict
{
	public Prefix(MFDManager __instance)
	{
		__instance.gameObject.AddComponent<>()
	}
}
