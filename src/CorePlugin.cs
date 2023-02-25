using UnityEngine;
using BepInEx;

namespace Thompson {
	[BepInPlugin("pl.szikaka.thompson", "Thompson Plugin", "2.0.0")]
	public  class CorePlugin : BaseUnityPlugin {
		internal static bool modding_kit_present = false;

		private void Awake() {
			Logger.LogInfo("M1928 Thompson mod is loaded!"); 
		}

		private void Update() {
			if (!modding_kit_present) {
				Debug.LogError("Mod Thompson M1928 will not work without the modding kit, please refer to the README file for instructions on how to download it");
			}
		}
	}
}
